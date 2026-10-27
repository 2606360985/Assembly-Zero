using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        private CommandResult Execute(WorldCommand c)
        {
            switch(c.Kind){case CommandKind.AcceptOrder:case CommandKind.ReserveOrder:case CommandKind.ReleaseOrder:case CommandKind.DeliverOrder:case CommandKind.CancelOrder:case CommandKind.Purchase:case CommandKind.SpotSell:case CommandKind.TakeControl:case CommandKind.ReleaseControl:case CommandKind.Drive:case CommandKind.Interact:case CommandKind.RecoverVehicle:case CommandKind.RightVehicle:return Settings.IsHardware?HardwareCommand(c):Fail("该命令仅适用于显卡战役");}
            if(c.Kind==CommandKind.Build)return Build(c);
            if(c.Kind==CommandKind.BuyFactory)
            {
                var f=Factory(c.Factory);if(f==null||f.Owned||!f.Discovered||State.Unlocked<4)return Fail("请先发现厂址并售出 T3 商品");
                if(!Pay(Settings.SecondFactoryCost))return Fail("购买厂址资金不足");f.Owned=true;Revision++;return Ok("第二厂址已购入，放置仓库后可接收调拨",f.Id);
            }
            switch(c.Kind){case CommandKind.Explore:case CommandKind.Collect:case CommandKind.Return:case CommandKind.Transfer:case CommandKind.HireCollector:case CommandKind.HireHauler:case CommandKind.Station:case CommandKind.Boost:case CommandKind.PickupPurchase:return VehicleCommand(c);}
            var n=Node(c.Node);if(n==null)return Fail("未选择有效节点");
            if(c.Kind==CommandKind.CancelChange){n.PendingRemove=false;n.PendingRecipe="";return Ok("已取消待完成改建");}
            if(c.Kind==CommandKind.Upgrade)
            {
                if(n.Level>=3)return Fail("已达最高等级");if(State.Unlocked<=n.Level)return Fail("请先销售上一档产品");
                var price=n.Level==1?Settings.Level2Cost:Settings.Level3Cost;if(!Pay(price))return Fail("升级资金不足");
                n.Level++;n.Investment+=price;State.Upgraded=true;Revision++;return Ok("设备已升级至 L"+n.Level,n.Id);
            }
            if(c.Kind==CommandKind.Policy)
            {
                if(c.Supplier!=0){var source=Node(c.Supplier);if(source==null||source.Factory!=n.Factory||source.Id==n.Id||!Produces(source,c.Material))return Fail("指定供应方不提供该物料");}
                if(c.Reserve<0||c.Priority<0||c.Priority>9)return Fail("优先级范围 0–9，保留量不能为负");
                var old=n.Policy(c.Material);var replacement=new SupplyPolicy{Material=c.Material,Supplier=c.Supplier,Priority=c.Priority,Reserve=c.Reserve};
                if(old!=null)n.Policies.Remove(old);n.Policies.Add(replacement);
                var affected=State.Routes.Where(r=>r.To==n.Id&&r.Material==c.Material&&c.Supplier!=0&&r.From!=c.Supplier).ToList();
                var result=CommitNetwork(State.Nodes,affected,null,false);
                if(!result.Success){n.Policies.Remove(replacement);if(old!=null)n.Policies.Add(old);return result;}
                Revision++;return Ok("物流策略已更新");
            }
            if(c.Kind==CommandKind.Recipe)
            {
                var recipe=Settings.Recipe(c.Recipe);if(recipe==null||recipe.Machine!=n.Kind||recipe.Level>n.Level||recipe.Level>State.Unlocked)return Fail("配方与机器类型、等级或解锁不符");
            }
            if((c.Kind==CommandKind.Remove||c.Kind==CommandKind.Recipe)&&n.Batch.Count>0)
            {
                n.PendingRemove=c.Kind==CommandKind.Remove;n.PendingRecipe=c.Kind==CommandKind.Recipe?c.Recipe:"";return Ok("当前批次完成后执行；可取消等待");
            }
            return Edit(n,c);
        }
        public string PlacementError(int factory,int x,int z,int ignored=0)
        {
            if(Factory(factory)?.Owned!=true)return "厂址未购买";
            if(x<3||z<3||x>=GridRouting.Width-3||z>=GridRouting.Depth-3)return "请留出厂界和端口通道";
            if(State.Nodes.Any(n=>n.Factory==factory&&n.Id!=ignored&&Math.Abs(n.X-x)<4&&Math.Abs(n.Z-z)<4))return "设备占地或接口通道重叠";
            if(State.Routes.Any(r=>r.Factory==factory&&r.From!=ignored&&r.To!=ignored&&r.Points.Any(p=>Math.Abs(p.X-x)<=1&&Math.Abs(p.Z-z)<=1)))return "占地与现有输送线冲突";
            return null;
        }
        private CommandResult Build(WorldCommand c)
        {
            var error=PlacementError(c.Factory,c.X,c.Z);if(error!=null)return Fail(error);
            var f=Factory(c.Factory);var free=c.Building==BuildingKind.Warehouse&&f.FreeWarehouse||c.Building==BuildingKind.Sales&&f.FreeSales;
            var cost=free?0:Settings.Cost(c.Building);if(State.Money<cost)return Fail("建造资金不足");
            if(Settings.IsHardware&&!Settings.Hardware.Buildings.Any(b=>b.Kind==c.Building))return Fail("未知建筑");
            var recipe=Settings.IsHardware?(string.IsNullOrEmpty(c.Recipe)?Settings.Hardware.Buildings.First(b=>b.Kind==c.Building).Recipe:c.Recipe):c.Building==BuildingKind.Manufacturer?"kit":c.Building==BuildingKind.Processor?"shell":"";
            if(Settings.IsHardware&&!string.IsNullOrEmpty(recipe)&&Settings.Recipe(recipe)?.Machine!=c.Building)return Fail("配方与设备不匹配");
            var n=new BusinessNode{Id=State.NextNode,Factory=c.Factory,X=c.X,Z=c.Z,Rotation=((c.Rotation%4)+4)%4,Kind=c.Building,RecipeId=recipe,Investment=cost};
            var proposed=new List<BusinessNode>(State.Nodes){n};
            var result=CommitNetwork(proposed,new List<BusinessRoute>(),null,true);if(!result.Success)return result;
            State.NextNode++;Pay(cost);State.Built=true;
            if(c.Building==BuildingKind.Warehouse){f.FreeWarehouse=false;n.Output.AddRange(f.Starter);f.Starter.Clear();}
            if(c.Building==BuildingKind.Sales)f.FreeSales=false;
            Revision++;return Ok("节点已建造；缺少上游时可稍后补建",n.Id);
        }
        private CommandResult Edit(BusinessNode n,WorldCommand c)
        {
            if(c.Kind!=CommandKind.Move&&c.Kind!=CommandKind.Remove&&c.Kind!=CommandKind.Recipe)return Fail("不支持的节点操作");
            if(c.Kind==CommandKind.Move){var error=PlacementError(n.Factory,c.X,c.Z,n.Id);if(error!=null)return Fail(error);}
            var affected=State.Routes.Where(r=>r.From==n.Id||r.To==n.Id).ToList();
            var oldX=n.X;var oldZ=n.Z;var oldRotation=n.Rotation;var oldRecipe=n.RecipeId;
            if(c.Kind==CommandKind.Move){n.X=c.X;n.Z=c.Z;n.Rotation=((c.Rotation%4)+4)%4;}
            if(c.Kind==CommandKind.Recipe)n.RecipeId=c.Recipe;
            var proposed=State.Nodes.Where(x=>c.Kind!=CommandKind.Remove||x!=n).ToList();
            var recover=c.Kind==CommandKind.Move?null:n;
            var result=CommitNetwork(proposed,affected,recover,false);
            if(!result.Success){n.X=oldX;n.Z=oldZ;n.Rotation=oldRotation;n.RecipeId=oldRecipe;return result;}
            if(c.Kind==CommandKind.Remove){var refund=n.Investment*3/4;State.Money+=refund;State.Expenses-=refund;if(Settings.IsHardware)Cash(refund,"设备回收");}
            n.PendingRemove=false;n.PendingRecipe="";Revision++;return Ok(c.Kind==CommandKind.Remove?"已拆除并回收库存，返还 75% 费用":"改建完成",n.Id);
        }
        private bool Produces(BusinessNode n,int material)=>n.Kind==BuildingKind.Warehouse?(Settings.IsHardware?Settings.Hardware.Materials.Any(m=>m.Id==material&&(m.Price>0||m.Id==HardwareCatalog.Ore||m.Finished)):material>=8&&material<=10||n.Output.Any(i=>i.Material==material)):Settings.Recipe(n.RecipeId) is ProductRule r&&(r.Output==material||r.Byproduct!=0&&r.Byproduct==material);
        private IEnumerable<int> Needs(BusinessNode n)
        {
            if(n.Kind==BuildingKind.Warehouse)return Settings.IsHardware?Settings.Hardware.Materials.Where(m=>m.Finished).Select(m=>m.Id):Array.Empty<int>();
            if(n.Kind==BuildingKind.Sales)return Settings.IsHardware?Array.Empty<int>():new[]{17,18,19};
            return Settings.Recipe(n.RecipeId)?.Inputs.Select(i=>i.Material)??Array.Empty<int>();
        }
        private CommandResult CommitNetwork(List<BusinessNode> proposed,List<BusinessRoute> removed,BusinessNode recover,bool allowDisconnected)
        {
            var recoverItems=removed.SelectMany(r=>r.Cargo).ToList();
            if(recover!=null)recoverItems.AddRange(recover.Input.Concat(recover.Output));
            var factory=recover?.Factory??removed.FirstOrDefault()?.Factory??0;
            var stores=proposed.Where(n=>n.Factory==factory&&n.Kind==BuildingKind.Warehouse&&n!=recover).OrderBy(n=>n.Id).ToList();
            if(recoverItems.Count>stores.Sum(n=>Capacity(n)-n.Output.Count))return Fail("本厂仓库回收空间不足，需要 "+recoverItems.Count+" 格");
            var routes=State.Routes.Except(removed).ToList();var next=State.NextRoute;
            foreach(var target in proposed.OrderBy(n=>n.Kind==BuildingKind.Sales?1:0).ThenBy(n=>n.Id))
            foreach(var material in Needs(target))
            {
                var policy=target.Policy(material);
                var sources=proposed.Where(n=>n.Factory==target.Factory&&n.Id!=target.Id&&!(target.Kind==BuildingKind.Warehouse&&n.Kind==BuildingKind.Warehouse)&&Produces(n,material)&&(policy==null||policy.Supplier==0||policy.Supplier==n.Id))
                    .OrderBy(n=>n.Kind==BuildingKind.Warehouse?1:0).ThenBy(n=>GridPoint.Distance(n.Center,target.Center)).ThenBy(n=>n.Id).ToList();
                if(!Settings.IsHardware&&target.Kind!=BuildingKind.Sales&&routes.Any(r=>r.To==target.Id&&r.Material==material))continue;
                foreach(var source in sources)
                {
                    if(routes.Any(r=>r.From==source.Id&&r.To==target.Id&&r.Material==material))continue;
                    var points=GridRouting.FactoryPath(source,target,proposed.Where(n=>n.Factory==target.Factory),routes.Where(r=>r.Factory==target.Factory));
                    if(points==null)continue;
                    var route=new BusinessRoute{Id=next++,Factory=target.Factory,From=source.Id,To=target.Id,Material=material,Points=points,Length=Math.Max(1024,(points.Count-1)*1229+512)};
                    route.Initialize();routes.Add(route);if(!Settings.IsHardware&&target.Kind!=BuildingKind.Sales)break;
                }
                if(!allowDisconnected&&sources.Count>0&&!routes.Any(r=>r.To==target.Id&&r.Material==material)&&removed.Any(r=>r.To==target.Id&&r.Material==material))return Fail("无法连接：新位置阻挡物料 "+EconomySettings.MaterialName(material));
            }
            foreach(var item in recoverItems)stores.First(n=>n.Output.Count<Capacity(n)).Output.Add(item);
            if(recover!=null){recover.Input.Clear();recover.Output.Clear();}
            State.Nodes=proposed;State.Routes=routes;State.NextRoute=next;Index();
            foreach(var target in proposed)foreach(var m in Needs(target))if(!routes.Any(r=>r.To==target.Id&&r.Material==m))target.Status="无法连接 "+EconomySettings.MaterialName(m)+"：缺供应方或通道";
            return Ok("物流连接已更新");
        }
    }
}
