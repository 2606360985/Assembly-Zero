using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed class ProcurementMaterial
    {
        public int Material { get; }
        public string Name { get; }
        public int Stock { get; }
        public int InTransit { get; }
        public int Demand { get; }
        public int Shortage { get; }
        public int InTransitUsed { get; }
        public int ProductionDemand { get; }
        public int UnitPrice { get; }
        public int SupplierStock { get; }

        internal ProcurementMaterial(MaterialDefinition material, int stock, int transit, int demand,
            int shortage, int transitUsed, int production, int price, int supplierStock)
        {
            Material=material.Id;Name=material.Name;Stock=stock;InTransit=transit;Demand=demand;
            Shortage=shortage;InTransitUsed=transitUsed;ProductionDemand=production;
            UnitPrice=price;SupplierStock=supplierStock;
        }
    }

    public sealed class ProcurementOverview
    {
        public int Factory { get; }
        public int ActiveOrders { get; }
        public IReadOnlyList<ProcurementMaterial> Materials { get; }
        public IReadOnlyDictionary<int,int> NonPurchasableShortages { get; }
        public int ShortageKinds => Materials.Count(m=>m.Shortage>0);
        public long EstimatedGoodsCost => Materials.Sum(m=>(long)m.Shortage*m.UnitPrice);

        internal ProcurementOverview(int factory, int orders, ProcurementMaterial[] materials, Dictionary<int,int> unavailable)
        {
            Factory=factory;ActiveOrders=orders;Materials=Array.AsReadOnly(materials);
            NonPurchasableShortages=new ReadOnlyDictionary<int,int>(unavailable);
        }
    }

    // A disposable planning ledger: no reservations, commands, RNG, or simulation state are changed.
    public static class HardwareProcurement
    {
        public static ProcurementOverview Calculate(WorldSimulation world, int factory)
        {
            if(world==null)throw new ArgumentNullException(nameof(world));
            if(!world.Settings.IsHardware)throw new ArgumentException("Requires the hardware economy",nameof(world));
            return new Ledger(world,factory).Calculate();
        }

        private sealed class Supply
        {
            public int Material, ReservedOrder, Version, Count=1;
            public string Spec;
            public bool Transit, Projected;
        }

        private sealed class Demand
        {
            public int Quantity, Shortage, TransitUsed, Production;
        }

        private sealed class Ledger
        {
            private readonly WorldSimulation world;
            private readonly int factory;
            private readonly BusinessNode[] nodes;
            private readonly List<Supply> supplies=new List<Supply>();
            private readonly Dictionary<int,Demand> demand=new Dictionary<int,Demand>();
            private readonly Dictionary<int,int> unavailable=new Dictionary<int,int>();
            private readonly HashSet<int> seen=new HashSet<int>();

            public Ledger(WorldSimulation world, int factory)
            {
                this.world=world;this.factory=factory;
                nodes=world.State.Nodes.Where(n=>n.Factory==factory).OrderBy(n=>n.Id).ToArray();
                foreach(var material in world.Settings.Hardware.Materials.Where(m=>m.Price>0))demand[material.Id]=new Demand();
                Add(nodes.SelectMany(n=>n.Input.Concat(n.Output)),false);
                Add(world.State.Routes.Where(r=>r.Factory==factory).SelectMany(r=>r.Cargo),false);
                Add(world.State.Vehicles.Where(v=>v.TargetFactory==factory).SelectMany(v=>v.Cargo),true);
                Add(world.State.Hardware.Deliveries.Where(d=>d.Factory==factory).SelectMany(d=>d.Cargo),true);
                // Batch inputs have already been committed. Only their one expected output is available to planning.
                foreach(var node in nodes.Where(n=>n.Batch.Count>0))
                {
                    var recipe=world.Settings.Recipe(node.RecipeId);if(recipe==null)continue;
                    var output=recipe.Output;var spec=recipe.SpecId;
                    if(recipe.Machine==BuildingKind.Tester&&recipe.Byproduct!=0&&node.Batch.Any(g=>g.Flawed))
                    {output=recipe.Byproduct;spec=node.Batch[0].SpecId;}
                    if(recipe.Machine==BuildingKind.Rework)
                    {
                        spec=node.Batch.FirstOrDefault(g=>g.Material==HardwareCatalog.Defect)?.SpecId??spec;
                        if(spec==HardwareCatalog.QuietSpec)output=recipe.Byproduct;
                    }
                    supplies.Add(new Supply{Material=output,Spec=spec,Version=recipe.SpecVersion,Projected=true});
                }
            }

            private void Add(IEnumerable<Goods> goods, bool transit)
            {
                foreach(var good in goods.OrderBy(g=>g.Id))
                {
                    if(!seen.Add(good.Id))continue;
                    // Existing flawed cards cannot cover a qualified order. Do not invent future rejects.
                    if(good.Flawed&&(good.Material==HardwareCatalog.RawGPU||good.Material==HardwareCatalog.QuietRaw))continue;
                    supplies.Add(new Supply{Material=good.Material,Spec=good.SpecId,Version=good.SpecVersion,
                        ReservedOrder=good.ReservedOrder,Transit=transit});
                }
            }

            public ProcurementOverview Calculate()
            {
                // Capture physical counts before consuming this private ledger.
                var stock=supplies.Where(s=>!s.Transit&&!s.Projected).GroupBy(s=>s.Material).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Count));
                var transit=supplies.Where(s=>s.Transit).GroupBy(s=>s.Material).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Count));
                var orders=world.State.Hardware.Orders.Where(o=>o.Factory==factory&&
                    (o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready)).OrderBy(o=>o.Deadline).ThenBy(o=>o.Id).ToArray();
                // Protect each order's own reservation before allocating any shared stock.
                var reserved=orders.ToDictionary(o=>o.Id,o=>Take(o.Material,o.Quantity,o,true));
                foreach(var order in orders)Need(order.Material,Math.Max(0,order.Quantity-reserved[order.Id]),order,new HashSet<int>());
                var rows=world.Settings.Hardware.Materials.Where(m=>m.Price>0).Select(m=>
                {
                    var d=demand[m.Id];
                    return new ProcurementMaterial(m,Count(stock,m.Id),Count(transit,m.Id),d.Quantity,d.Shortage,
                        d.TransitUsed,d.Production,world.PurchasePrice(m.Id),world.State.Hardware.Stock.FirstOrDefault(s=>s.Material==m.Id)?.Quantity??0);
                }).ToArray();
                return new ProcurementOverview(factory,orders.Length,rows,unavailable);
            }

            private static int Count(Dictionary<int,int> counts,int material)=>counts.TryGetValue(material,out var count)?count:0;

            private static bool Matches(Supply supply,int material,HardwareOrder order)
            {
                if(supply.Material!=material)return false;
                if(material==HardwareCatalog.Defect)return true;
                var specific=material==HardwareCatalog.RawGPU||material==HardwareCatalog.QuietRaw||
                    material==HardwareCatalog.Tested||material==HardwareCatalog.QuietTested||
                    material==HardwareCatalog.Standard||material==HardwareCatalog.Quiet||material==HardwareCatalog.Budget;
                return !specific||(supply.Spec==order.Spec&&supply.Version==order.Version);
            }

            private int Take(int material,int quantity,HardwareOrder order,bool reservedOnly=false)
            {
                if(quantity<=0)return 0;
                var taken=0;
                foreach(var supply in supplies.Where(s=>s.Count>0&&Matches(s,material,order)&&
                    (reservedOnly?s.ReservedOrder==order.Id:s.ReservedOrder==0)).OrderBy(s=>s.Transit).ThenBy(s=>s.Projected))
                {
                    var amount=Math.Min(quantity-taken,supply.Count);supply.Count-=amount;taken+=amount;
                    if(supply.Transit&&demand.TryGetValue(material,out var row))row.TransitUsed+=amount;
                    if(taken==quantity)break;
                }
                return taken;
            }

            private bool Makes(string recipe)=>nodes.Any(n=>!n.PendingRemove&&(string.IsNullOrEmpty(n.PendingRecipe)?n.RecipeId:n.PendingRecipe)==recipe);

            private void Need(int material,int quantity,HardwareOrder order,HashSet<int> path)
            {
                if(quantity<=0)return;
                demand.TryGetValue(material,out var row);
                if(row!=null)row.Quantity+=quantity;
                quantity-=Take(material,quantity,order);
                if(quantity<=0)return;
                if(!path.Add(material)){Missing(material,quantity);return;}
                var recipe=world.Settings.Recipes.FirstOrDefault(r=>r.Output==material&&r.Machine!=BuildingKind.Rework);
                if((material==HardwareCatalog.Heat||material==HardwareCatalog.QuietHeat)&&row!=null)
                {
                    if(recipe!=null&&Makes(recipe.Id))
                    {
                        row.Production+=quantity;
                        foreach(var input in recipe.Inputs)Need(input.Material,quantity*input.Quantity,order,path);
                    }
                    else row.Shortage+=quantity;
                }
                else if(material==HardwareCatalog.Aluminum&&row!=null)
                {
                    // Only existing ore can offset a purchase; future mining is deliberately not forecast.
                    if(recipe!=null&&Makes(recipe.Id)&&recipe.Inputs.Length==1)
                    {
                        var input=recipe.Inputs[0];
                        var available=supplies.Where(s=>s.Count>0&&s.ReservedOrder==0&&s.Material==input.Material).Sum(s=>s.Count);
                        var batches=Math.Min(quantity,available/input.Quantity);
                        Take(input.Material,batches*input.Quantity,order);quantity-=batches;row.Production+=batches;
                    }
                    row.Shortage+=quantity;
                }
                else if(row!=null)row.Shortage+=quantity;
                else if(material==HardwareCatalog.Defect||recipe==null)Missing(material,quantity);
                else foreach(var input in recipe.Inputs)Need(input.Material,quantity*input.Quantity,order,path);
                path.Remove(material);
            }

            private void Missing(int material,int quantity)
            {
                unavailable[material]=Count(unavailable,material)+quantity;
            }
        }
    }
}
