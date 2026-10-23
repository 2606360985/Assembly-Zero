using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    /// <summary>Authoritative single-player economy. Unity presentation never owns goods or cash.</summary>
    public sealed partial class WorldSimulation
    {
        public WorldState State { get; private set; }
        public EconomySettings Settings { get; }
        public int Revision { get; private set; }
        public CommandResult LastResult { get; private set; }
        private readonly Queue<WorldCommand> commands=new Queue<WorldCommand>();
        private readonly List<BeltAccessCandidate> candidates=new List<BeltAccessCandidate>();
        private readonly Dictionary<int,BusinessNode> nodeIndex=new Dictionary<int,BusinessNode>();
        private readonly Dictionary<int,List<BusinessRoute>> outgoing=new Dictionary<int,List<BusinessRoute>>();
        private bool logisticsDirty;
        public WorldSimulation(EconomySettings settings,WorldState restored=null)
        {
            Settings=settings??throw new ArgumentNullException(nameof(settings));
            State=restored??NewState();
            if(State.Version!=(Settings.IsHardware?2:1))throw new ArgumentException("存档版本与战役不匹配");
            if(Settings.IsHardware&&(State.Hardware==null||State.Hardware.ContentId!=Settings.Hardware.Id||State.Hardware.ContentVersion!=Settings.Hardware.Version))throw new ArgumentException("显卡配置版本不匹配");
            foreach(var route in State.Routes)route.Initialize();
            Index();
            if(restored==null&&Settings.IsHardware)InitializeHardware();
            if(restored!=null&&Settings.IsHardware)
            {
                MigrateHardwareLayout();
                // Resume older saves without requiring the frozen driving module to unload cargo.
                foreach(var v in State.Vehicles.Where(v=>v.Manual))
                {
                    v.Manual=false;v.Speed=v.Throttle=v.Steer=0;v.WorkTicks=0;v.Loop=false;v.PickupTask=false;
                    Travel(v,Factory(v.TargetFactory)?.Dock??Factory(1).Dock,2);
                }
                if(State.Hardware.FirstProductTick==0&&(FinishedStock(1).Any(g=>g.Material==HardwareCatalog.Standard||g.Material==HardwareCatalog.Quiet)||State.Hardware.Settlements.Count>0))
                    State.Hardware.FirstProductTick=Math.Max(1,State.Tick);
            }
            AssertConserved();
        }
        private WorldState NewState()
        {
            var s=new WorldState{Money=Settings.StartingMoney};
            if(Settings.IsHardware){s.Version=2;s.Hardware=new HardwareState{ContentVersion=Settings.Hardware.Version,SpotRemaining=Settings.Hardware.SpotCapacity};s.Unlocked=3;}
            s.Factories.Add(new FactorySite{Id=1,X=4,Z=4,Owned=true,Discovered=true});
            s.Factories.Add(new FactorySite{Id=2,X=70,Z=48});
            s.Resources.Add(new ResourceSite{Id=1,Material=8,X=10,Z=39,Discovered=true});
            s.Resources.Add(new ResourceSite{Id=2,Material=9,X=32,Z=62});
            s.Resources.Add(new ResourceSite{Id=3,Material=10,X=65,Z=21});
            s.Vehicles.Add(new WorldVehicle{Id=1,Role=VehicleRole.Pickup,Position=s.Factories[0].Dock});
            if(!Settings.IsHardware)foreach(var material in new[]{8,9,10})for(var i=0;i<20;i++){s.Factories[0].Starter.Add(new Goods(s.NextItem++,1,material));s.Created++;}
            return s;
        }
        private void Index()
        {
            nodeIndex.Clear();outgoing.Clear();foreach(var n in State.Nodes){nodeIndex.Add(n.Id,n);outgoing.Add(n.Id,new List<BusinessRoute>());}
            foreach(var r in State.Routes)outgoing[r.From].Add(r);
        }
        public BusinessNode Node(int id)=>nodeIndex.TryGetValue(id,out var n)?n:null;
        public FactorySite Factory(int id)=>State.Factories.FirstOrDefault(f=>f.Id==id);
        public int Capacity(BusinessNode n)=>n.Kind==BuildingKind.Warehouse?Settings.WarehouseCapacity*n.Level:n.Kind==BuildingKind.Sales?32:8*n.Level;
        public int OutputCapacity(BusinessNode n)=>n.Kind==BuildingKind.Warehouse?Capacity(n):4*n.Level;
        public int Count(BusinessNode n,int material)=>n.Input.Count(i=>i.Material==material)+n.Output.Count(i=>i.Material==material);
        private Goods Create(int origin,int material){State.Created++;return new Goods(State.NextItem++,origin,material);}
        public void Enqueue(WorldCommand command)=>commands.Enqueue(command);
        // Executes queued commands at a clock boundary, also used by paused building UI.
        public void FlushCommands(){while(commands.Count>0){LastResult=Execute(commands.Dequeue());Log(LastResult.Message);}}
        public void Tick()
        {
            FlushCommands();State.Tick++;
            if(Settings.IsHardware)TickHardware();
            foreach(var resource in State.Resources)if(State.Tick%Settings.RegenerationTicks==0&&resource.Stock<resource.Capacity)resource.Stock++;
            foreach(var n in State.Nodes.ToArray())Process(n);
            foreach(var r in State.Routes)Transport(r);
            foreach(var n in State.Nodes)Send(n);
            foreach(var v in State.Vehicles)TickVehicle(v);
            if(logisticsDirty){logisticsDirty=false;CommitNetwork(State.Nodes,new List<BusinessRoute>(),null,true);Revision++;}
            if(State.Tick%600==0)AssertConserved();
        }
        private void Process(BusinessNode n)
        {
            if(n.Kind==BuildingKind.Sales)
            {
                if(Settings.IsHardware){n.Status="订单交付 / 有限现货回收";return;}
                n.Status="等待商品";
                if(n.Input.Count==0)return;
                var item=n.Input[0];var price=Settings.Recipes.FirstOrDefault(r=>r.Output==item.Material)?.Price??0;
                if(price<=0)return;
                n.Input.RemoveAt(0);State.Money+=price;State.Revenue+=price;State.Sold++;State.Transactions++;
                var f=Factory(n.Factory);f.SalesCount++;n.Status="销售 +"+price;
                var level=item.Material==17?2:item.Material==18?3:4;
                if(level>State.Unlocked){State.Unlocked=level;Log(level==2?"T2 与采集团队已解锁":level==3?"T3 与运输队已解锁":"第二工厂购买资格已解锁");}
                if(n.Factory==2&&!State.Complete){State.Complete=true;Log("目标达成：第二工厂完成首次销售！可继续自由经营。");}
                return;
            }
            if(n.Kind==BuildingKind.Warehouse){n.Status="仓储 "+n.Output.Count+"/"+Capacity(n);return;}
            var recipe=Settings.Recipe(n.RecipeId);if(recipe==null){n.Status="请选择配方";return;}
            if(n.Batch.Count>0)
            {
                if(n.WorkRemaining>0)n.WorkRemaining--;
                n.Status="加工中";
                if(n.WorkRemaining>0)return;
                if(n.Output.Count>=OutputCapacity(n)){n.Status="输出堵塞";return;}
                var product=Create(n.Factory,recipe.Output);
                if(Settings.IsHardware){product.MaterialCost=n.Batch.Sum(g=>g.MaterialCost);product.ProcessingCost=n.Batch.Sum(g=>g.ProcessingCost)+n.BatchFee;product.FreightCost=n.Batch.Sum(g=>g.FreightCost);product.Gift=n.Batch.All(g=>g.Gift);product.SpecId=recipe.SpecId;product.SpecVersion=recipe.SpecVersion;}
                State.Consumed+=n.Batch.Count;n.Batch.Clear();n.BatchFee=0;n.Output.Add(product);
                if(Settings.IsHardware&&State.Hardware.FirstProductTick==0&&(product.Material==HardwareCatalog.Standard||product.Material==HardwareCatalog.Quiet))
                {
                    State.Hardware.FirstProductTick=State.Tick;Log("首款产品下线！星铸 S1 已完成装配、检测和包装；入库后可在订单页预留交付。");
                }
            }
            if(n.PendingRemove||!string.IsNullOrEmpty(n.PendingRecipe))
            {
                var c=new WorldCommand{Kind=n.PendingRemove?CommandKind.Remove:CommandKind.Recipe,Node=n.Id,Recipe=n.PendingRecipe};
                var result=Edit(n,c);if(!result.Success)n.Status=result.Message;return;
            }
            if(n.Output.Count>=OutputCapacity(n)){n.Status="输出堵塞";return;}
            foreach(var input in recipe.Inputs)
                if(n.Input.Count(i=>i.Material==input.Material&&i.ReservedOrder==0)<input.Quantity){n.Status="缺少 "+EconomySettings.MaterialName(input.Material);return;}
            if(Settings.IsHardware&&!Pay(recipe.ProcessingFee)){n.Status="加工资金不足";return;}
            n.BatchFee=Settings.IsHardware?recipe.ProcessingFee:0;
            foreach(var input in recipe.Inputs)for(var i=0;i<input.Quantity;i++){var item=n.Input.First(g=>g.Material==input.Material&&g.ReservedOrder==0);n.Input.Remove(item);n.Batch.Add(item);}
            n.WorkTotal=n.WorkRemaining=Math.Max(1,recipe.Seconds*60*100/Settings.UpgradePercent[n.Level-1]);n.Status="加工中";
        }
        private bool CanReceive(BusinessNode n,int material)
        {
            if(n.PendingRemove||!string.IsNullOrEmpty(n.PendingRecipe))return false;
            if(n.Kind==BuildingKind.Warehouse)return n.Output.Count<Capacity(n)&&n.Output.Count(i=>i.Material==material)<Capacity(n)/4;
            if(n.Kind==BuildingKind.Sales)return n.Input.Count<Capacity(n);
            return n.Input.Count(i=>i.Material==material)<Capacity(n);
        }
        private void Transport(BusinessRoute r)
        {
            r.Backend.TransportTick(State.Tick);var target=Node(r.To);
            if(CanReceive(target,r.Material))
            {
                r.Backend.QueryCandidates(new BeltAccessPort(new LaneId(0),r.Length-512,r.Length),candidates);
                if(candidates.Count>0&&r.Backend.TryTake(candidates[0].Handle,out var moved))
                {
                    var item=r.Cargo.First(g=>g.Id==moved.Handle.ItemId.Value);r.Cargo.Remove(item);
                    (target.Kind==BuildingKind.Warehouse?target.Output:target.Input).Add(item);
                }
            }
        }
        private void Send(BusinessNode n)
        {
            if(State.Tick<n.NextSend||n.Output.Count==0||!outgoing.TryGetValue(n.Id,out var choices)||choices.Count==0)return;
            // Fair round-robin inside the highest available consumer priority.
            var priority=int.MinValue;BusinessRoute selected=null;Goods goods=null;var selectedIndex=0;
            for(var k=0;k<choices.Count;k++)
            {
                var index=(n.Cursor+k)%choices.Count;var r=choices[index];var target=Node(r.To);if(!CanReceive(target,r.Material))continue;
                var item=n.Output.FirstOrDefault(g=>g.Material==r.Material&&g.ReservedOrder==0);if(item==null)continue;
                var score=target.Policy(r.Material)?.Priority??0;
                if(target.Kind==BuildingKind.Sales)
                {
                    var reserve=n.Policy(r.Material)?.Reserve??0;
                    if(n.Output.Count(g=>g.Material==r.Material)<=reserve)continue;
                    score-=10000;
                }
                if(score<=priority)continue;
                // Occupied inlets should not prevent another consumer from receiving.
                r.Backend.QueryCandidates(new BeltAccessPort(new LaneId(0),0,511),candidates);if(candidates.Count>0)continue;
                priority=score;selected=r;goods=item;selectedIndex=index;
            }
            if(selected==null)return;
            if(!selected.Backend.TryInsertExisting(new BeltAccessPort(new LaneId(0),0,0),new ItemId(goods.Id),new PartTypeId(goods.Material),out _))return;
            n.Output.Remove(goods);selected.Cargo.Add(goods);n.Cursor=(selectedIndex+1)%choices.Count;n.NextSend=State.Tick+Math.Max(3,12/n.Level);
        }
        public void CaptureState(){FlushCommands();foreach(var r in State.Routes)r.Saved=r.Backend.ExportRuntimeState();AssertConserved();}
        public void AssertConserved()
        {
            var all=State.Nodes.SelectMany(n=>n.Input.Concat(n.Output).Concat(n.Batch)).Concat(State.Routes.SelectMany(r=>r.Cargo)).Concat(State.Vehicles.SelectMany(v=>v.Cargo)).Concat(State.Factories.SelectMany(f=>f.Starter)).Concat(State.Hardware==null?Enumerable.Empty<Goods>():State.Hardware.Deliveries.SelectMany(d=>d.Cargo)).ToList();
            if(all.Select(i=>i.Id).Distinct().Count()!=all.Count||State.Created!=all.Count+State.Consumed+State.Sold||State.Money<0)throw new InvalidOperationException("EP04 物料/资金守恒失败");
            if(Settings.IsHardware)ValidateHardware(all);
            foreach(var r in State.Routes)
            {
                var items=r.Backend.ExportRuntimeState().Items;
                if(!items.Select(i=>i.Id).OrderBy(i=>i).SequenceEqual(r.Cargo.Select(i=>i.Id).OrderBy(i=>i)))throw new InvalidOperationException("运输归属不一致");
            }
        }
        private void Log(string message){State.Events.Add(message);if(State.Events.Count>40)State.Events.RemoveAt(0);}
        private CommandResult Ok(string message,int entity=0)=>new CommandResult(true,message,entity);
        private CommandResult Fail(string message)=>new CommandResult(false,message);
        private bool Pay(int amount){if(amount<0||State.Money<amount)return false;State.Money-=amount;State.Expenses+=amount;if(Settings.IsHardware&&amount>0)Cash(-amount,"支出");return true;}
    }
}
