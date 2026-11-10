using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    [Serializable] public sealed class FactoryAutomation
    {
        public int Factory, KeepCards=4, CashReserve=100000;
        public bool Purchase, Sell;
        public long Income, Fees;
        public string Status="先建机柜，装入自产零售显卡";
    }
    public sealed partial class WorldSimulation
    {
        private void EnsureComputeCatalog()
        {
            var buildings=Settings.Hardware.Buildings.ToList();
            foreach(var b in new[]{new HardwareBuilding{Kind=BuildingKind.ComputeRack,Name="算力机柜",Recipe="",Cost=24000},new HardwareBuilding{Kind=BuildingKind.PowerUnit,Name="供电模块",Recipe="",Cost=18000},new HardwareBuilding{Kind=BuildingKind.CoolingUnit,Name="冷却模块",Recipe="",Cost=18000}})
                if(!buildings.Any(x=>x.Kind==b.Kind))buildings.Add(b);
            Settings.Hardware.Buildings=buildings.ToArray();
        }
        public FactoryAutomation Automation(int factory)
        {
            State.Hardware.Automation??=new List<FactoryAutomation>();
            var policy=State.Hardware.Automation.FirstOrDefault(p=>p.Factory==factory);
            if(policy==null){policy=new FactoryAutomation{Factory=factory};State.Hardware.Automation.Add(policy);}return policy;
        }
        public int InstalledCards(int factory)=>State.Nodes.Where(n=>n.Factory==factory&&n.Kind==BuildingKind.ComputeRack).Sum(n=>n.Input.Count);
        public int ParkPower(int factory)=>4+State.Nodes.Where(n=>n.Factory==factory&&n.Kind==BuildingKind.PowerUnit).Sum(n=>8*n.Level);
        public int ParkCooling(int factory)=>4+State.Nodes.Where(n=>n.Factory==factory&&n.Kind==BuildingKind.CoolingUnit).Sum(n=>8*n.Level);
        public int EffectiveCards(int factory)=>Math.Min(16,Math.Min(InstalledCards(factory),Math.Min(ParkPower(factory),ParkCooling(factory))));
        private CommandResult ComputeCommand(WorldCommand c)
        {
            if(Factory(c.Factory)?.Owned!=true)return Fail("厂址未购买");
            var policy=Automation(c.Factory);
            if(c.Kind==CommandKind.AutomationPolicy)
            {
                if(c.Reserve<0||c.Reserve>40||c.Price<0||c.Price>10000000)return Fail("保留量或现金下限无效");
                policy.Purchase=c.Flag;policy.Sell=c.Priority==1;policy.KeepCards=c.Reserve;policy.CashReserve=c.Price;Revision++;return Ok("自动化规则已更新");
            }
            var rack=Node(c.Node);if(rack==null||rack.Factory!=c.Factory||rack.Kind!=BuildingKind.ComputeRack)return Fail("请选择本厂算力机柜");
            if(c.Kind==CommandKind.UninstallCompute)
            {
                var card=rack.Input.FirstOrDefault();if(card==null)return Fail("机柜为空");
                var store=State.Nodes.FirstOrDefault(n=>n.Factory==c.Factory&&n.Kind==BuildingKind.Warehouse&&CanReceive(n,card.Material));
                if(store==null)return Fail("仓库没有回收空间");rack.Input.Remove(card);store.Output.Add(card);Revision++;return Ok("显卡已完整退回仓库");
            }
            if(rack.Input.Count>=4*rack.Level)return Fail("机柜已满");
            var good=FinishedStock(c.Factory).Where(g=>g.Material==HardwareCatalog.Standard&&g.ReservedOrder==0&&!g.Flawed).OrderBy(g=>g.Id).FirstOrDefault();
            if(good==null)return Fail("需要一张未被订单预留的零售显卡");
            foreach(var store in State.Nodes.Where(n=>n.Factory==c.Factory&&n.Kind==BuildingKind.Warehouse))store.Output.Remove(good);
            rack.Input.Add(good);Revision++;return Ok("自产显卡已上架，机柜 "+rack.Input.Count+" / "+(4*rack.Level));
        }
        private void TickComputePark()
        {
            foreach(var factory in State.Factories.Where(f=>f.Owned))
            {
                var p=Automation(factory.Id);var count=InstalledCards(factory.Id);var active=EffectiveCards(factory.Id);
                var fee=active*50;var income=active*300;
                if(active>0&&State.Money<=int.MaxValue-income&&State.Money>=fee)
                {
                    Pay(fee);Earn(income,"算力渲染服务");p.Income+=income;p.Fees+=fee;
                    p.Status=active<count?"算力受限：补建供电 / 冷却；最多承接 16 卡业务":"渲染服务运行中 · 每有效卡净收益 ¥15 / 分钟";
                }
                else p.Status=count==0?"先建机柜，装入自产零售显卡":active==0?"配套容量不足":"运营资金不足或账户已满";
                foreach(var n in State.Nodes.Where(n=>n.Factory==factory.Id&&(n.Kind==BuildingKind.ComputeRack||n.Kind==BuildingKind.PowerUnit||n.Kind==BuildingKind.CoolingUnit)))
                {
                    n.RunState=n.Kind==BuildingKind.ComputeRack?(n.Input.Count==0?NodeState.Starved:State.Money<fee?NodeState.NoFunds:NodeState.Working):NodeState.Working;
                    n.Status=n.Kind==BuildingKind.ComputeRack?"已装 "+n.Input.Count+" / "+(4*n.Level)+" 张 · 全厂有效 "+active+" / "+count:n.Kind==BuildingKind.PowerUnit?"供电 +"+(8*n.Level):"冷却 +"+(8*n.Level);
                }
                if(p.Purchase)AutoSupply(factory.Id,p);
                if(p.Sell&&State.Nodes.Any(n=>n.Factory==factory.Id&&n.Kind==BuildingKind.Sales)&&FinishedStock(factory.Id).Count(g=>g.Material==HardwareCatalog.Standard&&g.ReservedOrder==0)>p.KeepCards&&State.Hardware.SpotRemaining>0)
                    SpotSell(new WorldCommand{Factory=factory.Id,Material=HardwareCatalog.Standard,Quantity=1});
            }
        }
        private void AutoSupply(int factory,FactoryAutomation policy)
        {
            var line=State.Nodes.Where(n=>n.Factory==factory&&n.Kind==BuildingKind.GPUAssembly&&!n.PendingRemove).OrderBy(n=>n.Id).FirstOrDefault();
            var sku=Sku(Recipe(line?.RecipeId)?.SpecId);if(sku==null)return;
            var goods=State.Nodes.Where(n=>n.Factory==factory).SelectMany(n=>n.Input.Concat(n.Output))
                .Concat(State.Routes.Where(r=>r.Factory==factory).SelectMany(r=>r.Cargo))
                .Concat(State.Hardware.Deliveries.Where(d=>d.Factory==factory).SelectMany(d=>d.Cargo))
                .Concat(State.Vehicles.Where(v=>v.TargetFactory==factory).SelectMany(v=>v.Cargo)).ToArray();
            foreach(var material in new[]{HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board,HardwareCatalog.Shell,HardwareCatalog.Carton,sku.Cooler})
            {
                if(!Settings.Hardware.Materials.Any(m=>m.Id==material&&m.Price>0))continue;
                int target=12*(material==HardwareCatalog.Memory?sku.Memory:1);
                int have=goods.Count(g=>g.Material==material&&(material!=HardwareCatalog.Core||g.Gen==sku.Gen&&(sku.CoreBin==0||g.CoreBin==sku.CoreBin)));
                var stock=State.Hardware.Stock.FirstOrDefault(s=>s.Material==material);int quantity=Math.Min(80,Math.Min(target-have,stock?.Quantity??0));
                var price=PurchasePrice(material,sku.Gen,sku.CoreBin);var fee=Settings.Hardware.DeliveryFee;
                quantity=Math.Min(quantity,(int)Math.Max(0,((long)State.Money-policy.CashReserve-fee)/Math.Max(1,price)));
                if(quantity<=0)continue;
                Purchase(new WorldCommand{Factory=factory,Material=material,Quantity=quantity,Generation=sku.Gen,CoreBin=sku.CoreBin,Delivery=0});
            }
        }
    }
}
