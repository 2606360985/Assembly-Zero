using System;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    // Player-designed SKUs: slot choices derive performance, noise and cost; the launch price sets the review score.
    public sealed partial class WorldSimulation
    {
        public bool MemoryUnlocked(int memory)=>memory==1||memory==2&&ResearchCompleted("mem-2x")||memory==4&&ResearchCompleted("mem-4x");
        public bool CoolerUnlocked(int cooler)=>cooler==HardwareCatalog.Heat||cooler==HardwareCatalog.QuietHeat||cooler==HardwareCatalog.TowerHeat&&ResearchCompleted("tower-cooler");
        public bool RecipeUnlocked(ProductRule r)=>r!=null&&(r.Output!=HardwareCatalog.TowerHeat||ResearchCompleted("tower-cooler"));
        // Builds an unsaved SKU from design fields; used for the designer preview and by DesignSku.
        public SkuDefinition Draft(WorldCommand c)
        {
            var gen=c.Generation>0?c.Generation:State.Hardware.CoreGen;
            var s=new SkuDefinition{Id="c"+State.Hardware.NextSku,Name=(c.Name??"").Trim(),Gen=gen,Memory=c.Memory,Cooler=c.Cooler,Shell=Math.Max(0,Math.Min(7,c.Shell)),Code="S"+gen+"-"+State.Hardware.NextSku};
            s.CoreBin=c.CoreBin;s.Market=c.Market;s.Fans=c.Fans;s.Length=c.Length;s.Backplate=c.Backplate;
            s.Short=s.Name;s.BasePrice=c.Price>0?c.Price:HardwareSku.SuggestedPrice(s);s.Score=HardwareSku.Score(s,State.Hardware.Reputation);return s;
        }
        private CommandResult DesignSku(WorldCommand c)
        {
            var s=Draft(c);var h=State.Hardware;
            if(s.Gen<1||s.Gen>h.CoreGen||c.CoreBin<0||c.CoreBin>3||!Enum.IsDefined(typeof(TargetMarket),c.Market))return Fail("核心世代、体质或目标市场无效");
            if(c.Fans<0||c.Fans>3||c.Length<0||c.Length>2||c.Shell<0||c.Shell>7)return Fail("外观参数无效");
            if(string.IsNullOrEmpty(s.Name)||s.Name.Length>16)return Fail("型号名称需 1–16 个字符");
            if(s.Name.Any(char.IsControl)||s.Name.Contains("<")||s.Name.Contains(">"))return Fail("型号名称不能包含控制字符或富文本标记");
            if(h.Skus.Any(x=>!x.Retired&&x.Name==s.Name))return Fail("已有同名在售型号");
            if(c.Memory!=1&&c.Memory!=2&&c.Memory!=4||!MemoryUnlocked(c.Memory))return Fail("显存配置未解锁");
            if(!HardwareCatalog.IsCooler(c.Cooler)||!CoolerUnlocked(c.Cooler))return Fail("散热方案未解锁");
            var suggested=HardwareSku.SuggestedPrice(s);if(s.BasePrice<suggested/2||s.BasePrice>suggested*2)return Fail("定价需在建议价的 50%–200% 之间");
            if(h.Skus.Count(x=>!x.Retired)>=HardwareSku.MaxActive)return Fail("在售型号已达上限 "+HardwareSku.MaxActive+"，请先下架旧型号");
            h.NextSku++;h.Skus.Add(s);RefreshSkus();Revision++;
            return Ok("新型号上市："+s.Name+" · 评测 "+s.Score+" 分；在显卡总装切换到该型号即可生产",h.Skus.Count);
        }
        private CommandResult RetireSku(WorldCommand c)
        {
            var s=Sku(c.Spec);if(s==null||s.Retired)return Fail("型号不存在或已下架");
            if(State.Hardware.Skus.Count(x=>!x.Retired)<=1)return Fail("至少保留一个在售型号");
            s.Retired=true;Revision++;return Ok("已下架 "+s.Name+"：不再接到新订单，已有订单、库存和在产批次不受影响");
        }
        // Client demand picks a SKU that meets its noise/performance needs, favouring better-reviewed models.
        private bool FitsClient(SkuDefinition s,ClientType c)=>!s.Retired&&(s.Market==TargetMarket.General||c.Market==TargetMarket.General||s.Market==c.Market)
            &&(c.Gen==0||s.Gen>=(c.Gen==2?Math.Max(State.Hardware.CoreGen,State.Hardware.IndustryGen):Math.Max(1,Math.Max(State.Hardware.CoreGen,State.Hardware.IndustryGen)-1)))
            &&(c.Gen==0||(c.MaxNoise==0?s.Cooler!=HardwareCatalog.QuietHeat:HardwareSku.Noise(s.Cooler)<=c.MaxNoise))&&HardwareSku.PerfFactor(s)>=c.MinPerf;
        private string PickSku(ClientType c)
        {
            if(c.Material!=HardwareCatalog.Standard)return null;
            var fits=State.Hardware.Skus.Where(s=>FitsClient(s,c)).ToArray();
            if(fits.Length==0)return null;if(fits.Length==1)return fits[0].Id;
            // Recovery customers order the model already on the line, keeping the zero-cash route usable.
            if(c.Gen==0)return fits.OrderByDescending(s=>State.Nodes.Any(n=>n.Kind==BuildingKind.GPUAssembly&&n.RecipeId==HardwareSku.RecipeId(s.Id))).ThenBy(s=>s.BasePrice).First().Id;
            int Weight(SkuDefinition s)=>s.Score*(s.Market!=TargetMarket.General&&s.Market==c.Market?2:1);
            var roll=NextRandom(fits.Sum(Weight));foreach(var s in fits){if(roll<Weight(s))return s.Id;roll-=Weight(s);}
            return fits[0].Id;
        }
        // Converts a SKU price (set in the money of its design generation) to the value of a card of the given generation today.
        public int SkuPricePercent(string spec,int gen)
        {
            var sku=Sku(spec);var nominal=Scale(100,Settings.Hardware.GenPricePercent,Math.Max(1,sku?.Gen??1)-1);
            return (int)((long)GenerationValuePercent(gen)*100/nominal);
        }
    }
}
