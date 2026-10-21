using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        private void InitializeHardware()
        {
            State.Resources.RemoveAll(r=>r.Id!=1);State.Resources[0].Material=HardwareCatalog.Ore;State.Resources[0].X=1;State.Resources[0].Z=27;
            State.Factories[0].FreeWarehouse=false;State.Factories[0].FreeSales=false;
            foreach(var m in Settings.Hardware.Materials.Where(m=>m.Price>0))State.Hardware.Stock.Add(new SupplierStock{Material=m.Id,Quantity=m.Stock});
            foreach(var t in Settings.Hardware.Orders)State.Hardware.Orders.Add(NewOrder(t,t.Id));
            var kinds=new[]{BuildingKind.Warehouse,BuildingKind.BoardAssembly,BuildingKind.GPUAssembly,BuildingKind.Tester,BuildingKind.Packaging,BuildingKind.Sales};
            var xs=new[]{4,12,20,28,34,28};var zs=new[]{16,8,8,8,16,24};
            for(var i=0;i<kinds.Length;i++)State.Nodes.Add(new BusinessNode{Id=State.NextNode++,Factory=1,Kind=kinds[i],X=xs[i],Z=zs[i],RecipeId=Settings.Hardware.Buildings.First(b=>b.Kind==kinds[i]).Recipe});
            var warehouse=State.Nodes[0];
            foreach(var m in new[]{HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board,HardwareCatalog.Shell,HardwareCatalog.Carton,HardwareCatalog.Heat})
                for(var i=0;i<(m==HardwareCatalog.Heat?2:8);i++){var g=Create(1,m);g.Gift=true;warehouse.Output.Add(g);}
            Index();var result=CommitNetwork(State.Nodes,new List<BusinessRoute>(),null,true);if(!result.Success)throw new InvalidOperationException(result.Message);
            Log("赠送：基础设施、8 套核心/显存/板材/外壳/包装和 2 个散热器。首单缺 6 个散热器，可自制或采购。");
        }
        private HardwareOrder NewOrder(OrderTemplate t,int id)=>new HardwareOrder{Id=id,Template=t.Id,Material=t.Material,Quantity=t.Quantity,UnitPrice=t.UnitPrice,Name=t.Name,Spec=t.Spec};
        public IEnumerable<Goods> FinishedStock(int factory)=>State.Nodes.Where(n=>n.Factory==factory&&n.Kind==BuildingKind.Warehouse).SelectMany(n=>n.Output);
        public int ReservedCount(int order)=>State.Nodes.SelectMany(n=>n.Output).Count(g=>g.ReservedOrder==order);
        public long RecentCash=>State.Hardware?.Cash.Where(c=>c.Tick>State.Tick-18000).Sum(c=>c.Amount)??0;
        public long ReplacementCost(int material)
        {
            var m=Settings.Hardware.Materials.First(x=>x.Id==material);if(m.Price>0)return m.Price;
            var r=Settings.Recipes.FirstOrDefault(x=>x.Output==material);return r==null?0:r.ProcessingFee+r.Inputs.Sum(i=>ReplacementCost(i.Material)*i.Quantity);
        }
        private void Cash(long amount,string category){State.Hardware.Cash.Add(new CashEntry{Tick=State.Tick,Amount=amount,Category=category});}
        private void Earn(int amount,string category){State.Money=checked(State.Money+amount);State.Revenue+=amount;Cash(amount,category);}
        private void TickHardware()
        {
            var h=State.Hardware;
            if(State.Tick%(Settings.Hardware.RestockSeconds*60)==0)foreach(var stock in h.Stock)stock.Quantity=Settings.Hardware.Materials.First(m=>m.Id==stock.Material).Stock;
            if(State.Tick%18000==0)h.SpotRemaining=Settings.Hardware.SpotCapacity;
            h.Cash.RemoveAll(c=>c.Tick<=State.Tick-18000);
            foreach(var order in h.Orders.Where(o=>o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready).ToArray())
            {
                if(State.Tick>order.Deadline){ReleaseReservation(order.Id);order.Phase=HardwareOrderPhase.Expired;OfferRecovery(Settings.Hardware.Orders.First(t=>t.Id==order.Template));Log("订单超期："+order.Name+"；库存已释放，可接维修社补货订单");}
                else order.Phase=ReservedCount(order.Id)>=order.Quantity?HardwareOrderPhase.Ready:HardwareOrderPhase.Active;
            }
            foreach(var d in h.Deliveries.Where(d=>d.Method!=2&&State.Tick>=d.DueTick).ToArray())
            {
                foreach(var g in d.Cargo.ToArray()){var store=State.Nodes.FirstOrDefault(n=>n.Factory==d.Factory&&n.Kind==BuildingKind.Warehouse&&CanReceive(n,g.Material));if(store==null)continue;store.Output.Add(g);d.Cargo.Remove(g);}
                if(d.Cargo.Count==0){h.Deliveries.Remove(d);Log("采购 #"+d.Id+" 已入库");}
            }
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
            var template=Settings.Hardware.Orders.First(t=>t.Id==order.Template);
            if(c.Kind==CommandKind.AcceptOrder)
            {
                if(order.Phase!=HardwareOrderPhase.Available)return Fail("订单已处理");
                if(order.Template==2&&!State.Hardware.Orders.Any(o=>o.Template==1&&o.Phase==HardwareOrderPhase.Delivered))return Fail("完成标准版首单后开放静音订单");
                if(Factory(c.Factory)?.Owned!=true)return Fail("厂址未购买");
                order.Factory=c.Factory;order.Deadline=State.Tick+template.Seconds*60;order.Phase=HardwareOrderPhase.Active;return Ok("已接单；请预留合格成品",order.Id);
            }
            if(order.Phase!=HardwareOrderPhase.Active&&order.Phase!=HardwareOrderPhase.Ready)return Fail("订单不处于可操作状态");
            if(State.Tick>order.Deadline)return Fail("已过交付期限");
            if(c.Kind==CommandKind.ReleaseOrder){ReleaseReservation(order.Id);order.Phase=HardwareOrderPhase.Active;return Ok("预留已释放");}
            if(c.Kind==CommandKind.CancelOrder){ReleaseReservation(order.Id);order.Phase=HardwareOrderPhase.Cancelled;OfferRecovery(template);return Ok("订单取消，无罚金；库存保留");}
            if(c.Kind==CommandKind.ReserveOrder)
            {
                var count=ReservedCount(order.Id);
                foreach(var g in FinishedStock(order.Factory).Where(g=>g.ReservedOrder==0&&Matches(g,order)).OrderBy(g=>g.Id).Take(Math.Max(0,order.Quantity-count)))g.ReservedOrder=order.Id;
                order.Phase=ReservedCount(order.Id)==order.Quantity?HardwareOrderPhase.Ready:HardwareOrderPhase.Active;return Ok("已预留 "+ReservedCount(order.Id)+" / "+order.Quantity);
            }
            if(c.Kind==CommandKind.DeliverOrder)
            {
                if(!State.Nodes.Any(n=>n.Factory==order.Factory&&n.Kind==BuildingKind.Sales))return Fail("需要交付站");
                var goods=FinishedStock(order.Factory).Where(g=>g.ReservedOrder==order.Id&&Matches(g,order)).ToArray();if(goods.Length!=order.Quantity)return Fail("预留合格成品不足，请先预留");
                var settlement=new Settlement{Order=order.Id,Quantity=goods.Length,Tick=State.Tick,Income=(long)order.Quantity*order.UnitPrice,MaterialCost=goods.Sum(g=>g.MaterialCost),ProcessingCost=goods.Sum(g=>g.ProcessingCost),FreightCost=goods.Sum(g=>g.FreightCost)};
                if(settlement.Income>int.MaxValue-State.Money)return Fail("资金超过当前账户上限");
                foreach(var n in State.Nodes.Where(n=>n.Factory==order.Factory&&n.Kind==BuildingKind.Warehouse))n.Output.RemoveAll(g=>g.ReservedOrder==order.Id);
                State.Sold+=goods.Length;State.Transactions++;Earn((int)settlement.Income,"订单收入");State.Hardware.Settlements.Add(settlement);order.Phase=HardwareOrderPhase.Delivered;
                if(order.Template==1){State.Unlocked=4;Factory(2).Discovered=true;Log("静音版订单与第二厂购买资格已开放");}
                OfferRecovery(template);return Ok("交付成功，收入 ¥"+(settlement.Income/100m)+"，实际毛利 ¥"+(settlement.Margin/100m));
            }
            return Fail("未知订单操作");
        }
        private static bool Matches(Goods g,HardwareOrder o)=>g.Material==o.Material&&g.SpecId==o.Spec&&g.SpecVersion==o.Version;
        private void OfferRecovery(OrderTemplate template)
        {
            var orders=State.Hardware.Orders.Where(o=>o.Template==template.Id).ToArray();
            // Failed introductory contracts may be quoted again so recovery can resume progression.
            var retry=orders.Any(o=>o.Phase==HardwareOrderPhase.Cancelled||o.Phase==HardwareOrderPhase.Expired)&&!orders.Any(o=>o.Phase==HardwareOrderPhase.Delivered);
            if((template.Repeatable||retry)&&!orders.Any(o=>o.Phase==HardwareOrderPhase.Available||o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready))State.Hardware.Orders.Add(NewOrder(template,State.Hardware.NextOrder++));
        }
        private CommandResult Purchase(WorldCommand c)
        {
            var h=Settings.Hardware;var stock=State.Hardware.Stock.FirstOrDefault(s=>s.Material==c.Material);
            if(stock==null||c.Quantity<=0||c.Quantity>80||c.Delivery<0||c.Delivery>2||Factory(c.Factory)?.Owned!=true)return Fail("无效采购参数");
            if(stock.Quantity<c.Quantity)return Fail("供应商库存不足，等待补货");
            var m=h.Materials.First(x=>x.Id==c.Material);var fee=c.Delivery==0?h.DeliveryFee:c.Delivery==1?h.ExpressFee:h.PickupFee;
            var cost=checked(m.Price*c.Quantity+fee);if(!Pay(cost))return Fail("采购资金不足");
            stock.Quantity-=c.Quantity;var d=new PurchaseDelivery{Id=State.Hardware.NextPurchase++,Factory=c.Factory,Method=c.Delivery,DueTick=State.Tick+60*(c.Delivery==0?h.DeliverySeconds:c.Delivery==1?h.ExpressSeconds:0)};
            for(var i=0;i<c.Quantity;i++){var g=Create(0,c.Material);g.MaterialCost=m.Price;g.FreightCost=fee/c.Quantity+(i<fee%c.Quantity?1:0);d.Cargo.Add(g);}
            State.Hardware.Deliveries.Add(d);return Ok(c.Delivery==2?"已支付；货物在采购站等待自提":"已采购；到货时间 "+(d.DueTick-State.Tick)/60+" 秒",d.Id);
        }
        private CommandResult SpotSell(WorldCommand c)
        {
            if(!State.Nodes.Any(n=>n.Factory==c.Factory&&n.Kind==BuildingKind.Sales))return Fail("需要交付站");
            if(c.Quantity<=0||c.Quantity>State.Hardware.SpotRemaining)return Fail("回收额度不足，每 5 分钟恢复");
            var raw=c.Material==HardwareCatalog.Ore;var m=Settings.Hardware.Materials.FirstOrDefault(x=>x.Id==c.Material);
            if(!raw&&(m==null||!m.Finished))return Fail("仅回收成品或铝原料");
            var goods=FinishedStock(c.Factory).Where(g=>g.Material==c.Material&&g.ReservedOrder==0).OrderBy(g=>g.Id).Take(c.Quantity).ToArray();if(goods.Length<c.Quantity)return Fail("未预留库存不足");
            var price=raw?100:c.Material==HardwareCatalog.Standard?35000:40000;
            if((long)State.Money+price*c.Quantity>int.MaxValue)return Fail("账户上限");
            foreach(var n in State.Nodes.Where(n=>n.Factory==c.Factory&&n.Kind==BuildingKind.Warehouse))n.Output.RemoveAll(g=>goods.Contains(g));
            State.Sold+=goods.Length;State.Hardware.SpotRemaining-=goods.Length;Earn(price*goods.Length,"现货回收");return Ok("已回收未预留库存");
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
        }
    }
}
