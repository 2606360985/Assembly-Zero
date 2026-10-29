using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        public const int AllocationInterval=30, EarlyPercent=115, LatePercent=75, ExpirePenaltyPercent=10, CancelPenaltyPercent=5;
        public const int EarlyReputation=5, OnTimeReputation=3, LateReputation=-6, ExpireReputation=-12, CancelReputation=-4;
        private void InitializeHardware()
        {
            State.Resources.RemoveAll(r=>r.Id!=1);State.Resources[0].Material=HardwareCatalog.Ore;State.Resources[0].X=Settings.Hardware.OreX;State.Resources[0].Z=Settings.Hardware.OreZ;
            State.Hardware.LayoutVersion=Settings.Hardware.LayoutVersion;
            State.Factories[0].FreeWarehouse=false;State.Factories[0].FreeSales=false;
            foreach(var m in Settings.Hardware.Materials.Where(m=>m.Price>0))State.Hardware.Stock.Add(new SupplierStock{Material=m.Id,Quantity=m.Stock});
            State.Hardware.Orders.Add(NewOrder(Settings.Hardware.Orders[0],Settings.Hardware.Orders[0].Id));
            // An intentionally incomplete line: the player places assembly, inspection and packaging.
            var kinds=new[]{BuildingKind.Warehouse,BuildingKind.BoardAssembly,BuildingKind.Sales};
            var xs=new[]{4,12,28};var zs=new[]{16,8,24};
            for(var i=0;i<kinds.Length;i++)State.Nodes.Add(new BusinessNode{Id=State.NextNode++,Factory=1,Kind=kinds[i],X=xs[i],Z=zs[i],RecipeId=Settings.Hardware.Buildings.First(b=>b.Kind==kinds[i]).Recipe});
            var warehouse=State.Nodes[0];
            foreach(var m in new[]{HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board,HardwareCatalog.Shell,HardwareCatalog.Carton,HardwareCatalog.Heat})
                for(var i=0;i<(m==HardwareCatalog.Heat?4:12);i++){var g=Create(1,m);g.Gift=true;warehouse.Output.Add(g);}
            Index();var result=CommitNetwork(State.Nodes,new List<BusinessRoute>(),null,true);if(!result.Success)throw new InvalidOperationException(result.Message);
            Log("开局：仓储、板卡装配、交付站已就位；放置总装、检测、包装即可打通产线。赠送 12 套料和 4 个散热器。");
        }
        private HardwareOrder NewOrder(OrderTemplate t,int id)=>new HardwareOrder{Id=id,Template=t.Id,Material=t.Material,Quantity=t.Quantity,UnitPrice=t.UnitPrice,Name=t.Name,Spec=t.Spec,Seconds=t.Seconds};
        public IEnumerable<Goods> FinishedStock(int factory)=>State.Nodes.Where(n=>n.Factory==factory&&n.Kind==BuildingKind.Warehouse).SelectMany(n=>n.Output);
        public int ReservedCount(int order)=>State.Nodes.SelectMany(n=>n.Output).Count(g=>g.ReservedOrder==order);
        public long RecentCash=>State.Hardware?.Cash.Where(c=>c.Tick>State.Tick-18000).Sum(c=>c.Amount)??0;
        public long ReplacementCost(int material)
        {
            var m=Settings.Hardware.Materials.First(x=>x.Id==material);if(m.Price>0)return PurchasePrice(material);
            var r=Settings.Recipes.FirstOrDefault(x=>x.Output==material);return r==null?0:r.ProcessingFee+r.Inputs.Sum(i=>ReplacementCost(i.Material)*i.Quantity);
        }
        public int ActiveOrderCount=>State.Hardware.Orders.Count(o=>o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready);
        public long OrderValue(HardwareOrder o)=>(long)o.Quantity*o.UnitPrice;
        private void Cash(long amount,string category){State.Hardware.Cash.Add(new CashEntry{Tick=State.Tick,Amount=amount,Category=category});}
        private void Earn(int amount,string category){State.Money=checked(State.Money+amount);State.Revenue+=amount;Cash(amount,category);}
        // Penalties never push cash below zero; the shortfall is forgiven to keep money non-negative.
        private long Charge(long amount,string category)
        {
            var paid=Math.Min(Math.Max(0,amount),State.Money);if(paid==0)return 0;
            State.Money-=(int)paid;State.Expenses+=paid;State.Hardware.Penalties+=(int)paid;Cash(-paid,category);return paid;
        }
        private void AdjustReputation(int delta){State.Hardware.Reputation=Math.Max(0,Math.Min(100,State.Hardware.Reputation+delta));}
        public const int StatsInterval=3600, StatsCapacity=120;
        private void RecordStats()
        {
            var h=State.Hardware;if(h.Stats==null)h.Stats=new List<StatsBucket>();
            var core=Settings.Hardware.Materials.First(m=>m.Id==HardwareCatalog.Core).Price;
            h.Stats.Add(new StatsBucket{Tick=State.Tick,Revenue=State.Revenue,Expenses=State.Expenses,Penalties=h.Penalties,Cards=h.Settlements.Sum(s=>s.Quantity),CorePricePercent=core>0?PurchasePrice(HardwareCatalog.Core)*100/core:100,Reputation=h.Reputation});
            if(h.Stats.Count>StatsCapacity)h.Stats.RemoveRange(0,h.Stats.Count-StatsCapacity);
        }
        private void TickHardware()
        {
            var h=State.Hardware;
            if(State.Tick%(Settings.Hardware.RestockSeconds*60)==0)foreach(var stock in h.Stock)
            {
                var full=Settings.Hardware.Materials.First(m=>m.Id==stock.Material).Stock;
                stock.Quantity=Math.Max(stock.Quantity,stock.Material==HardwareCatalog.Core?full*(CurrentEvent?.CoreSupplyPercent??100)/100:full);
            }
            if(State.Tick%18000==0)h.SpotRemaining=Settings.Hardware.SpotCapacity;
            h.Cash.RemoveAll(c=>c.Tick<=State.Tick-18000);
            if(State.Tick%StatsInterval==0)RecordStats();
            TickMarket();
            foreach(var order in h.Orders.Where(o=>o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready).ToArray())
                if(State.Tick>order.HardDeadline)Expire(order);
            if(State.Tick%AllocationInterval==0)Allocate();
            TickOffers();
            foreach(var d in h.Deliveries.Where(d=>d.Method!=2&&State.Tick>=d.DueTick).ToArray())
            {
                foreach(var g in d.Cargo.ToArray()){var store=State.Nodes.FirstOrDefault(n=>n.Factory==d.Factory&&n.Kind==BuildingKind.Warehouse&&CanReceive(n,g.Material));if(store==null)continue;store.Output.Add(g);d.Cargo.Remove(g);}
                if(d.Cargo.Count==0){h.Deliveries.Remove(d);Log("采购 #"+d.Id+" 已入库");}
            }
        }
        private void Expire(HardwareOrder order)
        {
            ReleaseReservation(order.Id);order.Phase=HardwareOrderPhase.Expired;
            var paid=Charge(OrderValue(order)*ExpirePenaltyPercent/100,"违约金");AdjustReputation(ExpireReputation);
            Log("订单作废："+order.Name+"；违约金 ¥"+(paid/100m)+"，口碑 "+ExpireReputation);
            OfferRetry(order);
        }
        // Finished goods are allocated to the earliest deadline first, then shipped automatically once complete.
        private void Allocate()
        {
            foreach(var order in State.Hardware.Orders.Where(o=>o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready).OrderBy(o=>o.Deadline).ThenBy(o=>o.Id).ToArray())
            {
                Reserve(order);
                if(order.Phase==HardwareOrderPhase.Ready&&State.Nodes.Any(n=>n.Factory==order.Factory&&n.Kind==BuildingKind.Sales))Deliver(order);
            }
        }
        private void Reserve(HardwareOrder order)
        {
            var count=ReservedCount(order.Id);
            foreach(var g in FinishedStock(order.Factory).Where(g=>g.ReservedOrder==0&&Matches(g,order)).OrderBy(g=>g.Id).Take(Math.Max(0,order.Quantity-count)))g.ReservedOrder=order.Id;
            order.Phase=ReservedCount(order.Id)>=order.Quantity?HardwareOrderPhase.Ready:HardwareOrderPhase.Active;
        }
        public DeliveryGrade GradeAt(HardwareOrder o,long tick)=>tick<=o.BonusDeadline?DeliveryGrade.Early:tick<=o.Deadline?DeliveryGrade.OnTime:DeliveryGrade.Late;
        private CommandResult Deliver(HardwareOrder order)
        {
            if(!State.Nodes.Any(n=>n.Factory==order.Factory&&n.Kind==BuildingKind.Sales))return Fail("需要交付站");
            var goods=FinishedStock(order.Factory).Where(g=>g.ReservedOrder==order.Id&&Matches(g,order)).ToArray();if(goods.Length!=order.Quantity)return Fail("合格成品尚未配齐");
            var grade=GradeAt(order,State.Tick);var percent=grade==DeliveryGrade.Early?EarlyPercent:grade==DeliveryGrade.OnTime?100:LatePercent;
            var income=OrderValue(order)*percent/100;var delta=grade==DeliveryGrade.Early?EarlyReputation:grade==DeliveryGrade.OnTime?OnTimeReputation:LateReputation;
            if(income>int.MaxValue-State.Money)return Fail("资金超过当前账户上限");
            var settlement=new Settlement{Order=order.Id,Name=order.Name,Quantity=goods.Length,Tick=State.Tick,Income=income,Adjustment=income-OrderValue(order),Grade=grade,ReputationDelta=delta,MaterialCost=goods.Sum(g=>g.MaterialCost),ProcessingCost=goods.Sum(g=>g.ProcessingCost),FreightCost=goods.Sum(g=>g.FreightCost)};
            foreach(var n in State.Nodes.Where(n=>n.Factory==order.Factory&&n.Kind==BuildingKind.Warehouse))n.Output.RemoveAll(g=>g.ReservedOrder==order.Id);
            State.Sold+=goods.Length;State.Transactions++;Earn((int)income,"订单收入");State.Hardware.Settlements.Add(settlement);order.Phase=HardwareOrderPhase.Delivered;AdjustReputation(delta);
            if(order.Template>State.Hardware.ScriptedDone)State.Hardware.ScriptedDone=order.Template;
            if(order.Template==1){State.Unlocked=4;Factory(2).Discovered=true;State.Hardware.Orders.Add(NewOrder(Settings.Hardware.Orders[1],State.Hardware.NextOrder++));Log("订单板已开放；第二厂购买资格已解锁");}
            PruneOrders();
            var label=grade==DeliveryGrade.Early?"提前交付 +15%":grade==DeliveryGrade.OnTime?"准时交付":"迟交 -25%";
            Log("交付："+order.Name+" · "+label+"，收入 ¥"+(income/100m)+"，口碑 "+(delta>0?"+":"")+delta);
            return Ok("交付成功，"+label+"，收入 ¥"+(income/100m)+"，实际毛利 ¥"+(settlement.Margin/100m));
        }
        private void PruneOrders()
        {
            var closed=State.Hardware.Orders.Where(o=>o.Phase==HardwareOrderPhase.Delivered||o.Phase==HardwareOrderPhase.Expired||o.Phase==HardwareOrderPhase.Cancelled).ToList();
            foreach(var o in closed.Take(Math.Max(0,closed.Count-20)))State.Hardware.Orders.Remove(o);
        }
        private void ReleaseReservation(int id){foreach(var g in State.Nodes.SelectMany(n=>n.Output))if(g.ReservedOrder==id)g.ReservedOrder=0;}
        private CommandResult HardwareCommand(WorldCommand c)
        {
            switch(c.Kind)
            {
                case CommandKind.Purchase:return Purchase(c);
                case CommandKind.SpotSell:return SpotSell(c);
                case CommandKind.TakeControl:case CommandKind.ReleaseControl:case CommandKind.Drive:case CommandKind.Interact:case CommandKind.RecoverVehicle:case CommandKind.RightVehicle:return DrivingCommand(c);
            }
            var order=State.Hardware.Orders.FirstOrDefault(o=>o.Id==c.Order);if(order==null)return Fail("订单不存在");
            if(c.Kind==CommandKind.AcceptOrder)
            {
                if(order.Phase!=HardwareOrderPhase.Available)return Fail("订单已处理");
                if(order.OfferExpires>0&&State.Tick>order.OfferExpires)return Fail("报价已过期");
                if(State.Hardware.Reputation<order.MinReputation)return Fail("口碑不足：需要 "+order.MinReputation+"（"+HardwareCatalog.ReputationTier(order.MinReputation)+"）");
                if(ActiveOrderCount>=Settings.Hardware.MaxActiveOrders)return Fail("同时进行的订单已达上限 "+Settings.Hardware.MaxActiveOrders);
                if(Factory(c.Factory)?.Owned!=true)return Fail("厂址未购买");
                var ticks=(long)order.Seconds*60;
                order.Factory=c.Factory;order.Deadline=State.Tick+ticks;order.BonusDeadline=State.Tick+ticks*60/100;order.HardDeadline=State.Tick+ticks*3/2;order.Phase=HardwareOrderPhase.Active;
                return Ok("已接单；合格成品入库后自动分配并交付",order.Id);
            }
            if(order.Phase!=HardwareOrderPhase.Active&&order.Phase!=HardwareOrderPhase.Ready)return Fail("订单不处于可操作状态");
            if(c.Kind==CommandKind.ReleaseOrder){ReleaseReservation(order.Id);order.Phase=HardwareOrderPhase.Active;return Ok("预留已释放");}
            if(c.Kind==CommandKind.CancelOrder)
            {
                ReleaseReservation(order.Id);order.Phase=HardwareOrderPhase.Cancelled;var paid=Charge(OrderValue(order)*CancelPenaltyPercent/100,"违约金");AdjustReputation(CancelReputation);OfferRetry(order);
                return Ok("订单取消：违约金 ¥"+(paid/100m)+"，口碑 "+CancelReputation+"；库存保留");
            }
            if(c.Kind==CommandKind.ReserveOrder){Reserve(order);return Ok("已分配 "+ReservedCount(order.Id)+" / "+order.Quantity);}
            if(c.Kind==CommandKind.DeliverOrder){Reserve(order);return Deliver(order);}
            return Fail("未知订单操作");
        }
        private static bool Matches(Goods g,HardwareOrder o)=>g.Material==o.Material&&g.SpecId==o.Spec&&g.SpecVersion==o.Version;
        // Failed scripted contracts are quoted again so the opening progression cannot dead-end.
        private void OfferRetry(HardwareOrder failed)
        {
            if(failed.Template<=0)return;var template=Settings.Hardware.Orders.First(t=>t.Id==failed.Template);
            var orders=State.Hardware.Orders.Where(o=>o.Template==template.Id).ToArray();
            if(orders.Any(o=>o.Phase==HardwareOrderPhase.Delivered||o.Phase==HardwareOrderPhase.Available||o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready))return;
            State.Hardware.Orders.Add(NewOrder(template,State.Hardware.NextOrder++));
        }
        private CommandResult Purchase(WorldCommand c)
        {
            var h=Settings.Hardware;var stock=State.Hardware.Stock.FirstOrDefault(s=>s.Material==c.Material);
            if(stock==null||c.Quantity<=0||c.Quantity>80||c.Delivery<0||c.Delivery>2||Factory(c.Factory)?.Owned!=true)return Fail("无效采购参数");
            if(stock.Quantity<c.Quantity)return Fail("供应商库存不足，等待补货");
            var price=PurchasePrice(c.Material);var fee=c.Delivery==0?h.DeliveryFee:c.Delivery==1?h.ExpressFee:h.PickupFee;
            var cost=checked(price*c.Quantity+fee);if(!Pay(cost))return Fail("采购资金不足");
            stock.Quantity-=c.Quantity;var d=new PurchaseDelivery{Id=State.Hardware.NextPurchase++,Factory=c.Factory,Method=c.Delivery,DueTick=State.Tick+60*(c.Delivery==0?h.DeliverySeconds:c.Delivery==1?h.ExpressSeconds:0)};
            for(var i=0;i<c.Quantity;i++){var g=Create(0,c.Material);g.MaterialCost=price;g.FreightCost=fee/c.Quantity+(i<fee%c.Quantity?1:0);d.Cargo.Add(g);}
            State.Hardware.Deliveries.Add(d);return Ok(c.Delivery==2?"已支付；货物在采购站等待自提":"已采购；到货时间 "+(d.DueTick-State.Tick)/60+" 秒",d.Id);
        }
        private CommandResult SpotSell(WorldCommand c)
        {
            if(!State.Nodes.Any(n=>n.Factory==c.Factory&&n.Kind==BuildingKind.Sales))return Fail("需要交付站");
            if(c.Quantity<=0||c.Quantity>State.Hardware.SpotRemaining)return Fail("现货成交额度不足，每 5 分钟恢复");
            var price=SpotPrice(c.Material);if(price<=0)return Fail("现货市场只收成品、丐版、次品或铝原料");
            var goods=FinishedStock(c.Factory).Where(g=>g.Material==c.Material&&g.ReservedOrder==0).OrderBy(g=>g.Id).Take(c.Quantity).ToArray();if(goods.Length<c.Quantity)return Fail("仓库中未分配库存不足");
            if((long)State.Money+(long)price*c.Quantity>int.MaxValue)return Fail("账户上限");
            foreach(var n in State.Nodes.Where(n=>n.Factory==c.Factory&&n.Kind==BuildingKind.Warehouse))n.Output.RemoveAll(g=>goods.Contains(g));
            State.Sold+=goods.Length;State.Hardware.SpotRemaining-=goods.Length;Earn(price*goods.Length,"现货成交");return Ok("现货成交 "+goods.Length+" 件，收入 ¥"+(price*goods.Length/100m));
        }
        private void ValidateHardware(List<Goods> all)
        {
            if(all.Any(g=>g.MaterialCost<0||g.ProcessingCost<0||g.FreightCost<0))throw new InvalidOperationException("负库存成本");
            foreach(var g in all.Where(g=>g.ReservedOrder!=0))
            {
                var o=State.Hardware.Orders.FirstOrDefault(x=>x.Id==g.ReservedOrder);
                if(o==null||(o.Phase!=HardwareOrderPhase.Active&&o.Phase!=HardwareOrderPhase.Ready)||!Matches(g,o)||!FinishedStock(o.Factory).Contains(g))throw new InvalidOperationException("订单预留归属无效");
            }
            if(State.Hardware.Settlements.GroupBy(s=>s.Order).Any(g=>g.Count()>1)||State.Hardware.Stock.Any(s=>s.Quantity<0))throw new InvalidOperationException("重复结算或负供应库存");
            if(State.Hardware.Reputation<0||State.Hardware.Reputation>100)throw new InvalidOperationException("口碑越界");
        }
    }
}
