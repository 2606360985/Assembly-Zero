using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    [Serializable] public sealed class NarrativeProgress
    {
        public int Version, Founder, Line, Factory=1, QualityMode, QualityStreak, ValidationCycles, Order, ContextFactory=1;
        public string FounderName="", Active="", LastDelivery="", LastProduct="", ProductId="", LogKey="";
        public bool IdentityChosen, Legacy, ValidationAccepted, Disabled, AiDeferred;
        public List<string> Completed=new List<string>(), Pending=new List<string>();
        public List<string> History=new List<string>();
    }

    public sealed class FounderLine
    {
        public string Actor, Text, Expression;
        public FounderLine(string actor,string text,string expression="calm"){Actor=actor;Text=text;Expression=expression;}
    }
    public sealed class FounderScene
    {
        public string Id, Title, Chapter, Location, Art, Card;
        public FounderLine[] Lines;
        public FounderScene(string id,string title,string chapter,string location,string art,string card,params FounderLine[] lines)
        {Id=id;Title=title;Chapter=chapter;Location=location;Art=art;Card=card;Lines=lines;}
    }
    // Content is engine-independent. Stable IDs are also the save and replay keys.
    public static class FounderStory
    {
        public static readonly FounderScene[] Scenes={
            new FounderScene("intro","把名字留在产品上","序章 / 创业者","星铸 · 测试车间","workshop","起步资源有限。信誉，从第一批产品开始。",
                new FounderLine("partner","厂房租下来了，第一批料也到了。现在，这条线得靠我们自己跑起来。","thinking"),
                new FounderLine("player","以前我们只对一个项目负责。从今天起，产品、交期，还有每一个相信我们的人，都得一起考虑。"),
                new FounderLine("partner","资金是起点，不是答案。先看样卡的测试结果。"),
                new FounderLine("player","把检测工位准备好。我想让客户拿到的每一张卡，都配得上我们的名字。"),
                new FounderLine("partner","晨湾的负责人正在等回复。第一单不大，但他们会用这批卡完成游戏的最后一次渲染。"),
                new FounderLine("player","那就从一次可靠的交付开始。","relieved")),
            new FounderScene("contract1","首批交付","第一章 / 首批交付","星铸 · 样卡验收台","acceptance","首批合同 · 实际数量、价格与交期见资料卡",
                new FounderLine("studio","样卡的画面没有问题。我们担心的是，后面送来的卡能不能保持一样的水平。","questioning"),
                new FounderLine("partner","单张通过，不代表批量稳定。测试工位还没补齐，这件事不能省。","thinking"),
                new FounderLine("player","我们增加检测工位，记录连续合格结果，再放行首批产品。"),
                new FounderLine("studio","可以。我们要的不是一张漂亮样卡，是整个团队都能按时开工。"),
                new FounderLine("partner","先稳定良率，还是提前准备并行测试？两种做法都要连续三张通过，区别是设备投入。","questioning"),
                new FounderLine("player","把验收条件写清楚。签下去，就按这个标准交付。")),
            new FounderScene("first","第一张，属于我们","第一章 / 首批交付","星铸 · 成品检测出口","first_product","首件记录 · 生产与检测来自真实产线",
                new FounderLine("partner","第一张合格成品下线了。外壳、风扇、测试记录，都齐了。","relieved"),
                new FounderLine("player","拿在手里的感觉，和看设计图完全不同。","relieved"),
                new FounderLine("partner","先别急着庆祝。继续看后面几张，确认这不是偶然。","thinking"),
                new FounderLine("player","记下它的批次。以后工厂再大，也留着这份首件记录。"),
                new FounderLine("partner","连续合格达到约定后，仓库会自动配齐并交付。"),
                new FounderLine("player","好。让产线自己证明，我们能把这一张，做成每一张。")),
            new FounderScene("delivery1","产品有了去处","第一章 / 首批交付","晨湾 · 渲染工作室","studio","首单结算 · 按实际交付结果反馈",
                new FounderLine("studio","卡已经装进工作站了。昨晚那一场景，现在终于能完整跑出来。","relieved"),
                new FounderLine("partner","{delivery}","thinking"),
                new FounderLine("player","我们会把这次记录留下。做得好的地方保留，出了偏差的地方修正。"),
                new FounderLine("studio","等游戏上线，制作名单里给你们留个位置。","relieved"),
                new FounderLine("partner","下一位客户要的是一整间店的装机量。我们的工作，开始从样品走向生意了。"),
                new FounderLine("player","那就先算清物料和节拍，再谈扩产。")),
            new FounderScene("contract2","让承诺具备规模","第一章 / 批量合作","星河 · 装机现场","gaming","批量合同 · 库存、散热器与交期需重新核对",
                new FounderLine("gaming","开业那天，每台机器都得能用。我不希望宣传打出去了，设备还在路上。","questioning"),
                new FounderLine("player","我们先核对物料，再按产线节拍承诺日期。"),
                new FounderLine("partner","首单证明了能做，这一单要证明能重复。别忘了补齐散热器。","thinking"),
                new FounderLine("gaming","外观可以朴素些，稳定性和维护要说到做到。"),
                new FounderLine("player","型号和验收方式写进合同。设备交出去之后，合作才刚开始。"),
                new FounderLine("partner","核对资料卡。准备好，再签字。")),
            new FounderScene("ai_invite","留下一部分自己的卡","第二章 / 算力时代","深脉 · 集群验证室","ai_lab","合作目标 · 4 张有效显卡，完成 3 次付费渲染结算",
                new FounderLine("research","我们看过你们的交付记录。现在需要一个能持续运行、也能维护的小型计算集群。","thinking"),
                new FounderLine("player","是购买设备，还是购买计算服务？"),
                new FounderLine("research","先做渲染验证。至少四张有效卡，供电、散热到位，完成三轮正式服务结算。"),
                new FounderLine("partner","卖出显卡能回笼现金。留在机柜里，收入来得慢，却会持续。我们要选好投入节奏。","thinking"),
                new FounderLine("player","先用自己的产品建立验证机柜。所有收入和运营费，都走现有账本。"),
                new FounderLine("research","通过后，我们再谈更长的合作。大型 AI 项目的第一道门槛，是可靠运行。")),
            new FounderScene("compute","一张卡之外的生意","第二章 / 算力时代","星铸 · 算力机房","compute","验证完成 · 经营收入仍以正式账本为准",
                new FounderLine("research","三轮服务都完成了。我们拿到的不只是一批设备，还有一个能够持续交付结果的团队。","relieved"),
                new FounderLine("partner","机柜里的卡还属于我们。每次运行，都在产生服务收入，也在消耗电力和维护资源。","thinking"),
                new FounderLine("player","以前我们计算一张卡的成本。现在要计算一整套系统能否长期运转。"),
                new FounderLine("research","规模再扩大，最先遇到的可能不是芯片性能，而是能源。"),
                new FounderLine("partner","那会是另一种责任。我们先把眼前的系统经营可靠。"),
                new FounderLine("player","从今天起，星铸既制造产品，也交付算力。")),
            new FounderScene("horizon","下一处现场","后续篇章 / 研究预告","联合能源研究站 · 远程简报","energy","后续内容预告 · 能源转换与轨道工业尚未开放",
                new FounderLine("research","我们正在研究用计算控制新型储能材料。计算不会凭空产生能量，但可能改变能量被利用的方式。","thinking"),
                new FounderLine("player","如果要稳定一座设施，设备可靠只是第一步。供电、控制和维护必须放在一起设计。"),
                new FounderLine("partner","等我们走到那一步，要回答的问题会更大：这座工厂靠什么运行，又由谁承担风险。","thinking"),
                new FounderLine("research","研究站还收到了一份深空材料分析。它的内部结构，有一些异常的规律。","questioning"),
                new FounderLine("player","先把数据保存好。下一段路，也从一个能被验证的问题开始。"),
                new FounderLine("partner","旧员工证别换了。以后到了轨道上，我还想认得出最初的我们。","relieved")),
            new FounderScene("product","第一次写下型号","产品支线 / 自有设计","星铸 · 产品设计台","design","{product} · 具体外观与性能以产品页实时预览为准",
                new FounderLine("partner","这是我们第一次把客户需求写进自己的型号。","thinking"),
                new FounderLine("player","显存、散热、市场和价格，每一个选择都得有理由。"),
                new FounderLine("partner","概念图可以表达方向。真正量产的配置，还是看产品页的实际预览。"),
                new FounderLine("player","让名字和设计一起被记住。下一步，把产线切换到这个型号。","relieved")),
            new FounderScene("recap","回到我们的现场","战役接续","星铸 · 测试车间","workshop","保留原有工厂、资金、库存与订单进度",
                new FounderLine("partner","之前的交付记录都在，工厂也还是原来的工厂。","relieved"),
                new FounderLine("player","那就从现在的问题继续。已经完成的事情，不必再做一次。"),
                new FounderLine("partner","剧情档案会留下我们的进度。先把眼前的订单和产线经营好。"))
        };
        public static FounderScene Find(string id)=>Scenes.FirstOrDefault(s=>s.Id==id);
        public static string DefaultName(int founder)=>founder==1?"沈知微":"林序";
        public static string PartnerName(int founder)=>founder==1?"林序":"沈知微";
        public static string Personalize(string scene,int line,int founder,string text)
        {
            if(scene=="intro"&&line==1)return founder==0?"以前我习惯亲自解决每一个产品问题。现在，得学会让整条产线和整个团队可靠运转。":"以前我负责把量产风险压下去。现在，得决定哪些风险值得我们一起承担。";
            if(scene=="first"&&line==1)return founder==0?"设计图上的每一处改动，现在都拿在手里了。":"我看过很多批次的测试。第一次，合格记录上写的是我们自己的名字。";
            if(scene=="compute"&&line==2)return founder==0?"以前我计算一张卡的成本。现在，要让一个不必事事由我动手的系统可靠运转。":"系统验证通过了。下一步，是在还不能完全确定未来的时候，为长期投入作决定。";
            return text;
        }
        public static string CleanName(string name,int founder)
        {
            var clean=new string((name??"").Trim().Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').Take(12).ToArray());
            return string.IsNullOrWhiteSpace(clean)?DefaultName(founder):clean;
        }
    }

    public sealed partial class WorldSimulation
    {
        public NarrativeProgress Narrative=>State.Hardware.Story;
        private void InitializeNarrative(bool restored)
        {
            var h=State.Hardware;
            // Unity serializes null inline classes as default objects; a zero version also means pre-VN data.
            if(h.Story==null||h.Story.Version==0)h.Story=new NarrativeProgress{Version=1,Legacy=restored};
            var n=h.Story;n.Completed??=new List<string>();n.Pending??=new List<string>();n.History??=new List<string>();
            if(restored&&n.Legacy&&!n.IdentityChosen)
            {
                if(h.FirstProductTick>0)MarkNarrative("first");
                if(h.ScriptedDone>=1){MarkNarrative("contract1");MarkNarrative("delivery1");}
                if(h.ScriptedDone>=2)MarkNarrative("contract2");
            }
        }
        public void ChooseFounder(int founder,string name)
        {
            var n=Narrative;if(n.IdentityChosen)return;
            n.Founder=founder==1?1:0;n.FounderName=FounderStory.CleanName(name,n.Founder);n.IdentityChosen=true;
            QueueNarrative(n.Legacy?"recap":"intro");ObserveNarrative();
        }
        private void MarkNarrative(string id){if(!Narrative.Completed.Contains(id))Narrative.Completed.Add(id);}
        public void QueueNarrative(string id)
        {
            var n=Narrative;if(!n.IdentityChosen||n.Disabled||n.Completed.Contains(id)||n.Pending.Contains(id)||n.Active==id)return;
            n.Pending.Add(id);
        }
        public void CompleteNarrative(string id)
        {
            MarkNarrative(id);Narrative.Pending.Remove(id);
            if(Narrative.Active==id){Narrative.Active="";Narrative.Line=0;}
            ObserveNarrative();
        }
        public void ObserveNarrative()
        {
            var h=State.Hardware;var n=Narrative;if(!n.IdentityChosen||n.Disabled)return;
            if(h.FirstProductTick>0)QueueNarrative("first");
            if(h.ScriptedDone>=1)QueueNarrative("delivery1");
            if(h.ScriptedDone>=2&&!n.AiDeferred)QueueNarrative("ai_invite");
            if(n.ValidationAccepted&&n.ValidationCycles>=3)QueueNarrative("compute");
            if(n.Completed.Contains("compute"))QueueNarrative("horizon");
            if(h.Skus.Any(s=>s.Id.StartsWith("c")))
            {
                if(string.IsNullOrEmpty(n.ProductId)){var first=h.Skus.First(s=>s.Id.StartsWith("c"));n.ProductId=first.Id;n.LastProduct=first.Name;}
                QueueNarrative("product");
            }
        }
        public bool NarrativeQualityReady(int factory)=>!Narrative.IdentityChosen||Narrative.QualityMode==0||
            Narrative.QualityStreak>=3&&State.Nodes.Count(n=>n.Factory==factory&&n.Kind==BuildingKind.Tester&&!n.PendingRemove)>=(Narrative.QualityMode==2?2:1);
        private void ObserveNarrativeTest(BusinessNode node,bool passed)
        {
            var n=Narrative;if(!n.IdentityChosen||n.QualityMode==0||State.Hardware.ScriptedDone>0||node.Factory!=n.Factory)return;
            n.QualityStreak=passed?Math.Min(3,n.QualityStreak+1):0;
        }
        public void StartNarrativeValidation(int factory)
        {
            var n=Narrative;if(n.ValidationAccepted)return;n.Factory=factory;n.ValidationAccepted=true;n.ValidationCycles=0;
        }
        private void ObserveNarrativeCompute(int factory,int active)
        {
            var n=Narrative;if(n.ValidationAccepted&&n.Factory==factory&&active>=4)n.ValidationCycles=Math.Min(3,n.ValidationCycles+1);
        }
        public string NarrativeGoal()
        {
            var n=Narrative;if(!n.IdentityChosen)return "选择创始人，开启自己的事业";
            if(n.Disabled)return "自由经营";
            if(State.Hardware.ScriptedDone==0&&n.QualityMode>0)return "首批验收 · 连续合格 "+n.QualityStreak+" / 3 · 检测工位 "+State.Nodes.Count(x=>x.Factory==n.Factory&&x.Kind==BuildingKind.Tester&&!x.PendingRemove)+" / "+(n.QualityMode==2?2:1);
            if(n.ValidationAccepted&&!n.Completed.Contains("compute"))return "算力验证 · 有效卡 "+EffectiveCards(n.Factory)+" / 4 · 服务结算 "+n.ValidationCycles+" / 3";
            return n.Completed.Contains("horizon")?"创业 / 算力篇已完成 · 继续经营，准备走向能源时代":State.Hardware.ScriptedDone<2?"完成教学客户交付，建立品牌信誉":"会见研究团队，建立自己的算力机柜";
        }
    }
}
