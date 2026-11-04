using System;
using System.Collections.Generic;

namespace AssemblyZero.Domain.EP04
{
    // Derives the final-assembly recipe of a SKU; every SKU flows through the same card materials.
    public static class HardwareSku
    {
        public const string RecipePrefix="sku:";
        public static string RecipeId(string sku)=>RecipePrefix+sku;
        public static bool IsSkuRecipe(string recipe)=>recipe!=null&&recipe.StartsWith(RecipePrefix);
        public static string SkuOf(string recipe)=>IsSkuRecipe(recipe)?recipe.Substring(RecipePrefix.Length):"";
        public static int CoolerSeconds(int cooler)=>cooler==HardwareCatalog.QuietHeat?2:cooler==HardwareCatalog.TowerHeat?3:0;
        public static int CoolerFee(int cooler)=>cooler==HardwareCatalog.QuietHeat?50:cooler==HardwareCatalog.TowerHeat?100:0;
        public const int GenPerfPercent=135, MaxActive=12;
        // Saturating integer scaling keeps arbitrarily late generations valid within the cash protocol.
        public static long Scale(long value,int percent,int steps,long cap=1000000000)
        {
            for(var i=0;i<steps&&value<cap;i++){var next=Math.Min(cap,value*percent/100);if(next==value)break;value=next;}
            return Math.Min(cap,value);
        }
        public static int CoreBin(int bin)=>bin==0?WorldSimulation.GradeB:bin;
        public static string CoreName(int bin)=>bin==1?"A 优选":bin==3?"C 经济":bin==2?"B 主流":"混合体质";
        public static string MarketName(TargetMarket market)=>new[]{"通用","游戏","矿场","AI","OEM"}[(int)market];
        public static int Noise(int cooler)=>cooler==HardwareCatalog.QuietHeat?28:cooler==HardwareCatalog.TowerHeat?34:40;
        public static string CoolerName(int cooler)=>cooler==HardwareCatalog.QuietHeat?"低噪三风扇":cooler==HardwareCatalog.TowerHeat?"塔式散热":"标准双风扇";
        // Performance relative to a 1-memory standard card of the same generation (100 = baseline).
        public static int PerfFactor(SkuDefinition s)=>(100+18*(s.Memory>=4?2:s.Memory>=2?1:0))*(s.Cooler==HardwareCatalog.TowerHeat?106:100)/100*(s.CoreBin==1?115:s.CoreBin==3?85:100)/100;
        public static int Perf(SkuDefinition s,int gen)=>(int)Scale(PerfFactor(s),GenPerfPercent,Math.Max(0,gen-1));
        // Fair launch price in generation-1 money: performance plus a premium for quieter coolers.
        public static int SuggestedPrice(SkuDefinition s)=>(int)Scale(48000L*PerfFactor(s)/100*(s.Cooler==HardwareCatalog.QuietHeat?112:s.Cooler==HardwareCatalog.TowerHeat?104:100)/100,GenPerfPercent,Math.Max(0,s.Gen-1),10000000)/100*100;
        // Review score 10..99 from value for money; 50 means priced exactly at the suggestion.
        public static int Score(SkuDefinition s,int reputation=10)=>s.BasePrice<=0?50:System.Math.Max(10,System.Math.Min(99,50+(int)((long)SuggestedPrice(s)*100/s.BasePrice-100)*3/2+(reputation-10)/5));
        public static ProductRule Assembly(SkuDefinition s)
        {
            var inputs=new List<RecipeInput>{new RecipeInput(HardwareCatalog.AssembledBoard),new RecipeInput(s.Cooler),new RecipeInput(HardwareCatalog.Shell)};
            // The board already carries one memory package; larger SKUs add the rest at final assembly.
            if(s.Memory>1)inputs.Add(new RecipeInput(HardwareCatalog.Memory,s.Memory-1));
            return new ProductRule{Id=RecipeId(s.Id),Name="总装 · "+s.Name,Output=HardwareCatalog.RawGPU,Machine=BuildingKind.GPUAssembly,
                Seconds=12+CoolerSeconds(s.Cooler)+(s.Memory-1),ProcessingFee=200+CoolerFee(s.Cooler)+(s.Memory-1)*30,
                Inputs=inputs.ToArray(),SpecId=s.Id,SpecVersion=s.Version,FlawPermille=100};
        }
    }
}
