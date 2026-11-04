using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    // Labs turn running costs into research points; generation projects keep the campaign endless.
    public sealed partial class WorldSimulation
    {
        public const string GenPrefix="gen-";
        private static long Scale(long value,int percent,int steps)=>HardwareSku.Scale(value,percent,steps);
        public static string GenProject(int gen)=>GenPrefix+gen;
        public ResearchDefinition Research(string id)
        {
            var h=Settings.Hardware;
            if(id!=null&&id.StartsWith(GenPrefix)&&int.TryParse(id.Substring(GenPrefix.Length),out var gen)&&gen>=2)
                return new ResearchDefinition{Id=id,Name="第 "+gen+" 代 GPU 核心",Detail="新工艺核心：显卡性能与合同价约 +"+(h.GenPricePercent-100)+"%，核心采购价 +"+(h.GenCorePercent-100)+"%；旧世代产品开始贬值",
                    Requires=gen>2?GenProject(gen-1):"",Points=(int)Math.Min(1000000,Scale(h.GenPoints,h.GenPointsPercent,gen-2)),Cost=(int)Math.Min(50000000,Scale(h.GenCost,h.GenCostPercent,gen-2))};
            return h.Research?.FirstOrDefault(r=>r.Id==id);
        }
        // Fixed projects not yet done, followed by the next generation.
        public IEnumerable<ResearchDefinition> ResearchOptions()
        {
            var h=State.Hardware;
            foreach(var r in Settings.Hardware.Research??Array.Empty<ResearchDefinition>())if(!h.ResearchDone.Contains(r.Id))yield return r;
            yield return Research(GenProject(h.CoreGen+1));
        }
        public bool ResearchAvailable(ResearchDefinition r)=>r!=null&&!State.Hardware.ResearchDone.Contains(r.Id)&&(string.IsNullOrEmpty(r.Requires)||State.Hardware.ResearchDone.Contains(r.Requires))
            &&(!r.Id.StartsWith(GenPrefix)||r.Id==GenProject(State.Hardware.CoreGen+1));
        public int LabPointsPerMinute(int factory=0)=>State.Nodes.Where(n=>n.Kind==BuildingKind.Lab&&(factory==0||n.Factory==factory)).Sum(n=>Settings.Hardware.LabPoints[n.Level-1])*60/Settings.Hardware.LabCycleSeconds;
        private CommandResult StartResearch(WorldCommand c)
        {
            var h=State.Hardware;var r=Research(c.Recipe);
            if(r==null)return Fail("未知研发项目");
            if(!string.IsNullOrEmpty(h.ResearchActive))return Fail("已有进行中的研发："+Research(h.ResearchActive)?.Name);
            if(!ResearchAvailable(r))return Fail("研发项目已完成或前置未满足");
            if(!Pay(r.Cost))return Fail("研发资金不足：需要 ¥"+r.Cost/100m);
            h.ResearchActive=r.Id;h.ResearchProgress=0;Revision++;
            return Ok("研发启动："+r.Name+"，需要 "+r.Points+" 研发点"+(State.Nodes.Any(n=>n.Kind==BuildingKind.Lab)?"":"；请建造研发实验室产出研发点"));
        }
        private void ProcessLab(BusinessNode n)
        {
            var h=State.Hardware;var hw=Settings.Hardware;
            if(string.IsNullOrEmpty(h.ResearchActive)){n.RunState=NodeState.Idle;n.Status="待命：在研发页选择项目";n.WorkRemaining=n.WorkTotal=0;return;}
            if(n.WorkRemaining<=0)
            {
                if(!Pay(hw.LabFees[n.Level-1])){n.RunState=NodeState.NoFunds;n.Status="研发经费不足";return;}
                n.WorkTotal=n.WorkRemaining=hw.LabCycleSeconds*60;
            }
            n.WorkRemaining--;n.BusyTicks++;n.RunState=NodeState.Working;
            var r=Research(h.ResearchActive);n.Status="研发 "+r.Name+" "+h.ResearchProgress+" / "+r.Points;
            if(n.WorkRemaining>0)return;
            h.ResearchProgress+=hw.LabPoints[n.Level-1];
            if(h.ResearchProgress>=r.Points)CompleteResearch(r);
        }
        private void CompleteResearch(ResearchDefinition r)
        {
            var h=State.Hardware;h.ResearchDone.Add(r.Id);h.ResearchActive="";h.ResearchProgress=0;Revision++;
            if(r.Id.StartsWith(GenPrefix))LaunchGeneration(int.Parse(r.Id.Substring(GenPrefix.Length)));
            else Log("研发完成："+r.Name+"。"+r.Detail);
        }
        private void LaunchGeneration(int gen)
        {
            var h=State.Hardware;h.CoreGen=gen;h.GenLaunchTick=State.Tick;h.NextOfferRefresh=State.Tick;
            if(h.IndustryGen<gen){h.IndustryGen=gen;h.IndustryLaunchTick=State.Tick;h.GenerationHistory.Add(new GenerationLaunch{Gen=gen,Tick=State.Tick});}
            Log("第 "+gen+" 代核心上市！供应商改供新核心，新订单要求更高世代；新工艺前 "+MaturitySeconds/60+" 分钟 A 档率偏低。");
        }
        private void TickGenerations()
        {
            var h=State.Hardware;var interval=Settings.Hardware.GenerationSeconds*60L;
            if(h.ScriptedDone<2||interval<=0)return;
            if(h.NextGenerationTick==0)h.NextGenerationTick=State.Tick+interval;
            if(State.Tick<h.NextGenerationTick)return;
            h.IndustryGen=Math.Max(h.CoreGen,h.IndustryGen)+1;h.IndustryLaunchTick=State.Tick;h.NextGenerationTick=State.Tick+interval;
            h.GenerationHistory.Add(new GenerationLaunch{Gen=h.IndustryGen,Tick=State.Tick});
            h.NextOfferRefresh=State.Tick;Revision++;Log("行业进入第 "+h.IndustryGen+" 代：旧卡贬值。研发下一代核心、设计新型号并改造装配设备。");
        }
        // Value of a card generation relative to a generation-1 card today: newer cores sell higher,
        // lagging generations lose value and keep sliding while the newest core is on the market.
        public int GenerationValuePercent(int gen)
        {
            var hw=Settings.Hardware;var h=State.Hardware;gen=Math.Max(1,gen);
            var value=Scale(100,hw.GenPricePercent,Math.Min(gen,h.CoreGen)-1);
            var lag=Math.Max(h.CoreGen,h.IndustryGen)-gen;if(lag<=0)return (int)value;
            value=Scale(value,hw.LagPercent,lag);
            var obsolete=h.GenerationHistory.FirstOrDefault(l=>l.Gen>gen)?.Tick??Math.Max(h.GenLaunchTick,h.IndustryLaunchTick);
            var minutes=Math.Max(0,State.Tick-obsolete)/3600;value=value*Math.Max(0,1000-hw.DecayPermillePerMinute*minutes)/1000;
            return (int)Math.Max(value,Scale(100,hw.GenPricePercent,gen-1)*hw.ValueFloorPercent/100);
        }
        public int CoreGenPercent=>(int)Scale(100,Settings.Hardware.GenCorePercent,State.Hardware.CoreGen-1);
    }
}
