using System;
using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Product page (SKU list + slot designer with live preview) and research page.
    public sealed partial class HardwareHud
    {
        private int designMemory=1,designCooler=HardwareCatalog.Heat,designShell,designPricePercent=100;
        private int designGeneration,designCoreBin,designFans,designLength; private bool designBackplate; private TargetMarket designMarket;
        private string designName="";
        private TMP_Text designStats;
        private UnityEngine.UI.RawImage designPreview;
        private WorldCommand DesignCommand(WorldSimulation w)
        {
            var c=new WorldCommand{Kind=CommandKind.DesignSku,Memory=designMemory,Cooler=designCooler,Shell=designShell,Generation=designGeneration,CoreBin=designCoreBin,Market=designMarket,Fans=designFans,Length=designLength,Backplate=designBackplate,Name=string.IsNullOrWhiteSpace(designName)?"星铸 S"+(designGeneration==0?w.State.Hardware.CoreGen:designGeneration)+" 新品 "+w.State.Hardware.NextSku:designName.Trim()};
            c.Price=HardwareSku.SuggestedPrice(w.Draft(c))*designPricePercent/100/100*100;return c;
        }
        private static string Hex(Color c)=>ColorUtility.ToHtmlStringRGB(c);
        private void ProductDrawer(WorldSimulation w)
        {
            var h=w.State.Hardware;
            Label(content,"<color=#16B8D1>在售型号 · 总装切换型号会等待当前批次完成</color>",16,24);
            foreach(var s in h.Skus.Where(s=>!s.Retired))SkuCard(w,s);
            Label(content,"<color=#16B8D1>新型号设计 · 第 "+h.CoreGen+" 代核心</color>",16,24);
            var holder=Box("SKU preview",content);holder.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=170;
            var image=new GameObject("SKU preview image",typeof(RectTransform),typeof(UnityEngine.UI.RawImage));image.transform.SetParent(holder,false);Stretch((RectTransform)image.transform,4);
            designPreview=image.GetComponent<UnityEngine.UI.RawImage>();designPreview.raycastTarget=false;
            var core=Row(Box("Design core",content,false));
            Button(core,"核心世代",()=>{designGeneration=(designGeneration==0?w.State.Hardware.CoreGen:designGeneration)%w.State.Hardware.CoreGen+1;RefreshDesigner(w);},0,40,"design:generation");
            Button(core,"核心体质",()=>{designCoreBin=(designCoreBin+1)%4;RefreshDesigner(w);},0,40,"design:core");
            Button(content,"目标市场",()=>{designMarket=(TargetMarket)(((int)designMarket+1)%5);RefreshDesigner(w);},0,40,"design:market");
            var memory=Row(Box("Design memory",content,false));Label(memory,"显存",15,40,52);
            foreach(var m in new[]{1,2,4}){var value=m;Button(memory,(w.MemoryUnlocked(m)?"":"锁 ")+m+" 颗",()=>{if(w.MemoryUnlocked(value)){designMemory=value;RefreshDesigner(w);}else Toast("需要研发"+(value==2?"「双显存布局」":"「四显存布局」"));},64,40,"design:memory:"+m);}
            var cooler=Row(Box("Design cooler",content,false));Label(cooler,"散热",15,40,52);
            foreach(var k in new[]{HardwareCatalog.Heat,HardwareCatalog.QuietHeat,HardwareCatalog.TowerHeat}){var value=k;Button(cooler,(w.CoolerUnlocked(k)?"":"锁 ")+(k==HardwareCatalog.Heat?"标准":k==HardwareCatalog.QuietHeat?"低噪":"塔式"),()=>{if(w.CoolerUnlocked(value)){designCooler=value;RefreshDesigner(w);}else Toast("需要研发「塔式散热」");},64,40,"design:cooler:"+k);}
            var look=Row(Box("Design look",content,false));
            Button(look,"外壳配色",()=>{designShell=(designShell+1)%HardwareFactoryView.ShellPalette.Length;RefreshDesigner(w);},0,40,"design:shell");
            Button(look,"定价 −5%",()=>{designPricePercent=Math.Max(50,designPricePercent-5);RefreshDesigner(w);},0,40,"design:cheaper");
            Button(look,"定价 +5%",()=>{designPricePercent=Math.Min(200,designPricePercent+5);RefreshDesigner(w);},0,40,"design:pricier");
            var shape=Row(Box("Design shape",content,false));
            Button(shape,"风扇数量",()=>{designFans=designFans%3+1;RefreshDesigner(w);},0,40,"design:fans");
            Button(shape,"卡长",()=>{designLength=(designLength+1)%3;RefreshDesigner(w);},0,40,"design:length");
            Button(shape,"背板",()=>{designBackplate=!designBackplate;RefreshDesigner(w);},0,40,"design:backplate");
            NameInput(w);
            designStats=Label(content,"",15,148);
            Button(content,"上市新型号",()=>{var r=game.Submit(DesignCommand(w));if(r.Success){designName="";RebuildDrawer();}},0,44,"design:launch");
            Button(content,"建造丐版包装机",()=>game.BeginBuild(BuildingKind.Packaging,"budget-pack"),actionId:"drawer:budget");
            Button(content,"分流 C 档：建造降级包装机",()=>game.BeginBuild(BuildingKind.Packaging,"c-bin-pack"),actionId:"design:c-bin");
            Button(content,"建造返修台（次品 + 显存 → 待检）",()=>game.BeginBuild(BuildingKind.Rework));
        }
        private void NameInput(WorldSimulation w)
        {
            var root=Box("Design name",content);root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=40;root.GetComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.1f);
            var text=Label(root,designName,16,36);Stretch((RectTransform)text.transform,8);
            var placeholder=Label(root,"型号名称（留空自动命名）",14,36);Stretch((RectTransform)placeholder.transform,8);placeholder.color=new Color(.47f,.61f,.7f);
            var input=root.gameObject.AddComponent<TMP_InputField>();input.textViewport=(RectTransform)text.transform;input.textComponent=(TextMeshProUGUI)text;input.placeholder=placeholder;
            input.characterLimit=16;input.text=designName;input.onValueChanged.AddListener(v=>designName=v);
        }
        private void SkuCard(WorldSimulation w,SkuDefinition s)
        {
            var style=HardwareVisualCatalog.Get(HardwareCatalog.Standard,w.IsQuiet(s.Id));var card=Column(Box("Product card "+s.Id,content));
            Strip(card,HardwareFactoryView.ShellPalette[s.Shell&7]);
            var stock=w.FinishedStock(game.Factory).Where(g=>g.Material==HardwareCatalog.Standard&&g.SpecId==s.Id).ToArray();
            var grades=string.Join(" ",new[]{1,2,3}.Select(g=>"<color=#"+Hex(HardwareFactoryView.GradeColor(g))+">"+WorldSimulation.GradeName(g)+stock.Count(x=>x.Grade==g)+"</color>"));
            var gens=string.Join("/",stock.GroupBy(g=>g.Gen).OrderByDescending(g=>g.Key).Select(g=>"G"+g.Key+"×"+g.Count()));
            var making=w.State.Nodes.Count(n=>n.Factory==game.Factory&&n.Kind==BuildingKind.GPUAssembly&&n.RecipeId==HardwareSku.RecipeId(s.Id));
            Label(card,"<b>"+s.Name+"</b>  <color=#"+Hex(style.Color)+">[ "+s.Code+" ]</color>  评测 <b>"+s.Score+"</b>",16,26);
            Label(card,"G"+s.Gen+" · "+HardwareSku.CoreName(s.CoreBin)+" · "+HardwareSku.MarketName(s.Market)+"\n"+s.Memory+" 颗显存 · "+HardwareSku.CoolerName(s.Cooler)+" · 性能 "+HardwareSku.Perf(s,s.Gen)+"\n合同基础价 "+HardwareGame.Money(s.BasePrice)+" · 理论成本 "+HardwareGame.Money(w.SkuReplacementCost(s))+"\n库存 "+grades+(gens==""?"":" · "+gens)+" · 在产总装 "+making,14,86);
            var row=Row(Box("SKU actions",card,false));
            Button(row,"总装改产此型号",()=>{foreach(var n in w.State.Nodes.Where(n=>n.Factory==game.Factory&&n.Kind==BuildingKind.GPUAssembly))game.Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=n.Id,Recipe=HardwareSku.RecipeId(s.Id)});RebuildDrawer();},0,36,"design:produce:"+s.Id);
            Button(row,"下架",()=>{game.Submit(new WorldCommand{Kind=CommandKind.RetireSku,Spec=s.Id});RebuildDrawer();},70,36,"design:retire:"+s.Id);
        }
        private void RefreshDesigner(WorldSimulation w)
        {
            if(designStats==null)return;
            var c=DesignCommand(w);var draft=w.Draft(c);var suggested=HardwareSku.SuggestedPrice(draft);
            designStats.text="<b>"+draft.Name+"</b> · G"+draft.Gen+" · "+HardwareSku.CoreName(draft.CoreBin)+" · "+HardwareSku.MarketName(draft.Market)+"\n"+HardwareFactoryView.ShellNames[draft.Shell]+" · "+(draft.Fans==0?"自动":draft.Fans.ToString())+" 风扇 · "+new[]{"标准卡长","加长","超长"}[draft.Length]+" · 背板"+(draft.Backplate?"有":"无")+"（外观不影响属性）\n性能 <b>"+HardwareSku.Perf(draft,draft.Gen)+"</b> · 噪音 "+HardwareSku.Noise(draft.Cooler)+"dB · 理论成本 "+HardwareGame.Money(w.SkuReplacementCost(draft))
                +"\n定价 <b>"+HardwareGame.Money(draft.BasePrice)+"</b>（建议 "+HardwareGame.Money(suggested)+"，"+designPricePercent+"%）\n预估评测 <b>"+draft.Score+"</b> 分 · 评测越高越容易拿到匹配客户的订单";
            if(designPreview!=null)designPreview.texture=game.View.SkuPreview(HardwareVisualKey.Of(HardwareCatalog.RawGPU,draft,WorldSimulation.GradeA));
        }
        private void ResearchDrawer(WorldSimulation w)
        {
            var h=w.State.Hardware;
            Button(content,"改造本厂板卡 / 总装设备 · 每台 "+HardwareGame.Money(w.Settings.Hardware.RetoolCost),()=>{foreach(var n in w.State.Nodes.Where(n=>n.Factory==game.Factory&&w.NeedsRetooling(n)&&n.RetoolGen==0).ToArray())game.Submit(new WorldCommand{Kind=CommandKind.Retool,Node=n.Id});},0,44,"research:retool");
            if(!w.State.Nodes.Any(n=>n.Kind==BuildingKind.Lab))Button(content,"建造研发实验室（"+HardwareGame.Money(w.Settings.Cost(BuildingKind.Lab))+"）",()=>game.BeginBuild(BuildingKind.Lab),0,44,"research:lab");
            foreach(var r in w.ResearchOptions())
            {
                var id=r.Id;var active=h.ResearchActive==id;var card=Column(Box("Research "+id,content));
                Strip(card,id.StartsWith(WorldSimulation.GenPrefix)?Gold:accent);
                Label(card,"<b>"+r.Name+"</b>  <size=13>"+r.Points+" 点 · "+HardwareGame.Money(r.Cost)+"</size>\n<size=13><color=#9FB2BE>"+r.Detail+"</color></size>",15,58);
                if(active)Label(card,"<color=#38C785>研发中</color>",14,22);
                else Button(card,w.ResearchAvailable(r)?"开始研发":"前置未完成",()=>{game.Submit(new WorldCommand{Kind=CommandKind.StartResearch,Recipe=id});RebuildDrawer();},0,36,"research:start:"+id);
            }
            var done=h.ResearchDone.Select(id=>w.Research(id)?.Name).Where(n=>n!=null).ToArray();
            if(done.Length>0)Label(content,"<color=#7F8E99>已完成："+string.Join("、",done)+"</color>",13,40);
        }
        private string ResearchSummary(WorldSimulation w)
        {
            var h=w.State.Hardware;var active=w.Research(h.ResearchActive);var rate=w.LabPointsPerMinute();
            var labs=w.State.Nodes.Count(n=>n.Kind==BuildingKind.Lab);
            return "已研发 <b>G"+h.CoreGen+"</b> · 行业 G"+Math.Max(h.CoreGen,h.IndustryGen)+(h.NextGenerationTick>0?" · 下代 "+Math.Max(0,(h.NextGenerationTick-w.State.Tick)/60)+" 秒":"")+(w.ProcessPenalty(h.CoreGen)>0?" · 爬坡 A 档 -"+w.ProcessPenalty(h.CoreGen)+"%":"")
                +"\n实验室 "+labs+" 座 · "+rate+" 点/分钟"+(active!=null?"\n研发「"+active.Name+"」"+h.ResearchProgress+" / "+active.Points+(rate>0?" · 约 "+Math.Max(0,(active.Points-h.ResearchProgress)/Math.Max(1,rate))+" 分钟":""):"\n<color=#E8D27A>实验室空闲：选择下方项目</color>")
                +"\n旧世代残值：落后一代 "+w.GenerationValuePercent(Math.Max(1,h.CoreGen-1))*100/Math.Max(1,w.GenerationValuePercent(h.CoreGen))+"%";
        }
        private void Toast(string message){game.Message=message;}
    }
}
