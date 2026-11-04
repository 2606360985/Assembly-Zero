using System;

namespace AssemblyZero.Domain.EP04
{
    // Inspection bins every passing card into A/B/C. Better testers and mature processes yield more A cards.
    public sealed partial class WorldSimulation
    {
        public const int GradeA=1, GradeB=2, GradeC=3, MaturitySeconds=600, MaturityPenalty=10;
        public static string GradeName(int grade)=>grade==GradeA?"A":grade==GradeB?"B":grade==GradeC?"C":"—";
        public static string GradeRequirement(int minGrade)=>minGrade==GradeA?"仅 A 档":minGrade==GradeB?"B 档及以上":"不限档位";
        // Contract and resale value of a grade relative to the SKU price.
        public static int GradePercent(int grade)=>grade==GradeA?125:grade==GradeC?80:100;
        public static int RequirementPercent(int minGrade)=>minGrade==GradeA?125:minGrade==GradeB?105:100;
        public bool ResearchCompleted(string id)=>Settings.IsHardware&&State.Hardware.ResearchDone.Contains(id);
        // Remaining A-grade penalty (percentage points) while the newest core generation is still ramping up.
        public int ProcessPenalty(int gen)
        {
            var h=State.Hardware;if(gen!=h.CoreGen||h.CoreGen<=1)return 0;
            var window=MaturitySeconds*60L;var elapsed=State.Tick-h.GenLaunchTick;
            return elapsed>=window?0:(int)(MaturityPenalty*(window-elapsed)/window);
        }
        public (int A,int B,int C) GradeOdds(BusinessNode tester,int gen)
        {
            var level=Math.Max(1,tester?.Level??1);var penalty=ProcessPenalty(gen);
            var a=15+5*(level-1)+(ResearchCompleted("test-bin")?5:0)-penalty;var c=25-5*(level-1)+penalty;
            a=Math.Max(0,Math.Min(100,a));c=Math.Max(0,Math.Min(100-a,c));
            return (a,100-a-c,c);
        }
        private int RollGrade(BusinessNode tester,int gen)
        {
            var odds=GradeOdds(tester,gen);var roll=NextRandom(100);
            return roll<odds.A?GradeA:roll<odds.A+odds.B?GradeB:GradeC;
        }
        public static bool GradeSatisfies(int grade,int minGrade)=>minGrade==0||grade>0&&grade<=minGrade;
    }
}
