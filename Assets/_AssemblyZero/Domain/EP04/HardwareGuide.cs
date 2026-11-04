using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed class HardwareHint
    {
        public string Key,Title,Detail,Action;
        public int Order,Node,Material,Quantity,Generation,CoreBin;
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
                // Once the opening contracts are done, steer the player toward research and new generations.
                if(h.Settlements.Count>=2&&!w.State.Nodes.Any(n=>n.Kind==BuildingKind.Lab))
                    return new HardwareHint{Key="research",Title="建造研发实验室",Detail="研发解锁大显存、塔式散热和新一代核心。新世代上市后旧卡持续贬值，越早投入越主动。",Action="放置研发实验室",Building=BuildingKind.Lab};
                if(w.State.Nodes.Any(n=>n.Kind==BuildingKind.Lab)&&string.IsNullOrEmpty(h.ResearchActive))
                    return Hint("research","选择研发项目","实验室空闲。下一代核心："+w.Research(WorldSimulation.GenProject(h.CoreGen+1)).Name+"；落后两代时旧卡接近残值。","打开研发");
                if(h.CoreGen>1&&w.State.Nodes.Any(n=>n.Kind==BuildingKind.GPUAssembly)&&w.FinishedStock(factory).Any(g=>g.Material==HardwareCatalog.Standard&&g.Gen<=h.CoreGen-2))
                    return Hint("generation","旧世代库存贬值中","仓库里有落后两代的显卡，合同不再接收，尽快在市场页现货清仓，并采购新核心。","查看市场");
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
            var budget=order.Material==HardwareCatalog.Budget;var sku=budget?null:w.Sku(order.Spec);
            var retool=nodes.FirstOrDefault(n=>w.NeedsRetooling(n)&&sku!=null&&sku.Gen>n.ProcessGen);
            if(retool!=null)return new HardwareHint{Key="retool",Title="改造装配工艺至 G"+sku.Gen,Detail="新核心需要设备工艺改造。每台停机 "+w.Settings.Hardware.RetoolSeconds+" 秒，保留输送接口。",Action="查看设备改造",Node=retool.Id,Order=order.Id};
            if(!budget)foreach(var kind in new[]{BuildingKind.GPUAssembly,BuildingKind.Tester,BuildingKind.Packaging})
            {
                var id=kind==BuildingKind.GPUAssembly?HardwareSku.RecipeId(order.Spec):kind==BuildingKind.Tester?"test":"pack";
                if(!nodes.Any(n=>n.Kind==kind&&(n.RecipeId==id||n.PendingRecipe==id)))return new HardwareHint{Key="recipe",Title="切换产品配方",Detail="当前订单要求"+w.ProductName(order.Spec)+"。把一台显卡总装切换到该型号（检测与包装通用）；在产品页一键切换，等待旧批次完成。",Action="查看产品",Order=order.Id};
            }
            if(budget&&!nodes.Any(n=>n.Kind==BuildingKind.Packaging&&(n.RecipeId=="budget-pack"||n.PendingRecipe=="budget-pack")))
                return new HardwareHint{Key="recipe",Title="丐版需要专门包装",Detail="把一台包装机切换到“丐版包装”：次品 + 包装材料 → S1 丐版。建议另建一台包装机。",Action="查看产品",Order=order.Id};
            var stock=Inventory(w,factory,order);
            if(!budget&&sku!=null)
            {
                var heat=sku.Cooler;var heatRecipe=w.Settings.Recipes.FirstOrDefault(r=>r.Output==heat&&r.Machine==BuildingKind.Manufacturer);
                var aluminumPer=heatRecipe?.Inputs.FirstOrDefault(i=>i.Material==HardwareCatalog.Aluminum)?.Quantity??2;
                var purchasable=w.Settings.Hardware.Materials.Any(m=>m.Id==heat&&m.Price>0);
                var needs=new[]{heat,HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board,HardwareCatalog.Shell,HardwareCatalog.Carton};
                foreach(var m in needs)
                {
                    var perCard=m==HardwareCatalog.Memory?sku.Memory:1;
                    var missing=order.Quantity*perCard-Supplied(stock,m,order,sku.Memory,sku.Gen,sku.CoreBin);if(missing<=0)continue;
                    var making=heatRecipe!=null&&nodes.Any(n=>n.Kind==BuildingKind.Manufacturer&&(n.RecipeId==heatRecipe.Id||n.PendingRecipe==heatRecipe.Id));
                    if(m==heat&&!purchasable&&!making)
                        return new HardwareHint{Key="build",Title="建造"+HardwareCatalog.Name(heat)+"产线",Detail=HardwareCatalog.Name(heat)+"不能外购。放一台散热器加工机并切换到该配方，再用铝材供料。",Action="放置散热器加工",Building=BuildingKind.Manufacturer,Order=order.Id};
                    if(m==heat&&making)
                    {
                        if(stock.Count(g=>g.Material==HardwareCatalog.Aluminum)>=aluminumPer)continue;
                        if(!nodes.Any(n=>n.Kind==BuildingKind.Processor&&n.RecipeId=="aluminum"))
                            return new HardwareHint{Key="supply",Title="为散热器加工机补料",Detail="散热器需要铝材。可采购铝材自动配送；长期生产也可建设铝材加工机，再规划皮卡循环采矿。",Action="查看铝材采购",Material=HardwareCatalog.Aluminum,Quantity=Math.Min(80,missing*aluminumPer),Order=order.Id};
                        if(!stock.Any(g=>g.Material==HardwareCatalog.Ore))
                            return new HardwareHint{Key="mining",Title="规划上游采集",Detail="铝材加工机需要铝原料。运输页规划循环采集，皮卡自动采满返厂卸货。",Action="前往规划采矿",Order=order.Id};
                        continue;
                    }
                    return new HardwareHint{Key="supply",Title="补齐"+HardwareCatalog.Name(m),Detail="还差 "+missing+" 个"+HardwareCatalog.Name(m)+(m==HardwareCatalog.Core?" · G"+sku.Gen+" / "+HardwareSku.CoreName(sku.CoreBin):"")+"。常规配送 "+w.Settings.Hardware.DeliverySeconds+" 秒到货；"+(m==heat?"也可自建散热器加工机，长期更便宜。":"留意行情，涨价前囤货更划算。"),Action="查看缺料采购",Material=m,Quantity=missing,Generation=sku.Gen,CoreBin=sku.CoreBin,Order=order.Id};
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
        private static int Supplied(List<Goods> stock,int material,HardwareOrder order,int cardMemory,int generation,int coreBin)
        {
            var downstream=new List<int>{material};
            if(material!=HardwareCatalog.Carton)downstream.AddRange(new[]{HardwareCatalog.RawGPU,HardwareCatalog.Tested});
            if(material==HardwareCatalog.Core||material==HardwareCatalog.Memory||material==HardwareCatalog.Board)downstream.Add(HardwareCatalog.AssembledBoard);
            downstream.Add(order.Material);
            // A finished or semi-finished card embodies all memory of its SKU; loose parts and boards count once.
            return stock.Where(g=>downstream.Contains(g.Material)&&(!(g.Material==HardwareCatalog.Core||g.Material==HardwareCatalog.AssembledBoard)||g.Gen==generation&&(coreBin==0||HardwareSku.CoreBin(g.CoreBin)==coreBin))&&(!HardwareCatalog.IsCard(g.Material)||g.SpecId==order.Spec&&!g.Flawed&&g.Gen>=order.MinGen&&(g.Grade==0||WorldSimulation.GradeSatisfies(g.Grade,order.MinGrade))))
                .Sum(g=>material==HardwareCatalog.Memory&&HardwareCatalog.IsCard(g.Material)?cardMemory:1);
        }
        private static HardwareHint Hint(string key,string title,string detail,string action)=>new HardwareHint{Key=key,Title=title,Detail=detail,Action=action};
    }
}
