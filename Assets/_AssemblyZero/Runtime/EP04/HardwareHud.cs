using System;
using System.Linq;
using System.Collections.Generic;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareHud : MonoBehaviour
    {
        private HardwareGame game;private Canvas canvas;private RectTransform drawer,content,pause,modal,build,goals,notices,toast;
        private TMP_Text top,goal,notice,details,selection,guideAction,speedLabel,toastText;private int material=HardwareCatalog.Heat,quantity=6,delivery;private float zoom=1;private bool confirmNew;
        private readonly Color panel=new Color(.055f,.09f,.135f,.97f),ink=new Color(.88f,.94f,.97f),accent=HardwareVisualCatalog.Cyan;
        private static readonly Color Gold=new Color(.95f,.64f,.23f),Good=new Color(.22f,.78f,.52f),Bad=new Color(.92f,.33f,.27f);
        private RectTransform buildViewport;
        private Sprite roundedPanel;
        private Texture2D panelTexture;
        private float layoutWidth,toastTime,flashTime;
        private string orderSignature="";
        private readonly Dictionary<int,(TMP_Text Status,UnityEngine.UI.Image Fill)> orderCards=new Dictionary<int,(TMP_Text Status,UnityEngine.UI.Image Fill)>();
        private readonly Dictionary<int,UnityEngine.UI.Button> acceptButtons=new Dictionary<int,UnityEngine.UI.Button>();
        public static readonly string[] Pages={"工厂","订单","产品","采购","市场","运输","报表"};
        private Sprite PanelSprite()
        {
            if(roundedPanel!=null)return roundedPanel;
            panelTexture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Hardware UI rounded panel",filterMode=FilterMode.Bilinear};
            var pixels=new Color[1024];for(var y=0;y<32;y++)for(var x=0;x<32;x++)
            {
                var dx=Mathf.Max(7-x,x-24);var dy=Mathf.Max(7-y,y-24);
                var distance=new Vector2(Mathf.Max(0,dx),Mathf.Max(0,dy)).magnitude;
                pixels[y*32+x]=new Color(1,1,1,Mathf.Clamp01(7-distance));
            }
            panelTexture.SetPixels(pixels);panelTexture.Apply();roundedPanel=Sprite.Create(panelTexture,new Rect(0,0,32,32),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));return roundedPanel;
        }
        private void ProductCard(Transform parent,int material,string description)
        {
            var style=HardwareVisualCatalog.Get(material);var card=Box("Product card "+material,parent);var column=Column(card);
            Strip(column,style.Color);
            Label(column,HardwareVisualCatalog.Caption(material),16,26);
            Label(column,HardwareCatalog.Name(material)+"\n"+description,15,46);
        }
        private void Strip(Transform parent,Color color)
        {
            var strip=Box("SKU strip",parent);var image=strip.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;
            strip.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=4;
        }
        private void OnDestroy(){if(roundedPanel!=null)Destroy(roundedPanel);if(panelTexture!=null)Destroy(panelTexture);}
        public void Initialize(HardwareGame g)
        {
            game=g;canvas=new GameObject("Hardware HUD",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<Canvas>();canvas.transform.SetParent(transform,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            if(FindAnyObjectByType<EventSystem>()==null)new GameObject("Hardware EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var bar=Box("Status",canvas.transform);Anchor(bar,new Vector2(0,1),Vector2.one,new Vector2(0,-56),Vector2.zero);var row=Row(bar);top=Label(row,"",18,46,1000);
            Button(row,"工厂",()=>{game.ChangeFactory();},82);speedLabel=Button(row,"速度 1×",game.CycleSpeed,105,40,"speed").GetComponentInChildren<TMP_Text>();Button(row,"菜单",game.TogglePause,82);
            var nav=Box("Navigation",canvas.transform);Anchor(nav,new Vector2(0,1),new Vector2(0,1),new Vector2(8,-72-Pages.Length*64-16),new Vector2(68,-72));var list=Column(nav);
            foreach(var label in Pages){var target=label;Button(list,target,()=>{if(target=="工厂"){game.FocusFactory();game.Drawer="";RebuildDrawer();}else game.SetDrawer(target);},0,56,"nav:"+target);}
            goals=Box("Tracked order",canvas.transform);Anchor(goals,new Vector2(0,1),new Vector2(0,1),new Vector2(84,-308),new Vector2(354,-72));var gc=Column(goals);goal=Label(gc,"",16,152);Button(gc,"下一步",FollowGuide,0,48);guideAction=gc.GetComponentsInChildren<UnityEngine.UI.Button>().Last().GetComponentInChildren<TMP_Text>();
            drawer=Box("Context drawer",canvas.transform);Anchor(drawer,new Vector2(1,0),Vector2.one,new Vector2(-356,116),new Vector2(-12,-72));
            var scroll=drawer.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            var viewport=Box("Viewport",drawer);Stretch(viewport,8);viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            content=Box("Content",viewport,false);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;Column(content);var fit=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=content;
            build=Box("Construction",canvas.transform);Anchor(build,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-600,12),new Vector2(600,108));var buildColumn=Column(build);selection=Label(buildColumn,"建设设备 · 右键/中键拖动 · 滚轮缩放",16,24);buildViewport=Box("Build viewport",buildColumn,false);var viewportElement=buildViewport.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();viewportElement.preferredHeight=58;viewportElement.minHeight=58;
            buildViewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var buildings=game.Config.Economy.Hardware.Buildings;
            var buildRow=Row(Box("Categories",buildViewport,false));buildRow.anchorMin=new Vector2(0,0);buildRow.anchorMax=new Vector2(0,1);buildRow.pivot=new Vector2(0,.5f);buildRow.sizeDelta=new Vector2(buildings.Length*144+16,0);buildRow.anchoredPosition=Vector2.zero;
            var buildScroll=buildViewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();buildScroll.viewport=buildViewport;buildScroll.content=buildRow;buildScroll.horizontal=true;buildScroll.vertical=false;buildScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;buildScroll.scrollSensitivity=35;
            foreach(var b in buildings){var kind=b.Kind;Button(buildRow,b.Name+"\n"+HardwareGame.Money(b.Cost),()=>game.BeginBuild(kind),136,48,"build:"+kind);}
            notices=Box("Notifications",canvas.transform);Anchor(notices,new Vector2(1,1),Vector2.one,new Vector2(-356,-226),new Vector2(-12,-72));var nc=Column(notices);notice=Label(nc,"",16,92);Button(nc,"处理供料 / 查看采购",()=>game.SetDrawer("采购"),0,34);
            toast=Box("Settlement toast",canvas.transform);Anchor(toast,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-270,-212),new Vector2(270,-68));toast.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            toastText=Label(toast,"",17,140);Stretch((RectTransform)toastText.transform,12);toastText.alignment=TextAlignmentOptions.Center;toast.gameObject.SetActive(false);
            modal=Box("Modal blocker",canvas.transform);Stretch(modal);modal.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.45f);pause=Box("Pause",modal);Anchor(pause,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-220,-260),new Vector2(220,260));var pc=Column(pause);Label(pc,"已暂停 · 全部业务冻结",24,48);Button(pc,"继续",game.TogglePause);Button(pc,"保存显卡战役",()=>game.Save());Button(pc,"读取显卡战役",()=>game.Load());Button(pc,"新战役（再次点击确认）",()=>{if(confirmNew){game.NewGame();confirmNew=false;}else{confirmNew=true;game.Message="再次点击新战役确认；下次保存才更新显卡存档";}});Button(pc,"界面缩放 100 / 115 / 130%",()=>{zoom=zoom>=1.3f?1:zoom+.15f;scaler.referenceResolution=new Vector2(1920/zoom,1080/zoom);});Button(pc,"退出",()=>{game.Save();Application.Quit();});modal.gameObject.SetActive(false);
            InitializeNavigation();RebuildDrawer();Refresh();
        }
        private RectTransform Box(string name,Transform parent,bool background=true)
        {
            var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);var rt=(RectTransform)obj.transform;if(background){var image=obj.AddComponent<UnityEngine.UI.Image>();image.color=panel;image.sprite=PanelSprite();image.type=UnityEngine.UI.Image.Type.Sliced;}return rt;
        }
        private static void Anchor(RectTransform r,Vector2 min,Vector2 max,Vector2 a,Vector2 b){r.anchorMin=min;r.anchorMax=max;r.offsetMin=a;r.offsetMax=b;}
        private static void Stretch(RectTransform r,float inset=0)=>Anchor(r,Vector2.zero,Vector2.one,new Vector2(inset,inset),new Vector2(-inset,-inset));
        private RectTransform Column(RectTransform r)
        {
            var l=r.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();l.padding=new RectOffset(8,8,8,8);l.spacing=8;l.childControlHeight=true;l.childForceExpandHeight=false;l.childControlWidth=true;l.childForceExpandWidth=true;return r;
        }
        private RectTransform Row(RectTransform r){var l=r.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();l.spacing=8;l.padding=new RectOffset(8,8,4,4);l.childControlHeight=true;l.childForceExpandHeight=true;l.childControlWidth=true;l.childForceExpandWidth=false;var e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.preferredHeight=52;return r;}
        private TMP_Text Label(Transform parent,string text,int size=18,float height=40,float width=0)
        {
            var r=Box("Text",parent,false);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=game.Font;t.fontSize=size;t.color=ink;t.text=text;t.raycastTarget=false;t.overflowMode=TextOverflowModes.Ellipsis;t.textWrappingMode=TextWrappingModes.Normal;var e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.preferredHeight=height;e.minHeight=height;if(width>0){e.flexibleWidth=1;e.preferredWidth=width;}return t;
        }
        private UnityEngine.UI.Button Button(Transform parent,string label,Action action,float width=0,float height=40,string actionId=null)
        {
            var r=Box(label,parent);r.GetComponent<UnityEngine.UI.Image>().color=Color.white;var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();
            var state=b.colors;state.normalColor=new Color(.13f,.21f,.29f);state.highlightedColor=new Color(.18f,.35f,.43f);state.pressedColor=new Color(.06f,.48f,.58f);state.selectedColor=new Color(.12f,.36f,.45f);state.disabledColor=new Color(.1f,.14f,.18f,.55f);state.fadeDuration=.1f;b.colors=state;b.onClick.AddListener(()=>action());var e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.preferredHeight=height;e.minHeight=height;if(width>0)e.preferredWidth=width;else e.flexibleWidth=1;
            var t=Label(r,label,width>0?16:18,height);Stretch((RectTransform)t.transform,4);t.alignment=TextAlignmentOptions.Center;
            if(label.Contains("\n¥"))
            {
                var index=Array.FindIndex(game.Config.Economy.Hardware.Buildings,b=>label.StartsWith(b.Name+"\n"));
                BuildingIcon(r,index);
                var rt=(RectTransform)t.transform;rt.offsetMin=new Vector2(34,4);
            }
            if(actionId!=null)guideTargets[actionId]=r;
            return b;
        }
        private void BuildingIcon(Transform parent,int index)
        {
            var icon=Box("Equipment icon",parent,false);icon.anchorMin=icon.anchorMax=new Vector2(0,.5f);icon.sizeDelta=new Vector2(26,26);icon.anchoredPosition=new Vector2(18,0);
            void Line(float x,float y,float w,float h)
            {
                var r=Box("Icon detail",icon);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,y);r.GetComponent<UnityEngine.UI.Image>().color=accent;r.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            }
            switch(index)
            {
                case 0:Line(-9,0,3,22);Line(9,0,3,22);for(var y=-8;y<=8;y+=8)Line(0,y,18,2);break;
                case 1:Line(0,-3,22,15);Line(6,8,4,9);Line(0,-3,12,3);break;
                case 2:Line(-9,0,3,22);Line(9,0,3,22);Line(0,10,22,3);Line(0,0,10,5);break;
                case 3:Line(0,0,14,14);for(var i=-1;i<=1;i++){Line(i*6,10,2,5);Line(i*6,-10,2,5);Line(10,i*6,5,2);Line(-10,i*6,5,2);}break;
                case 4:Line(0,0,25,16);Line(-6,0,7,7);Line(6,0,7,7);Line(-13,0,2,24);break;
                case 5:Line(-9,0,3,22);Line(9,0,3,22);Line(0,10,22,3);Line(0,-2,13,2);break;
                case 6:Line(0,0,22,18);Line(0,0,3,20);Line(0,9,24,3);break;
                case 8:Line(0,-6,22,4);Line(-4,4,3,14);Line(4,8,12,3);Line(10,2,3,9);break;
                default:Line(0,8,26,5);Line(-9,-2,3,15);Line(9,-2,3,15);Line(0,-9,24,3);break;
            }
        }
        public void ShowPause(bool value){modal.gameObject.SetActive(value);if(value)modal.SetAsLastSibling();}
        public void ShowSettlement(Settlement s)
        {
            if(toast==null)return;
            var color=s.Grade==DeliveryGrade.Early?Good:s.Grade==DeliveryGrade.OnTime?accent:Gold;
            var grade=s.Grade==DeliveryGrade.Early?"提前交付 +15%":s.Grade==DeliveryGrade.OnTime?"准时交付":"迟交 -25%";
            toastText.text="<size=28><color=#"+ColorUtility.ToHtmlStringRGB(color)+">"+grade+"</color></size>\n"+s.Name+"\n收入 <b>"+HardwareGame.Money(s.Income)+"</b>   毛利 "+HardwareGame.Money(s.Margin)+"   口碑 "+(s.ReputationDelta>0?"+":"")+s.ReputationDelta;
            toast.gameObject.SetActive(true);toast.SetAsLastSibling();modal.SetAsLastSibling();toastTime=5;
        }
        public void FlashMarket()=>flashTime=3;
        private static string Phase(HardwareOrderPhase p)=>new[]{"可接","进行中","已配齐","已交付","已作废","已取消"}[(int)p];
        private static string Seconds(long ticks)=>Math.Max(0,ticks/60)+" 秒";
        private string Product(int material)=>material==HardwareCatalog.Quiet?"静音版":material==HardwareCatalog.Budget?"丐版":"标准版";
        public void RebuildDrawer()
        {
            ClearDrawerGuide();
            orderCards.Clear();acceptButtons.Clear();
            foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}details=null;drawer.gameObject.SetActive(game.Drawer!=""&&!game.Truck.Manual);if(!drawer.gameObject.activeSelf)return;
            Label(content,"<color=#16B8D1>//</color>  "+game.Drawer,24,42);Button(content,"关闭",()=>{game.Drawer="";RebuildDrawer();});
            details=Label(content,"",17,120);
            guideTargets["drawer:details"]=(RectTransform)details.transform;
            var w=game.World;var h=w.State.Hardware;
            if(game.Drawer=="设备")
            {
                Button(content,"采购缺料",()=>{var n=w.Node(game.SelectedNode);var r=game.Config.Economy.Recipe(n?.RecipeId);if(r!=null){var missing=r.Inputs.FirstOrDefault(i=>n.Input.Count(g=>g.Material==i.Material)<i.Quantity&&game.Config.Economy.Hardware.Materials.Any(m=>m.Id==i.Material&&m.Price>0));if(missing!=null)material=missing.Material;}game.Drawer="采购";RebuildDrawer();},actionId:"drawer:shortage");
                Button(content,"切换配方",()=>{var n=w.Node(game.SelectedNode);if(n==null)return;var rs=game.Config.Economy.Recipes.Where(r=>r.Machine==n.Kind).ToArray();if(rs.Length>0)game.Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=n.Id,Recipe=rs[(Array.FindIndex(rs,r=>r.Id==n.RecipeId)+1)%rs.Length].Id});});
                Button(content,"再建一台同类设备",()=>{var n=w.Node(game.SelectedNode);if(n!=null)game.BeginBuild(n.Kind,n.RecipeId);});
                Button(content,"查看上游",()=>{var route=w.State.Routes.FirstOrDefault(r=>r.To==game.SelectedNode);if(route!=null)game.SelectedNode=route.From;});
                Button(content,"移动 / 旋转",game.BeginMove);Button(content,"升级：提速，并降低次品率",()=>game.Submit(new WorldCommand{Kind=CommandKind.Upgrade,Node=game.SelectedNode}));
                Button(content,"拆除：回收库存，返还投入75%",()=>game.Submit(new WorldCommand{Kind=CommandKind.Remove,Node=game.SelectedNode}));Button(content,"取消等待改建",()=>game.Submit(new WorldCommand{Kind=CommandKind.CancelChange,Node=game.SelectedNode}));
            }
            if(game.Drawer=="订单")
            {
                var active=h.Orders.Where(o=>o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready).OrderBy(o=>o.Deadline).ToArray();
                if(active.Length>0)Label(content,"<color=#16B8D1>进行中 · 成品入库后自动分配并交付</color>",16,24);
                foreach(var o in active)OrderCard(o);
                Label(content,"<color=#16B8D1>订单板 · "+(w.OfferBoardOpen?"每 "+w.Settings.Hardware.OfferRefreshSeconds+" 秒刷新":"交付首单后开放")+"</color>",16,24);
                foreach(var o in w.Offers.OrderBy(o=>o.Template==0?1:0).ThenBy(o=>o.MinReputation).ThenBy(o=>o.Id))OrderCard(o);
                orderSignature=OrderSignature();
            }
            if(game.Drawer=="采购")
            {
                Button(content,"切换采购物料",()=>{var ms=game.Config.Economy.Hardware.Materials.Where(m=>m.Price>0).Select(m=>m.Id).ToArray();material=ms[(Array.IndexOf(ms,material)+1)%ms.Length];},actionId:"drawer:material");
                var qr=Row(Box("Quantity",content,false));Button(qr,"−1",()=>quantity=Math.Max(1,quantity-1),80,actionId:"drawer:minus");Button(qr,"+1",()=>quantity=Math.Min(80,quantity+1),80,actionId:"drawer:plus");Button(qr,"数量 6 / 8 / 12",()=>quantity=quantity==6?8:quantity==8?12:6,132);
                Button(content,"切换：常规 / 加急 / 自提",()=>delivery=(delivery+1)%3);
                Button(content,"支付并采购",()=>{var result=game.Submit(new WorldCommand{Kind=CommandKind.Purchase,Factory=game.Factory,Material=material,Quantity=quantity,Delivery=delivery});if(result.Success&&delivery==2&&game.Truck.Phase==VehiclePhase.Idle&&game.Truck.Cargo.Count==0)game.Submit(new WorldCommand{Kind=CommandKind.PickupPurchase,TargetFactory=game.Factory});},actionId:"drawer:purchase");
            }
            if(game.Drawer=="产品")
            {
                ProductCard(content,HardwareCatalog.Standard,"双风扇 / 石墨外壳 · 基础合同价 ¥480");ProductCard(content,HardwareCatalog.Quiet,"三风扇 / 浅色外壳 · 需要口碑 20 · 合同价 ¥560");ProductCard(content,HardwareCatalog.Budget,"次品 + 包装材料 → 丐版 · 电商与翻新渠道");
                Label(content,"板卡装配 → 显卡总装 → 检测 → 包装\n检测出的次品：返修台修复 / 丐版包装 / 市场卖给翻新商",15,60);
                Button(content,"切换产线：标准 / 静音",()=>{var nodes=w.State.Nodes.Where(n=>n.Factory==game.Factory&&(n.Kind==BuildingKind.GPUAssembly||n.Kind==BuildingKind.Tester||n.Kind==BuildingKind.Packaging&&n.RecipeId!="budget-pack")).ToArray();var quiet=nodes.Any(n=>!n.RecipeId.StartsWith("quiet-"));foreach(var n in nodes){var id=n.Kind==BuildingKind.GPUAssembly?"gpu":n.Kind==BuildingKind.Tester?"test":"pack";game.Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=n.Id,Recipe=(quiet?"quiet-":"")+id});}},actionId:"drawer:recipe");
                Button(content,"建造丐版包装机",()=>game.BeginBuild(BuildingKind.Packaging,"budget-pack"),actionId:"drawer:budget");
                Button(content,"建造返修台（次品 + 显存 → 待检）",()=>game.BeginBuild(BuildingKind.Rework));
            }
            if(game.Drawer=="市场")
            {
                foreach(var m in new[]{HardwareCatalog.Standard,HardwareCatalog.Quiet,HardwareCatalog.Budget,HardwareCatalog.Defect,HardwareCatalog.Ore})
                {
                    var id=m;var sell=Row(Box("Spot "+m,content,false));
                    Button(sell,"现货卖出 "+HardwareCatalog.Name(m)+" ×1",()=>game.Submit(new WorldCommand{Kind=CommandKind.SpotSell,Factory=game.Factory,Material=id,Quantity=1}));
                    Button(sell,"×5",()=>game.Submit(new WorldCommand{Kind=CommandKind.SpotSell,Factory=game.Factory,Material=id,Quantity=5}),64);
                }
            }
            if(game.Drawer=="运输")
            {
                Button(content,"查看矿点与车库",game.FocusLogistics);
                var cameras=Row(Box("Logistics cameras",content,false));Button(cameras,"矿区",game.FocusMine);Button(cameras,"采购站",game.FocusSupplier);Button(cameras,"皮卡",game.FocusTruck);Button(content,"返回工厂",game.FocusFactory);
                Button(content,"规划循环采集铝原料",()=>game.Submit(new WorldCommand{Kind=CommandKind.Collect,Resource=1,TargetFactory=game.Factory}),actionId:"drawer:mining");
                Button(content,"自动取回当前厂采购",()=>game.Submit(new WorldCommand{Kind=CommandKind.PickupPurchase,TargetFactory=game.Factory}),actionId:"drawer:pickup");
                Button(content,"停止任务并返厂卸货",()=>game.Submit(new WorldCommand{Kind=CommandKind.Return,TargetFactory=game.Factory}));
                Button(content,"救援返回车库 · 耗时60秒",()=>game.Submit(new WorldCommand{Kind=CommandKind.RecoverVehicle}));
                Button(content,"购买第二工厂",()=>game.Submit(new WorldCommand{Kind=CommandKind.BuyFactory,Factory=2}));
                Button(content,"调拨铝材到另一工厂",()=>game.Submit(new WorldCommand{Kind=CommandKind.Transfer,Factory=game.Factory,TargetFactory=game.Factory==1?2:1,Material=HardwareCatalog.Aluminum}));
            }
            Refresh();
        }
        private void OrderCard(HardwareOrder o)
        {
            var id=o.Id;var card=Column(Box("Order card "+id,content));
            Strip(card,HardwareVisualCatalog.Get(o.Material).Color);
            Label(card,"<b>"+o.Name+"</b>",17,26);
            var status=Label(card,"",14,o.Phase==HardwareOrderPhase.Available?78:60);
            UnityEngine.UI.Image image=null;
            if(o.Phase!=HardwareOrderPhase.Available)
            {
                var track=Box("Order progress",card);track.GetComponent<UnityEngine.UI.Image>().color=new Color(.12f,.2f,.27f);track.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=6;
                var fill=Box("Allocated product fill",track);Stretch(fill);image=fill.GetComponent<UnityEngine.UI.Image>();image.color=accent;image.type=UnityEngine.UI.Image.Type.Filled;image.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;image.fillOrigin=0;image.raycastTarget=false;
                Button(card,"取消订单（违约金 "+HardwareGame.Money(game.World.OrderValue(o)*WorldSimulation.CancelPenaltyPercent/100)+"）",()=>{game.Submit(new WorldCommand{Kind=CommandKind.CancelOrder,Order=id,Factory=game.Factory});RebuildDrawer();},0,32);
            }
            else acceptButtons[id]=Button(card,"接单",()=>{game.SelectedOrder=id;game.Submit(new WorldCommand{Kind=CommandKind.AcceptOrder,Order=id,Factory=game.Factory});RebuildDrawer();},0,40,"drawer:accept:"+id);
            orderCards[id]=(status,image);
        }
        private string OrderSignature()=>string.Join(",",game.World.State.Hardware.Orders.Where(o=>o.Phase<=HardwareOrderPhase.Ready).Select(o=>o.Id+":"+(int)o.Phase));
        public void FollowGuide()
        {
            var hint=HardwareGuide.Evaluate(game.World,game.Factory);
            var order=game.World.State.Hardware.Orders.FirstOrDefault(o=>o.Id==hint.Order);
            if(order!=null&&order.Phase!=HardwareOrderPhase.Available&&game.Factory!=order.Factory){game.Factory=order.Factory;game.FocusFactory();game.View.Rebuild();}
            if(hint.Order>0)game.SelectedOrder=hint.Order;
            if(hint.Key=="build"||hint.Key=="bottleneck"){game.BeginBuild(hint.Building);return;}
            if(hint.Node>0){game.FocusNode(hint.Node);return;}
            if(hint.Key=="supply"){material=hint.Material;quantity=Math.Clamp(hint.Quantity,1,80);delivery=0;game.Drawer="采购";}
            else game.Drawer=hint.Key=="order"||hint.Key=="free"?"订单":hint.Key=="pickup"||hint.Key=="mining"?"运输":hint.Key=="arrival"?"采购":"产品";
            RebuildDrawer();
        }
        private string MarketBadge(WorldSimulation w)
        {
            var e=w.CurrentEvent;if(e!=null)return "<color=#F2A33A>「"+e.Name+"」剩 "+w.MarketSecondsLeft+"s</color>";
            var f=w.ForecastEvent;if(f!=null)return "<color=#E8D27A>预告「"+f.Name+"」"+w.MarketSecondsLeft+"s</color>";
            return w.OfferBoardOpen?"行情平稳":"";
        }
        public void Refresh()
        {
            if(top==null||game.World==null)return;var w=game.World;var h=w.State.Hardware;
            top.text="<color=#16B8D1>星铸  /  GPU FACTORY</color>   "+HardwareGame.Money(w.State.Money)+"   口碑 "+h.Reputation+" · "+HardwareCatalog.ReputationTier(h.Reputation)+"   5分钟净流入 "+HardwareGame.Money(w.RecentCash)+"   "+MarketBadge(w)+"   "+(w.State.Tick/3600)+":"+(w.State.Tick/60%60).ToString("00")+"   "+(game.Paused?"暂停":game.Speed+"×")+"   "+game.Factory+"号厂";
            speedLabel.text="速度 "+game.Speed+"×";
            var hint=HardwareGuide.Evaluate(w,game.Factory);
            tutorialHint=hint;
            goal.text="<color=#16B8D1>当前目标 / "+hint.Title+"</color>\n"+hint.Detail;guideAction.text=hint.Action;
            notice.text=string.Join("\n",game.Notices.Reverse());notices.gameObject.SetActive(game.Drawer==""&&game.Notices.Length>0&&!game.Truck.Manual);
            build.gameObject.SetActive(true);goals.gameObject.SetActive(true);
            selection.text=game.Placing.HasValue?"放置 "+game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==game.Placing).Name+" · R旋转设备 · Esc取消 · Q/E旋转视角":"WASD移动 · Q/E旋转视角 · 右键/中键拖动 · 滚轮缩放 · H引导 · 空格暂停";
            var v=game.Truck;
            if(game.Drawer=="订单"&&OrderSignature()!=orderSignature){RebuildDrawer();return;}
            foreach(var card in orderCards)
            {
                var o=h.Orders.FirstOrDefault(x=>x.Id==card.Key);if(o==null)continue;var caption=HardwareVisualCatalog.Caption(o.Material);var value=w.OrderValue(o);
                if(o.Phase==HardwareOrderPhase.Available)
                {
                    var locked=h.Reputation<o.MinReputation;
                    card.Value.Status.text=caption+" ×"+o.Quantity+" · 单价 "+HardwareGame.Money(o.UnitPrice)+" · 总额 <b>"+HardwareGame.Money(value)+"</b>\n交期 "+o.Seconds+" 秒，前 "+o.Seconds*60/100+" 秒内交付 <color=#38C785>+15%</color>\n超期作废违约金 "+HardwareGame.Money(value*WorldSimulation.ExpirePenaltyPercent/100)+(o.MinReputation>0?" · "+(locked?"<color=#EB5545>":"")+"需口碑 "+o.MinReputation+(locked?"</color>":""):"")+(o.OfferExpires>0?" · 报价剩 "+Seconds(o.OfferExpires-w.State.Tick):"");
                    if(acceptButtons.TryGetValue(o.Id,out var accept))accept.interactable=!locked&&w.ActiveOrderCount<w.Settings.Hardware.MaxActiveOrders&&(o.OfferExpires==0||w.State.Tick<=o.OfferExpires);
                    continue;
                }
                var reserved=w.ReservedCount(o.Id);var grade=w.GradeAt(o,w.State.Tick);
                var timing=grade==DeliveryGrade.Early?"<color=#38C785>奖金期剩 "+Seconds(o.BonusDeadline-w.State.Tick)+"</color>":grade==DeliveryGrade.OnTime?"交期剩 "+Seconds(o.Deadline-w.State.Tick):"<color=#EB5545>已迟交 -25%，作废倒计时 "+Seconds(o.HardDeadline-w.State.Tick)+"</color>";
                var percent=grade==DeliveryGrade.Early?WorldSimulation.EarlyPercent:grade==DeliveryGrade.OnTime?100:WorldSimulation.LatePercent;
                card.Value.Status.text=caption+" 已分配 <b>"+reserved+" / "+o.Quantity+"</b> · "+Phase(o.Phase)+"\n"+timing+"\n现在交付可得 "+HardwareGame.Money(value*percent/100);
                if(card.Value.Fill!=null){card.Value.Fill.fillAmount=Mathf.Clamp01(reserved/(float)o.Quantity);card.Value.Fill.color=grade==DeliveryGrade.Late?Bad:o.Phase==HardwareOrderPhase.Ready?Good:accent;}
            }
            if(details==null)return;
            if(game.Drawer=="设备")
            {
                var n=w.Node(game.SelectedNode);if(n==null){details.text="未选择设备";return;}var recipe=game.Config.Economy.Recipe(n.RecipeId);var bottleneck=w.Bottleneck(n.Factory)?.Id==n.Id;
                details.text=game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name+" L"+n.Level+(bottleneck?"  <color=#EB5545>【瓶颈】</color>":"")+"\n生产："+(recipe?.Name??"物流服务")+"\n状态："+StateLabel(n.RunState)+" · "+n.Status+(recipe==null?"":"\n利用率 <b>"+w.Utilization(n)+"%</b> · 周期 "+recipe.Seconds*100/game.Config.Economy.UpgradePercent[n.Level-1]/100f+"s")
                    +(recipe!=null&&recipe.FlawPermille>0?"\n次品率 "+w.FlawPermille(n,recipe)/10f+"%（首单后生效，升级可降低）":"")+"\n"+(recipe==null?"":string.Join("\n",recipe.Inputs.Select(i=>HardwareCatalog.Name(i.Material)+" 缺 "+Math.Max(0,i.Quantity-n.Input.Count(g=>g.Material==i.Material)))))+"\n输入 "+n.Input.Count+" / 输出 "+n.Output.Count+" / 加工 "+n.WorkRemaining/60+"s"+(n.PendingRemove||n.PendingRecipe!=""?"\n改建等待当前批次完成":"")
                    +(bottleneck?"\n下游在等它：再建一台同类设备或升级，产量立刻提升。":"");
            }
            if(game.Drawer=="订单")details.text="口碑 <b>"+h.Reputation+"</b> · "+HardwareCatalog.ReputationTier(h.Reputation)+" · 进行中 "+w.ActiveOrderCount+" / "+w.Settings.Hardware.MaxActiveOrders+"\n提前（前60%时间）+15% 口碑+"+WorldSimulation.EarlyReputation+" · 准时 口碑+"+WorldSimulation.OnTimeReputation+"\n迟交 -25% 口碑"+WorldSimulation.LateReputation+" · 超期1.5倍作废：违约金10% 口碑"+WorldSimulation.ExpireReputation;
            if(game.Drawer=="采购")
            {
                var m=game.Config.Economy.Hardware.Materials.FirstOrDefault(m=>m.Id==material&&m.Price>0);if(m==null){material=HardwareCatalog.Heat;m=game.Config.Economy.Hardware.Materials.First(m=>m.Id==material);}var config=game.Config.Economy.Hardware;var fee=delivery==0?config.DeliveryFee:delivery==1?config.ExpressFee:config.PickupFee;var price=w.PurchasePrice(material);
                details.text="青港元件供应站\n"+HardwareVisualCatalog.Caption(material)+"\n"+m.Name+" ×"+quantity+"\n单价 "+HardwareGame.Money(price)+Trend(price,m.Price)+" · 库存 "+h.Stock.First(s=>s.Material==material).Quantity+"\n"+new[]{"常规 "+config.DeliverySeconds+"秒","加急 "+config.ExpressSeconds+"秒","自提：空闲皮卡自动装运"}[delivery]+" · 运费 "+HardwareGame.Money(fee)+"\n合计 <b>"+HardwareGame.Money((long)price*quantity+fee)+"</b>\n每 "+config.RestockSeconds+" 秒补货 · 在途/待提 "+h.Deliveries.Sum(d=>d.Cargo.Count)+" 件";
            }
            if(game.Drawer=="产品")details.text="星铸 S1 产品线\n当前仓库：标准 "+Stock(HardwareCatalog.Standard)+" · 静音 "+Stock(HardwareCatalog.Quiet)+" · 丐版 "+Stock(HardwareCatalog.Budget)+" · 次品 "+Stock(HardwareCatalog.Defect)+"\n切换配方会等待当前批次完成";
            if(game.Drawer=="市场")
            {
                var e=w.CurrentEvent;var f=w.ForecastEvent;
                details.text=(e!=null?"<color=#F2A33A><b>「"+e.Name+"」</b>剩 "+w.MarketSecondsLeft+" 秒</color>\n"+e.Detail:f!=null?"<color=#E8D27A>预告「"+f.Name+"」"+w.MarketSecondsLeft+" 秒后到来</color>\n"+f.Detail+"\n现在囤料或转产正是时候。":w.OfferBoardOpen?"行情平稳 · 下次变化约 "+w.MarketSecondsLeft+" 秒后":"交付首单后，市场行情开始波动。")
                    +"\n\n采购价：GPU核心 "+HardwareGame.Money(w.PurchasePrice(HardwareCatalog.Core))+Trend(w.PurchasePrice(HardwareCatalog.Core),18000)+" · 显存包 "+HardwareGame.Money(w.PurchasePrice(HardwareCatalog.Memory))+Trend(w.PurchasePrice(HardwareCatalog.Memory),6000)
                    +"\n合同单价：标准 "+w.OrderPricePercent(HardwareCatalog.Standard)+"% · 静音 "+w.OrderPricePercent(HardwareCatalog.Quiet)+"%"
                    +"\n现货价：标准 "+HardwareGame.Money(w.SpotPrice(HardwareCatalog.Standard))+" · 静音 "+HardwareGame.Money(w.SpotPrice(HardwareCatalog.Quiet))+" · 丐版 "+HardwareGame.Money(w.SpotPrice(HardwareCatalog.Budget))+" · 次品 "+HardwareGame.Money(w.SpotPrice(HardwareCatalog.Defect))
                    +"\n成交额度 "+h.SpotRemaining+" 件 / 5分钟 · 仅卖未分配库存";
            }
            if(game.Drawer=="运输")details.text="<color=#16B8D1>自动物流 / PICKUP 01</color>\n"+v.Status+"\n载货 "+v.Cargo.Count+" / "+game.Config.Economy.VehicleCapacity+" · 送往 "+v.TargetFactory+"号厂\n任务："+(v.PickupTask?"取回采购":v.Loop?"循环采集":"单次 / 待命")+"\n采满 → 返厂 → 卸货 → 再采集；仓满或矿空会等待\n待自提 "+h.Deliveries.Where(d=>d.Method==2&&d.Factory==game.Factory).Sum(d=>d.Cargo.Count)+" 件";
            if(game.Drawer=="报表")details.text="现金流 ≠ 利润\n口碑 "+h.Reputation+" · "+HardwareCatalog.ReputationTier(h.Reputation)+"\n近5分钟净流入 "+HardwareGame.Money(w.RecentCash)+"\n累计收入 "+HardwareGame.Money(w.State.Revenue)+" · 现金支出 "+HardwareGame.Money(w.State.Expenses)+"\n违约金 "+HardwareGame.Money(h.Penalties)+" · 设备投资 "+HardwareGame.Money(w.State.Nodes.Sum(n=>n.Investment))+"\n\n"+string.Join("\n",h.Settlements.AsEnumerable().Reverse().Take(4).Select(s=>(s.Grade==DeliveryGrade.Early?"<color=#38C785>提前</color>":s.Grade==DeliveryGrade.OnTime?"准时":"<color=#F2A33A>迟交</color>")+" "+s.Name+"\n  收入 "+HardwareGame.Money(s.Income)+" · 毛利 "+HardwareGame.Money(s.Margin)+" · 口碑 "+(s.ReputationDelta>0?"+":"")+s.ReputationDelta));
        }
        private int Stock(int material)=>game.World.FinishedStock(game.Factory).Count(g=>g.Material==material&&g.ReservedOrder==0);
        private static string Trend(int price,int basePrice)=>price>basePrice?" <color=#EB5545>↑"+(price*100/basePrice-100)+"%</color>":price<basePrice?" <color=#38C785>↓"+(100-price*100/basePrice)+"%</color>":"";
        public static string StateLabel(NodeState s)=>s==NodeState.Working?"<color=#38C785>运行</color>":s==NodeState.Starved?"<color=#F2C14E>缺料</color>":s==NodeState.Blocked?"<color=#EB5545>堵塞</color>":s==NodeState.NoFunds?"<color=#B07CE8>资金不足</color>":s==NodeState.Disconnected?"<color=#9AA5AE>未连通</color>":"待机";
        private void LateUpdate()
        {
            if(canvas==null)return;var width=((RectTransform)canvas.transform).rect.width;
            if(Mathf.Abs(layoutWidth-width)>.5f)
            {
                layoutWidth=width;var bw=Mathf.Min(1200,width-24);
                build.offsetMin=new Vector2(-bw*.5f,12);build.offsetMax=new Vector2(bw*.5f,116);
            }
            if(details!=null){var e=details.GetComponent<UnityEngine.UI.LayoutElement>();var height=Mathf.Max(80,details.preferredHeight+8);if(Mathf.Abs(e.preferredHeight-height)>1)e.preferredHeight=height;}
            if(toastTime>0){toastTime-=Time.unscaledDeltaTime;if(toastTime<=0)toast.gameObject.SetActive(false);}
            if(flashTime>0){flashTime-=Time.unscaledDeltaTime;top.color=Color.Lerp(ink,Gold,.5f+.5f*Mathf.Sin(Time.unscaledTime*8));if(flashTime<=0)top.color=ink;}
            UpdateTutorial();
        }
    }
}
