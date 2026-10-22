using System;
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
        public static HardwareHint Evaluate(WorldSimulation w,int factory=1)
        {
            var h=w.State.Hardware;
            var done=h.Orders.Any(o=>o.Template==1&&o.Phase==HardwareOrderPhase.Delivered);
            var order=h.Orders.FirstOrDefault(o=>o.Template==(done?2:1)&&(o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready))
                ??h.Orders.FirstOrDefault(o=>o.Template==(done?2:1)&&o.Phase==HardwareOrderPhase.Available);
            if(order==null)return Hint("free","合同已完成 · 自由经营","可接维修社补货单、扩建产线或规划自动采矿。","查看订单");
            if(order.Phase==HardwareOrderPhase.Available)
                return new HardwareHint{Key="order",Title=done?"下一款产品 · 静音版":"1 / 6 · 接下第一份订单",Detail=done?"标准版首单已交付。接下静庭订单，再切换静音配方并补齐原料。":"打开订单页，选择晨湾工作室并接单。赠送产线和物料已就绪；首单需要 8 张标准版。",Action="前往接单",Order=order.Id};
            factory=order.Factory;
            var nodes=w.State.Nodes.Where(n=>n.Factory==factory).ToArray();
            foreach(var kind in new[]{BuildingKind.Warehouse,BuildingKind.BoardAssembly,BuildingKind.GPUAssembly,BuildingKind.Tester,BuildingKind.Packaging,BuildingKind.Sales})
                if(!nodes.Any(n=>n.Kind==kind&&!n.PendingRemove))return new HardwareHint{Key="build",Title="补齐生产链",Detail="当前缺少"+w.Settings.Hardware.Buildings.First(b=>b.Kind==kind).Name+"。放置后输送线会自动连接，既有设备无需重建。",Action="放置所需设备",Building=kind,Order=order.Id};
            var quiet=order.Material==HardwareCatalog.Quiet;
            foreach(var kind in new[]{BuildingKind.GPUAssembly,BuildingKind.Tester,BuildingKind.Packaging})
            {
                var id=(quiet?"quiet-":"")+(kind==BuildingKind.GPUAssembly?"gpu":kind==BuildingKind.Tester?"test":"pack");
                if(!nodes.Any(n=>n.Kind==kind&&(n.RecipeId==id||n.PendingRecipe==id)))return new HardwareHint{Key="recipe",Title="检查产品配方",Detail="总装、检测和包装需使用同一规格。当前合同要求"+(quiet?"静音版":"标准版")+"；在产品页切换产线，等待旧批次完成。",Action="查看产品",Order=order.Id};
            }
            var reserved=w.ReservedCount(order.Id);
            var ready=w.FinishedStock(factory).Count(g=>g.Material==order.Material&&g.SpecId==order.Spec&&g.SpecVersion==order.Version&&(g.ReservedOrder==0||g.ReservedOrder==order.Id));
            if(reserved>=order.Quantity)return new HardwareHint{Key="deliver",Title="6 / 6 · 交付首批产品",Detail="已预留 "+reserved+" / "+order.Quantity+" 张。打开订单页点击整单交付，结算收入和毛利。",Action="前往交付",Order=order.Id};
            if(ready>=order.Quantity)return new HardwareHint{Key="reserve",Title="5 / 6 · 预留合格成品",Detail="仓库已有 "+ready+" / "+order.Quantity+" 张合格成品。在订单页预留库存，再整单交付。",Action="前往预留",Order=order.Id};
            var all=nodes.SelectMany(n=>n.Input.Concat(n.Output).Concat(n.Batch))
                .Concat(w.State.Routes.Where(r=>r.Factory==factory).SelectMany(r=>r.Cargo))
                .Concat(w.State.Vehicles.Where(v=>v.TargetFactory==factory).SelectMany(v=>v.Cargo))
                .Concat(h.Deliveries.Where(d=>d.Factory==factory).SelectMany(d=>d.Cargo)).ToArray();
            var heat=quiet?HardwareCatalog.QuietHeat:HardwareCatalog.Heat;
            // Each heatsink exists once, either as itself or embodied in a downstream product.
            var supplied=all.Count(g=>(g.ReservedOrder==0||g.ReservedOrder==order.Id)&&(g.Material==heat||g.Material==order.Material||g.Material==(quiet?HardwareCatalog.QuietRaw:HardwareCatalog.RawGPU)||g.Material==(quiet?HardwareCatalog.QuietTested:HardwareCatalog.Tested)));
            if(supplied<order.Quantity&&!nodes.Any(n=>n.Kind==BuildingKind.Manufacturer&&n.RecipeId==(quiet?"quiet-heat":"heat")))
                return new HardwareHint{Key="supply",Title="2 / 6 · 补齐散热器",Detail="还需 "+(order.Quantity-supplied)+" 个"+HardwareCatalog.Name(heat)+"。采购并配送入库后自动供料；也可自行建设散热器加工机。",Action="查看缺料采购",Material=heat,Quantity=order.Quantity-supplied,Order=order.Id};
            if(supplied<order.Quantity&&all.Count(g=>g.Material==HardwareCatalog.Aluminum)<(quiet?3:2))
            {
                if(!nodes.Any(n=>n.Kind==BuildingKind.Processor&&n.RecipeId=="aluminum"))
                    return new HardwareHint{Key="supply",Title="2 / 6 · 为散热器加工机补料",Detail="散热器需要铝材。可采购铝材自动配送；长期生产也可建设铝材加工机，再规划皮卡循环采矿。",Action="查看铝材采购",Material=HardwareCatalog.Aluminum,Quantity=Math.Min(80,(order.Quantity-supplied)*(quiet?3:2)),Order=order.Id};
                if(!all.Any(g=>g.Material==HardwareCatalog.Ore))
                    return new HardwareHint{Key="mining",Title="2 / 6 · 规划上游采集",Detail="铝材加工机需要铝原料。运输页规划循环采集，皮卡自动采满返厂卸货；原料 → 铝材 → 散热器会沿输送线供料。",Action="前往规划采矿",Order=order.Id};
            }
            var pickup=h.Deliveries.FirstOrDefault(d=>d.Factory==factory&&d.Method==2);
            if(pickup!=null)return new HardwareHint{Key="pickup",Title="采购物资等待自动自提",Detail="运输页派遣皮卡，自动前往采购站、装载并返厂卸货；大批货物分次取回。",Action="查看运输任务",Order=order.Id};
            var delivery=h.Deliveries.FirstOrDefault(d=>d.Factory==factory&&d.Method!=2);
            if(delivery!=null)return new HardwareHint{Key="arrival",Title="2 / 6 · 等待补料入库",Detail="到货约剩 "+Math.Max(0,(delivery.DueTick-w.State.Tick)/60)+" 秒。到货后产线自动继续；仓满时货物保留在途。",Action="查看采购",Order=order.Id};
            var stage=nodes.Where(n=>n.Batch.Count>0).OrderByDescending(n=>n.Kind==BuildingKind.Packaging?4:n.Kind==BuildingKind.Tester?3:n.Kind==BuildingKind.GPUAssembly?2:1).FirstOrDefault();
            var first=h.FirstProductTick>0;
            if(stage!=null)return new HardwareHint{Key="produce",Title=first?"4 / 6 · 首款产品已下线":"3 / 6 · 生产第一张显卡",Detail=(first?"首张显卡已完成包装！仓库成品 "+ready+" / "+order.Quantity+"。\n":"板卡装配 → 显卡总装 → 检测 → 包装 → 入库。\n")+w.Settings.Hardware.Buildings.First(b=>b.Kind==stage.Kind).Name+" 正在加工，约剩 "+(stage.WorkRemaining+59)/60+" 秒。可用顶部 2× 加速。",Action="定位当前工序",Node=stage.Id,Order=order.Id};
            var blocked=nodes.FirstOrDefault(n=>n.Status.Contains("堵")||n.Status.Contains("资金不足"))??nodes.FirstOrDefault(n=>n.Status.Contains("缺少"));
            if(blocked!=null)return new HardwareHint{Key="blocked",Title=first?"继续生产 · 检查缺料":"3 / 6 · 检查产线等待原因",Detail=w.Settings.Hardware.Buildings.First(b=>b.Kind==blocked.Kind).Name+"："+blocked.Status+"。设备页可查看上游或采购缺料；中间件需由上游制造。",Action="查看等待设备",Node=blocked.Id,Order=order.Id};
            return Hint("produce",first?"4 / 6 · 首款产品已下线":"3 / 6 · 自动供料中","输送线正在供料。板卡装配 → 总装 → 检测 → 包装，合格成品回到仓库；无需手动搬运。","查看产品");
        }
        private static HardwareHint Hint(string key,string title,string detail,string action)=>new HardwareHint{Key=key,Title=title,Detail=detail,Action=action};
    }
}
