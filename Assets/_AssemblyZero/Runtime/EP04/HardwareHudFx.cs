using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AssemblyZero.Unity.EP04
{
    // Presentation-only feedback: icon chips, money roll-up and the banknote cash-in flight.
    public sealed partial class HardwareHud
    {
        private TMP_Text moneyText,netText,repText,eventText,timeText,factoryText;
        private RectTransform moneyChip,eventChip,fxLayer;
        private double displayedMoney=double.NaN;private long pendingCash;
        public int BanknotesInFlight {get;private set;}
        public int BanknotesSpawned {get;private set;}
        public long DisplayedMoney=>double.IsNaN(displayedMoney)?0:(long)displayedMoney;
        private TMP_Text Chip(Transform row,string icon,Color tint,float width,out RectTransform chip)
        {
            chip=Box("Chip "+icon,row);chip.GetComponent<Image>().color=new Color(.09f,.15f,.21f,.95f);
            var e=chip.gameObject.AddComponent<LayoutElement>();e.preferredWidth=width;e.minWidth=Mathf.Min(width,70);
            Icon(chip,icon,tint,22,Vector2.zero,new Vector2(0,.5f),new Vector2(16,0));
            var t=Label(chip,"",17,40);var rt=(RectTransform)t.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=new Vector2(32,0);rt.offsetMax=new Vector2(-6,0);
            t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=TextWrappingModes.NoWrap;Destroy(t.GetComponent<LayoutElement>());return t;
        }
        private Image Icon(RectTransform parent,string id,Color tint,float size,Vector2 offset)=>Icon(parent,id,tint,size,offset,new Vector2(.5f,.5f),Vector2.zero);
        private Image Icon(RectTransform parent,string id,Color tint,float size,Vector2 offset,Vector2 anchor,Vector2 anchorOffset)
        {
            var r=Box("Icon "+id,parent,false);r.anchorMin=r.anchorMax=anchor;r.sizeDelta=new Vector2(size,size);r.anchoredPosition=offset+anchorOffset;
            var image=r.gameObject.AddComponent<Image>();image.sprite=HardwareIcons.Get(id);image.color=tint;image.raycastTarget=false;image.preserveAspect=true;return image;
        }
        // A layout-driven row element holding a single icon.
        private Image IconCell(Transform parent,string id,Color tint,float size)
        {
            var cell=Box("Icon cell",parent,false);var e=cell.gameObject.AddComponent<LayoutElement>();e.preferredWidth=e.minWidth=size;e.preferredHeight=e.minHeight=size;
            var image=cell.gameObject.AddComponent<Image>();image.sprite=HardwareIcons.Get(id);image.color=tint;image.raycastTarget=false;image.preserveAspect=true;return image;
        }
        private RectTransform FxLayer()
        {
            if(fxLayer!=null)return fxLayer;fxLayer=Box("Feedback layer",canvas.transform,false);Stretch(fxLayer);return fxLayer;
        }
        private Vector2 ToCanvas(Vector2 screen){RectTransformUtility.ScreenPointToLocalPointInRectangle(FxLayer(),screen,null,out var local);return local;}
        private Vector2 WorldToCanvas(Vector3 world)=>ToCanvas(game.ViewCamera.WorldToScreenPoint(world));
        private Vector2 RectCenter(RectTransform r)=>ToCanvas(RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)));
        private void UpdateMoneyRoll()
        {
            if(moneyText==null||game.World==null)return;var target=game.World.State.Money-pendingCash;
            if(double.IsNaN(displayedMoney)||Mathf.Abs((float)(target-displayedMoney))<1)displayedMoney=target;
            else displayedMoney+= (target-displayedMoney)*(1-Mathf.Exp(-Time.unscaledDeltaTime*9));
            moneyText.text="<b>"+HardwareGame.Money((long)System.Math.Round(displayedMoney))+"</b>";
        }
        public void SnapMoney(){pendingCash=0;displayedMoney=double.NaN;BanknotesInFlight=0;if(fxLayer!=null)foreach(Transform c in fxLayer)Destroy(c.gameObject);}
        public TMP_Text FloatText(Vector2 at,string text,Color color,float size=34,float rise=90,float duration=1.3f)
        {
            var r=Box("Float text",FxLayer(),false);r.sizeDelta=new Vector2(420,60);r.anchoredPosition=at;
            var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=game.Font;t.fontSize=size;t.color=color;t.text=text;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.fontStyle=FontStyles.Bold;
            t.outlineWidth=.18f;t.outlineColor=new Color32(8,14,20,220);
            r.localScale=Vector3.zero;
            HardwareTween.Run(r,duration,p=>{r.anchoredPosition=at+Vector2.up*rise*p;r.localScale=Vector3.one*(p<.15f?HardwareTween.OutBack(p/.15f):1);t.alpha=p<.7f?1:1-(p-.7f)/.3f;},HardwareTween.OutQuad,()=>Destroy(r.gameObject));
            return t;
        }
        private Vector3 SalesPoint(int factory)
        {
            var sales=game.World.State.Nodes.FirstOrDefault(n=>n.Factory==factory&&n.Kind==BuildingKind.Sales);var f=game.World.Factory(factory);
            return sales==null?game.CameraFocus:new Vector3((f.X+sales.X)*1.2f,2.5f,(f.Z+sales.Z)*1.2f);
        }
        // Settlement payoff: register sound, big floating amount, banknotes flying into the money chip.
        public void ShowCashIn(Settlement s,int factory)
        {
            if(canvas==null||moneyChip==null)return;
            var visible=factory==game.Factory;var start=visible?WorldToCanvas(SalesPoint(factory)):Vector2.zero;
            var grade=s.Grade;var tint=grade==DeliveryGrade.Early?Gold:grade==DeliveryGrade.OnTime?new Color(.55f,.92f,.62f):new Color(.95f,.7f,.35f);
            HardwareAudio.Play("cash");
            FloatText(start+Vector2.up*30,"+"+HardwareGame.Money(s.Income),tint,grade==DeliveryGrade.Early?44:38,120,1.6f);
            if(grade==DeliveryGrade.Early)FloatText(start+Vector2.up*78,"提前交付 +15%",Gold,22,110,1.6f);
            var bills=Mathf.Clamp((int)(s.Income/20000)+(grade==DeliveryGrade.Early?6:grade==DeliveryGrade.OnTime?4:2),4,grade==DeliveryGrade.Late?8:16);
            var share=s.Income/bills;var remainder=s.Income-share*bills;pendingCash+=s.Income;
            var end=RectCenter(moneyChip);
            for(var i=0;i<bills;i++)
            {
                var amount=share+(i==bills-1?remainder:0);var index=i;
                var bill=Box("Banknote",FxLayer(),false);bill.sizeDelta=new Vector2(44,24);bill.anchoredPosition=start;
                var image=bill.gameObject.AddComponent<Image>();image.sprite=HardwareIcons.Get("bill");image.color=grade==DeliveryGrade.Early&&i%3==0?Gold:new Color(.36f,.82f,.48f);image.raycastTarget=false;
                var spread=new Vector2(Random.Range(-160f,160f),Random.Range(80f,220f));var control=Vector2.Lerp(start,end,.35f)+spread;var spin=Random.Range(-540f,540f);
                BanknotesInFlight++;BanknotesSpawned++;bill.localScale=Vector3.zero;
                HardwareTween.Run(bill,.85f+i*.02f,p=>{bill.anchoredPosition=HardwareTween.Bezier(start,control,end,p);bill.localRotation=Quaternion.Euler(0,0,spin*(1-p));bill.localScale=Vector3.one*Mathf.Lerp(1.3f,.7f,p)*(p<.1f?p/.1f:1);},HardwareTween.InOutQuad,()=>
                {
                    Destroy(bill.gameObject);BanknotesInFlight--;pendingCash=System.Math.Max(0,pendingCash-amount);
                    HardwareAudio.Play("coin",.8f,1+index*.015f);HardwareTween.Punch(moneyChip,.08f,.18f);
                },i*.07f);
            }
        }
        public void ShowPenalty(long amount)
        {
            if(canvas==null||moneyChip==null||amount<=0)return;HardwareAudio.Play("penalty");
            var at=RectCenter(moneyChip);var t=FloatText(at+Vector2.down*30,"-"+HardwareGame.Money(amount),Bad,28,-90,1.4f);
            HardwareTween.Punch(moneyChip,.1f,.3f);
        }
        // Radial progress ring: dim track plus a filled arc, with an optional centred number.
        private (Image Fill,TMP_Text Text) Ring(Transform parent,float size,Color color,int fontSize=14)
        {
            var cell=Box("Ring",parent,false);var e=cell.gameObject.AddComponent<LayoutElement>();e.preferredWidth=e.minWidth=size;e.preferredHeight=e.minHeight=size;
            // Rows force-expand children vertically, so keep the dial square inside the stretched cell.
            var square=Box("Ring square",cell,false);Stretch(square);var fit=square.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=1;
            var track=square.gameObject.AddComponent<Image>();track.sprite=HardwareIcons.Get("ring");track.color=new Color(1,1,1,.12f);track.raycastTarget=false;
            var fillRect=Box("Ring fill",square,false);Stretch(fillRect);var fill=fillRect.gameObject.AddComponent<Image>();fill.sprite=HardwareIcons.Get("ring");fill.color=color;fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Radial360;fill.fillOrigin=(int)Image.Origin360.Top;fill.fillClockwise=false;fill.raycastTarget=false;
            var t=Label(square,"",fontSize,size);Stretch((RectTransform)t.transform);t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;Destroy(t.GetComponent<LayoutElement>());
            return (fill,t);
        }
        // KPI tile: icon, large number and caption. Returns the number text for refresh.
        private TMP_Text Tile(Transform row,string icon,Color tint,string caption)
        {
            var tile=Box("KPI "+caption,row);tile.GetComponent<Image>().color=new Color(.09f,.15f,.21f,.95f);var e=tile.gameObject.AddComponent<LayoutElement>();e.flexibleWidth=1;e.preferredHeight=e.minHeight=64;
            Icon(tile,icon,tint,22,Vector2.zero,new Vector2(0,1),new Vector2(18,-18));
            var c=Label(tile,caption,12,18);var cr=(RectTransform)c.transform;cr.anchorMin=new Vector2(0,1);cr.anchorMax=Vector2.one;cr.pivot=new Vector2(.5f,1);cr.offsetMin=new Vector2(34,-28);cr.offsetMax=new Vector2(-6,-8);c.color=new Color(.6f,.7f,.76f);Destroy(c.GetComponent<LayoutElement>());
            var v=Label(tile,"",20,30);var vr=(RectTransform)v.transform;vr.anchorMin=Vector2.zero;vr.anchorMax=new Vector2(1,0);vr.pivot=new Vector2(.5f,0);vr.offsetMin=new Vector2(10,6);vr.offsetMax=new Vector2(-6,36);v.fontStyle=FontStyles.Bold;v.textWrappingMode=TextWrappingModes.NoWrap;Destroy(v.GetComponent<LayoutElement>());
            return v;
        }
        private HardwareLineChart Chart(Transform parent,float height)
        {
            var box=Box("Chart",parent);box.GetComponent<Image>().color=new Color(.04f,.08f,.12f,.95f);var e=box.gameObject.AddComponent<LayoutElement>();e.preferredHeight=e.minHeight=height;
            var r=Box("Chart graphic",box,false);Stretch(r,8);var chart=r.gameObject.AddComponent<HardwareLineChart>();chart.raycastTarget=false;return chart;
        }
        public HardwareLineChart RevenueChart {get;private set;}
        private HardwareLineChart priceChart;
        private RectTransform tooltip;private TMP_Text tooltipTitle,tooltipBody;private (Image Fill,TMP_Text Text) tooltipRing;private int tooltipNode=-1;
        public bool TooltipVisible=>tooltip!=null&&tooltip.gameObject.activeSelf;
        private void BuildTooltip()
        {
            tooltip=Box("Hover tooltip",canvas.transform);tooltip.anchorMin=tooltip.anchorMax=Vector2.zero;tooltip.pivot=new Vector2(0,1);tooltip.sizeDelta=new Vector2(280,124);
            tooltip.GetComponent<Image>().raycastTarget=false;var row=Row(Box("Tooltip row",tooltip,false));Stretch(row,6);Destroy(row.GetComponent<LayoutElement>());
            tooltipRing=Ring(row,64,Good,15);var column=Column(Box("Tooltip text",row,false));column.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
            tooltipTitle=Label(column,"",17,24);tooltipBody=Label(column,"",13,70);tooltip.gameObject.SetActive(false);
        }
        private void UpdateTooltip()
        {
            if(tooltip==null)BuildTooltip();var w=game.World;var n=w.Node(game.HoverNode);
            var show=n!=null&&!game.Paused&&!game.Placing.HasValue&&!ProcurementOpen&&!EquipmentLibraryOpen;if(tooltip.gameObject.activeSelf!=show)tooltip.gameObject.SetActive(show);if(!show){tooltipNode=-1;return;}
            var local=ToCanvasRoot(game.HoverScreen+new Vector2(22,-18));var root=(RectTransform)canvas.transform;
            local.x=Mathf.Min(local.x,root.rect.width-tooltip.sizeDelta.x-8);local.y=Mathf.Max(local.y,tooltip.sizeDelta.y+8);tooltip.anchoredPosition=local;tooltip.SetAsLastSibling();
            if(tooltipNode==n.Id&&Time.frameCount%10!=0)return;tooltipNode=n.Id;
            var recipe=game.Config.Economy.Recipe(n.RecipeId);var u=recipe==null?0:w.Utilization(n);var bottleneck=w.Bottleneck(n.Factory)?.Id==n.Id;
            tooltipRing.Fill.fillAmount=u/100f;tooltipRing.Fill.color=bottleneck?Bad:n.RunState==NodeState.Working?Good:n.RunState==NodeState.Blocked?Bad:Gold;tooltipRing.Text.text=recipe==null?"—":"<b>"+u+"%</b>";
            tooltipTitle.text="<b>"+game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name+"</b> L"+n.Level+(bottleneck?" <color=#EB5545>瓶颈</color>":"");
            tooltipBody.text=StateLabel(n.RunState)+(recipe==null?"":"  "+recipe.Seconds*100/game.Config.Economy.UpgradePercent[n.Level-1]/100f+"s/件")+"\n"
                +(recipe==null?"库存 "+n.Output.Count:string.Join("  ",recipe.Inputs.Select(i=>HardwareCatalog.Name(i.Material).Replace("星铸 S1 ","")+" "+n.Input.Count(g=>g.Material==i.Material))))
                +"\n<color=#7F8E99>点击查看 · 输出 "+n.Output.Count+"</color>";
        }
        private Vector2 ToCanvasRoot(Vector2 screen){RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,screen,null,out var local);var r=((RectTransform)canvas.transform).rect;return local-r.min;}
        private static float[] Deltas(System.Collections.Generic.List<StatsBucket> stats,System.Func<StatsBucket,long> value,int count)
        {
            var start=Mathf.Max(1,stats.Count-count);var result=new float[Mathf.Max(0,stats.Count-start)];
            for(var i=start;i<stats.Count;i++)result[i-start]=(value(stats[i])-value(stats[i-1]))/100f;return result;
        }
        private TMP_Text lensLabel,masterLabel,sfxLabel,ambientLabel;
        private static string Percent(float v)=>Mathf.RoundToInt(v*100)+"%";
        private void RefreshSettings()
        {
            if(lensLabel!=null)lensLabel.text="微距镜头 · "+(game.Lens==null?"—":game.Lens.Strength<=0?"关":Percent(game.Lens.Strength));
            if(game.Audio==null||masterLabel==null)return;
            masterLabel.text="主音量 "+Percent(game.Audio.Master);sfxLabel.text="音效 "+Percent(game.Audio.Sfx);ambientLabel.text="环境 "+Percent(game.Audio.Ambient);
        }
        private RectTransform showcaseBar;
        private readonly System.Collections.Generic.List<GameObject> hiddenForShowcase=new System.Collections.Generic.List<GameObject>();
        private void BuildShowcaseBar()
        {
            showcaseBar=Box("Showcase bar",canvas.transform);Anchor(showcaseBar,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-420,16),new Vector2(420,72));var row=Row(showcaseBar);
            Label(row,"<color=#16B8D1>展示模式</color> · Tab 切换机位 · F12 导出",15,40,260);
            Button(row,"切换机位",game.NextShowcaseShot,110);Button(row,"导出胶囊图",()=>game.StartCoroutine(game.ExportCapsules()),130,40,"capsule");Button(row,"退出展示",game.ExitShowcase,110);
            showcaseBar.gameObject.SetActive(false);
        }
        public void SetShowcase(bool on)
        {
            if(on)CloseProcurementOverview();
            if(on)CloseEquipmentLibrary();
            if(on)
            {
                hiddenForShowcase.Clear();
                foreach(Transform child in canvas.transform)if(child!=showcaseBar&&child!=modal&&child!=fxLayer&&child.gameObject.activeSelf){hiddenForShowcase.Add(child.gameObject);child.gameObject.SetActive(false);}
                showcaseBar.gameObject.SetActive(true);showcaseBar.SetAsLastSibling();SnapMoney();
            }
            else{foreach(var g in hiddenForShowcase)if(g!=null)g.SetActive(true);hiddenForShowcase.Clear();showcaseBar.gameObject.SetActive(false);Refresh();}
        }
        private bool drawerShown;private Vector2? drawerHome;
        private void AnimateDrawer(bool open)
        {
            if(open&&!drawerShown)
            {
                var r=drawer;var home=drawerHome??=r.anchoredPosition;r.anchoredPosition=home+Vector2.right*380;HardwareAudio.Play("whoosh",.5f);
                HardwareTween.Cancel(r);HardwareTween.Run(r,.22f,p=>r.anchoredPosition=home+Vector2.right*380*(1-p),HardwareTween.OutQuad,()=>r.anchoredPosition=home);
            }
            drawerShown=open;
        }
    }
}
