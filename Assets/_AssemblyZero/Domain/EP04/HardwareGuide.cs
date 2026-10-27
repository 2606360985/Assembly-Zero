using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed class HardwareHint
    {
        public string Key,Title,Detail,Action;
        public int Order,Node,Material,Quantity;
        public BuildingKind Building;
    }

    // Derived from real inventory and machine state, so loading or rebuilding cannot skip a step.
    public static class HardwareGuide
    {
        private static readonly BuildingKind[] Chain={BuildingKind.Warehouse,BuildingKind.BoardAssembly,BuildingKind.GPUAssembly,BuildingKind.Tester,BuildingKind.Packaging,BuildingKind.Sales};
        public static HardwareOrder FocusOrder(WorldSimulation w)
        {
            var h=w.State.Hardware;bool Open(HardwareOrder o)=>o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready;
            var scripted=h.ScriptedDone+1;
            return h.Orders.FirstOrDefault(o=>o.Template==scripted&&Open(o))??h.Orders.FirstOrDefault(o=>o.Template==scripted&&o.Phase==HardwareOrderPhase.Available)
                ??h.Orders.Where(Open).OrderBy(o=>o.Deadline).FirstOrDefault();
        }
        public static HardwareHint Evaluate(WorldSimulation w,int factory=1)
        {
            var h=w.State.Hardware;var first=h.ScriptedDone==0;
            var order=FocusOrder(w);
            if(order==null)
            {
                var bottleneck=w.Bottleneck(factory);
                if(bottleneck!=null)return BottleneckHint(w,bottleneck,0);
                return Hint("free","自由经营 · 订单板",w.ForecastEvent!=null?"行情预告：「"+w.ForecastEvent.Name+"」即将到来。"+w.ForecastEvent.Detail+"。提前囤料或转产。":"在订单板挑选客户：提前交付 +15% 并涨口碑，超期扣违约金。口碑越高，订单越大。","查看订单");
            }
            if(order.Phase==HardwareOrderPhase.Available)
                return new HardwareHint{Key="order",Title=first?"1 / 6 · 接下首单":"下一单 · "+order.Name,Detail=first?"晨湾工作室要 3 张标准版，"+order.Seconds+" 秒内交付；前 60% 时间内交付有 15% 奖金。":"首单已交付！星河网吧要 8 张标准版。散热器只剩 1 个，需要补货。",Action="前往接单",Order=order.Id};
            factory=order.Factory;
            var nodes=w.State.Nodes.Where(n=>n.Factory==factory).ToArray();
            for(var i=0;i<Chain.Length;i++)
            {
                var kind=Chain[i];if(nodes.Any(n=>n.Kind==kind&&!n.PendingRemove))continue;
                var name=w.Settings.Hardware.Buildings.First(b=>b.Kind==kind).Name;
                return new HardwareHint{Key="build",Title=(first?"2 / 6 · ":"")+"放置"+name,Detail="产线缺少"+name+"。在地面青色推荐位放下即可，输送线会自动连接。板卡装配 → 显卡总装 → 检测 → 包装 → 入库。",Action="放置"+name,Building=kind,Order=order.Id};
            }
            var quiet=order.Material==HardwareCatalog.Quiet;var budget=order.Material==HardwareCatalog.Budget;
            if(!budget)foreach(var kind in new[]{BuildingKind.GPUAssembly,BuildingKind.Tester,BuildingKind.Packaging})
            {
                var id=(quiet?"quiet-":"")+(kind==BuildingKind.GPUAssembly?"gpu":kind==BuildingKind.Tester?"test":"pack");
                if(!nodes.Any(n=>n.Kind==kind&&(n.RecipeId==id||n.PendingRecipe==id)))return new HardwareHint{Key="recipe",Title="切换产品配方",Detail="总装、检测和包装需使用同一规格。当前订单要求"+(quiet?"静音版":"标准版")+"；在产品页一键切换，等待旧批次完成。",Action="查看产品",Order=order.Id};
            }
            if(budget&&!nodes.Any(n=>n.Kind==BuildingKind.Packaging&&(n.RecipeId=="budget-pack"||n.PendingRecipe=="budget-pack")))
                return new HardwareHint{Key="recipe",Title="丐版需要专门包装",Detail="把一台包装机切换到“丐版包装”：次品 + 包装材料 → S1 丐版。建议另建一台包装机。",Action="查看产品",Order=order.Id};
            var stock=Inventory(w,factory,order);
            if(!budget)
            {
                var heat=quiet?HardwareCatalog.QuietHeat:HardwareCatalog.Heat;
                var needs=new[]{heat,HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board,HardwareCatalog.Shell,HardwareCatalog.Carton};
                foreach(var m in needs)
                {
                    var missing=order.Quantity-Supplied(stock,m,order);if(missing<=0)continue;
                    if(m==heat&&nodes.Any(n=>n.Kind==BuildingKind.Manufacturer&&n.RecipeId==(quiet?"quiet-heat":"heat")))
                    {
                        if(stock.Count(g=>g.Material==HardwareCatalog.Aluminum)>=(quiet?3:2))continue;
                        if(!nodes.Any(n=>n.Kind==BuildingKind.Processor&&n.RecipeId=="aluminum"))
                            return new HardwareHint{Key="supply",Title="为散热器加工机补料",Detail="散热器需要铝材。可采购铝材自动配送；长期生产也可建设铝材加工机，再规划皮卡循环采矿。",Action="查看铝材采购",Material=HardwareCatalog.Aluminum,Quantity=Math.Min(80,missing*(quiet?3:2)),Order=order.Id};
                        if(!stock.Any(g=>g.Material==HardwareCatalog.Ore))
                            return new HardwareHint{Key="mining",Title="规划上游采集",Detail="铝材加工机需要铝原料。运输页规划循环采集，皮卡自动采满返厂卸货。",Action="前往规划采矿",Order=order.Id};
                        continue;
                    }
                    return new HardwareHint{Key="supply",Title="补齐"+HardwareCatalog.Name(m),Detail="还差 "+missing+" 个"+HardwareCatalog.Name(m)+"。常规配送 "+w.Settings.Hardware.DeliverySeconds+" 秒到货；"+(m==heat?"也可自建散热器加工机，长期更便宜。":"留意行情，涨价前囤货更划算。"),Action="查看缺料采购",Material=m,Quantity=missing,Order=order.Id};
                }
            }
            var pickup=h.Deliveries.FirstOrDefault(d=>d.Factory==factory&&d.Method==2);
            if(pickup!=null)return new HardwareHint{Key="pickup",Title="采购物资等待自动自提",Detail="运输页派遣皮卡，自动前往采购站、装载并返厂卸货；大批货物分次取回。",Action="查看运输任务",Order=order.Id};
            var delivery=h.Deliveries.FirstOrDefault(d=>d.Factory==factory&&d.Method!=2);
            if(delivery!=null&&!nodes.Any(n=>n.Batch.Count>0))return new HardwareHint{Key="arrival",Title="等待补料入库",Detail="到货约剩 "+Math.Max(0,(delivery.DueTick-w.State.Tick)/60)+" 秒。到货后产线自动继续。",Action="查看采购",Order=order.Id};
            var bn=w.Bottleneck(factory);
            if(!first&&bn!=null&&nodes.Count(n=>n.Kind==bn.Kind)<2)return BottleneckHint(w,bn,order.Id);
            var reserved=w.ReservedCount(order.Id);var made=h.FirstProductTick>0;
            var left=Math.Max(0,(order.Deadline-w.State.Tick)/60);var bonus=Math.Max(0,(order.BonusDeadline-w.State.Tick)/60);
            var timing=bonus>0?"奖金期剩 "+bonus+" 秒":left>0?"交期剩 "+left+" 秒":"已超期，尽快交付";
            var stage=nodes.Where(n=>n.Batch.Count>0).OrderByDescending(n=>n.Kind==BuildingKind.Packaging?4:n.Kind==BuildingKind.Tester?3:n.Kind==BuildingKind.GPUAssembly?2:1).FirstOrDefault();
            if(stage!=null)return new HardwareHint{Key="produce",Title=first?(made?"4 / 6 · 首款产品已下线":"3 / 6 · 生产第一张显卡"):"生产中 · "+reserved+" / "+order.Quantity,Detail=(made?"已分配 "+reserved+" / "+order.Quantity+" 张，配齐后自动交付。":"板卡装配 → 显卡总装 → 检测 → 包装 → 入库。")+"\n"+timing+"。顶部可切换 4× / 8× 加速。",Action="定位当前工序",Node=stage.Id,Order=order.Id};
            var blocked=nodes.FirstOrDefault(n=>n.RunState==NodeState.Blocked||n.RunState==NodeState.NoFunds||n.RunState==NodeState.Disconnected)??nodes.FirstOrDefault(n=>n.RunState==NodeState.Starved);
            if(blocked!=null)return new HardwareHint{Key="blocked",Title="检查产线等待原因",Detail=w.Settings.Hardware.Buildings.First(b=>b.Kind==blocked.Kind).Name+"："+blocked.Status+"。红灯=堵塞，黄灯=缺料，灰灯=未连通。",Action="查看等待设备",Node=blocked.Id,Order=order.Id};
            return Hint("produce",first?"5 / 6 · 自动交付":"自动交付中","合格成品入库后自动分配给订单，配齐即发货。"+timing+"。","查看订单");
        }
        private static HardwareHint BottleneckHint(WorldSimulation w,BusinessNode n,int order)
        {
            var name=w.Settings.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name;
            return new HardwareHint{Key="bottleneck",Title="瓶颈：「"+name+"」满负荷 "+w.Utilization(n)+"%",Detail="下游设备在等料。再放一台"+name+"，输送线自动并联，产量立刻提升；也可升级设备提速。",Action="再建一台"+name,Building=n.Kind,Node=n.Id,Order=order};
        }
        private static List<Goods> Inventory(WorldSimulation w,int factory,HardwareOrder order)
        {
            var h=w.State.Hardware;
            return w.State.Nodes.Where(n=>n.Factory==factory).SelectMany(n=>n.Input.Concat(n.Output).Concat(n.Batch))
                .Concat(w.State.Routes.Where(r=>r.Factory==factory).SelectMany(r=>r.Cargo))
                .Concat(w.State.Vehicles.Where(v=>v.TargetFactory==factory).SelectMany(v=>v.Cargo))
                .Concat(h.Deliveries.Where(d=>d.Factory==factory).SelectMany(d=>d.Cargo))
                .Where(g=>g.ReservedOrder==0||g.ReservedOrder==order.Id).ToList();
        }
        // Each input exists once, either as itself or embodied in a downstream product of the order's specification.
        private static int Supplied(List<Goods> stock,int material,HardwareOrder order)
        {
            var quiet=order.Material==HardwareCatalog.Quiet;
            var downstream=new List<int>{material};
            if(material!=HardwareCatalog.Carton)downstream.AddRange(new[]{quiet?HardwareCatalog.QuietRaw:HardwareCatalog.RawGPU,quiet?HardwareCatalog.QuietTested:HardwareCatalog.Tested});
            if(material==HardwareCatalog.Core||material==HardwareCatalog.Memory||material==HardwareCatalog.Board)downstream.Add(HardwareCatalog.AssembledBoard);
            downstream.Add(order.Material);
            return stock.Count(g=>downstream.Contains(g.Material)&&(g.Material!=order.Material||g.SpecId==order.Spec));
        }
        private static HardwareHint Hint(string key,string title,string detail,string action)=>new HardwareHint{Key=key,Title=title,Detail=detail,Action=action};
    }
}
