using System;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        public bool NeedsRetooling(BusinessNode n)=>Settings.IsHardware&&(n.Kind==BuildingKind.BoardAssembly||n.Kind==BuildingKind.GPUAssembly)&&n.ProcessGen<State.Hardware.CoreGen;
        private CommandResult Retool(WorldCommand c)
        {
            var n=Node(c.Node);
            if(n==null||!NeedsRetooling(n)||n.RetoolGen!=0||n.PendingRemove||n.PendingRecipe!="")return Fail("该设备无需改造或已有待执行改造");
            if(!Pay(Settings.Hardware.RetoolCost))return Fail("工艺改造资金不足");
            n.RetoolGen=State.Hardware.CoreGen;n.RetoolRemaining=Settings.Hardware.RetoolSeconds*60;Revision++;
            return Ok("当前批次完成后停机改造 "+Settings.Hardware.RetoolSeconds+" 秒；保留原输送接口");
        }
        private bool TickRetooling(BusinessNode n)
        {
            if(n.RetoolGen==0||n.Batch.Count>0)return false;
            n.RunState=NodeState.Working;n.Status="停机改造至 G"+n.RetoolGen+" · "+Math.Max(0,n.RetoolRemaining/60)+" 秒";
            if(--n.RetoolRemaining<=0){n.ProcessGen=n.RetoolGen;n.RetoolGen=0;n.RetoolRemaining=0;RecoverUnusedBoards(n.Factory);Revision++;}
            return true;
        }
        // Attribute filters apply before loading a machine, so mismatched boards cannot block its input.
        public bool AcceptsGoods(BusinessNode n,Goods g)
        {
            if(!Settings.IsHardware||n.Kind==BuildingKind.Warehouse)return true;
            if(n.RetoolGen!=0)return false;
            var r=Recipe(n.RecipeId);if(r==null)return true;
            if(r.OnlyGrade!=0&&g.Material==HardwareCatalog.Tested&&g.Grade!=r.OnlyGrade)return false;
            if(n.Kind==BuildingKind.BoardAssembly&&g.Material==HardwareCatalog.Core)
            {
                var designs=State.Nodes.Where(x=>x.Factory==n.Factory&&x.Kind==BuildingKind.GPUAssembly).Select(x=>Sku(Recipe(x.PendingRecipe!=""?x.PendingRecipe:x.RecipeId)?.SpecId)).Where(s=>s!=null).ToArray();
                return g.Gen<=Math.Max(1,n.ProcessGen)&&(designs.Length==0||designs.Any(s=>s.Gen==g.Gen&&(s.CoreBin==0||s.CoreBin==HardwareSku.CoreBin(g.CoreBin))));
            }
            var sku=Sku(r.SpecId);
            return n.Kind!=BuildingKind.GPUAssembly||g.Material!=HardwareCatalog.AssembledBoard||sku==null||
                g.Gen==sku.Gen&&(sku.CoreBin==0||HardwareSku.CoreBin(g.CoreBin)==sku.CoreBin);
        }
        private void RecoverUnusedBoards(int factory)
        {
            var consumers=State.Nodes.Where(n=>n.Factory==factory&&n.Kind==BuildingKind.GPUAssembly).ToArray();
            if(consumers.Length==0)return;
            foreach(var n in State.Nodes.Where(n=>n.Factory==factory&&n.Kind==BuildingKind.BoardAssembly))
            foreach(var g in n.Output.Where(g=>g.Material==HardwareCatalog.AssembledBoard&&!consumers.Any(c=>AcceptsGoods(c,g)))
                .Concat(n.Input.Where(g=>g.Material==HardwareCatalog.Core&&!AcceptsGoods(n,g))).ToArray())
            {
                var store=State.Nodes.FirstOrDefault(s=>s.Factory==factory&&s.Kind==BuildingKind.Warehouse&&s.Output.Count<Capacity(s));
                if(store==null)return;
                n.Output.Remove(g);n.Input.Remove(g);store.Output.Add(g);logisticsDirty=true;
            }
        }
    }
}
