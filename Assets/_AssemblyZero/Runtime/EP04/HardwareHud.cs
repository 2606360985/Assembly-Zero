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
        private HardwareGame game;private Canvas canvas;private RectTransform drawer,content,pause,modal,build,goals,notices;
        private TMP_Text top,goal,notice,details,selection,guideAction;private int material=HardwareCatalog.Heat,quantity=6,delivery;private float zoom=1;private bool confirmNew;
        private readonly Color panel=new Color(.055f,.09f,.135f,.97f),ink=new Color(.88f,.94f,.97f),accent=HardwareVisualCatalog.Cyan;
        private RectTransform buildViewport;
        private Sprite roundedPanel;
        private Texture2D panelTexture;
        private float layoutWidth;
        private readonly Dictionary<int,(TMP_Text Status,UnityEngine.UI.Image Fill)> orderCards=new Dictionary<int,(TMP_Text Status,UnityEngine.UI.Image Fill)>();
        private readonly Dictionary<string,UnityEngine.UI.Button> orderActions=new Dictionary<string,UnityEngine.UI.Button>();
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
        private void ProductCard(Transform parent,int material)
        {
            var style=HardwareVisualCatalog.Get(material);var card=Box("Product card "+material,parent);var column=Column(card);
            var strip=Box("SKU strip",column);strip.GetComponent<UnityEngine.UI.Image>().color=style.Color;strip.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            strip.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=4;
            Label(column,HardwareVisualCatalog.Caption(material),16,30);
            Label(column,HardwareCatalog.Name(material)+"\n"+(style.Quiet?"三风扇 / 浅色外壳 / 低噪散热":"双风扇 / 石墨外壳 / 标准散热"),16,52);
        }
        private void OnDestroy(){if(roundedPanel!=null)Destroy(roundedPanel);if(panelTexture!=null)Destroy(panelTexture);}
        public void Initialize(HardwareGame g)
        {
            game=g;canvas=new GameObject("Hardware HUD",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<Canvas>();canvas.transform.SetParent(transform,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            if(FindAnyObjectByType<EventSystem>()==null)new GameObject("Hardware EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var bar=Box("Status",canvas.transform);Anchor(bar,new Vector2(0,1),Vector2.one,new Vector2(0,-56),Vector2.zero);var row=Row(bar);top=Label(row,"",18,46,1000);Button(row,"工厂",()=>{game.ChangeFactory();},82);Button(row,"1× / 2×",()=>{if(!game.Truck.Manual)game.Speed=game.Speed==1?2:1;},105);Button(row,"菜单",game.TogglePause,82);
            var nav=Box("Navigation",canvas.transform);Anchor(nav,new Vector2(0,1),new Vector2(0,1),new Vector2(8,-496),new Vector2(68,-72));var list=Column(nav);
            foreach(var label in new[]{"工厂","订单","产品","采购","运输","报表"}){var target=label;Button(list,target,()=>{if(target=="工厂"){game.FocusFactory();game.Drawer="";RebuildDrawer();}else game.SetDrawer(target);},0,56,"nav:"+target);}
            goals=Box("Tracked order",canvas.transform);Anchor(goals,new Vector2(0,1),new Vector2(0,1),new Vector2(84,-308),new Vector2(354,-72));var gc=Column(goals);goal=Label(gc,"",16,152);Button(gc,"下一步",FollowGuide,0,48);guideAction=gc.GetComponentsInChildren<UnityEngine.UI.Button>().Last().GetComponentInChildren<TMP_Text>();
            drawer=Box("Context drawer",canvas.transform);Anchor(drawer,new Vector2(1,0),Vector2.one,new Vector2(-356,116),new Vector2(-12,-72));
            var scroll=drawer.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport=Box("Viewport",drawer);Stretch(viewport,8);viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            content=Box("Content",viewport,false);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;Column(content);var fit=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=content;
            build=Box("Construction",canvas.transform);Anchor(build,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-600,12),new Vector2(600,108));var buildColumn=Column(build);selection=Label(buildColumn,"建设设备 · 右键/中键拖动 · 滚轮缩放",16,24);buildViewport=Box("Build viewport",buildColumn,false);var viewportElement=buildViewport.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();viewportElement.preferredHeight=58;viewportElement.minHeight=58;
            buildViewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var buildRow=Row(Box("Categories",buildViewport,false));buildRow.anchorMin=new Vector2(0,0);buildRow.anchorMax=new Vector2(0,1);buildRow.pivot=new Vector2(0,.5f);buildRow.sizeDelta=new Vector2(1170,0);buildRow.anchoredPosition=Vector2.zero;
            var buildScroll=buildViewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();buildScroll.viewport=buildViewport;buildScroll.content=buildRow;buildScroll.horizontal=true;buildScroll.vertical=false;buildScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;buildScroll.scrollSensitivity=35;
            foreach(var b in game.Config.Economy.Hardware.Buildings){var kind=b.Kind;Button(buildRow,b.Name+"\n"+HardwareGame.Money(b.Cost),()=>game.BeginBuild(kind),136,48,"build:"+kind);}
            notices=Box("Notifications",canvas.transform);Anchor(notices,new Vector2(1,1),Vector2.one,new Vector2(-356,-226),new Vector2(-12,-72));var nc=Column(notices);notice=Label(nc,"",16,92);Button(nc,"处理供料 / 查看采购",()=>game.SetDrawer("采购"),0,34);
            modal=Box("Modal blocker",canvas.transform);Stretch(modal);modal.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.45f);pause=Box("Pause",modal);Anchor(pause,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-220,-280),new Vector2(220,280));var pc=Column(pause);Label(pc,"已暂停 · 全部业务冻结",24,48);Button(pc,"继续",game.TogglePause);Button(pc,"保存显卡战役",()=>game.Save());Button(pc,"读取显卡战役",()=>game.Load());Button(pc,"新战役（再次点击确认）",()=>{if(confirmNew){game.NewGame();confirmNew=false;}else{confirmNew=true;game.Message="再次点击新战役确认；下次保存才更新显卡存档";}});Button(pc,"旧 EP04 经营原型",game.OpenLegacy);Button(pc,"界面缩放 100 / 115 / 130%",()=>{zoom=zoom>=1.3f?1:zoom+.15f;scaler.referenceResolution=new Vector2(1920/zoom,1080/zoom);});Button(pc,"退出",()=>{game.Save();Application.Quit();});modal.gameObject.SetActive(false);
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
                default:Line(0,8,26,5);Line(-9,-2,3,15);Line(9,-2,3,15);Line(0,-9,24,3);break;
            }
        }
        public void ShowPause(bool value){modal.gameObject.SetActive(value);if(value)modal.SetAsLastSibling();}
        public void RebuildDrawer()
        {
            ClearDrawerGuide();
            orderCards.Clear();orderActions.Clear();
            foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}details=null;drawer.gameObject.SetActive(game.Drawer!=""&&!game.Truck.Manual);if(!drawer.gameObject.activeSelf)return;
            Label(content,"<color=#16B8D1>//</color>  "+game.Drawer,24,42);Button(content,"关闭",()=>{game.Drawer="";RebuildDrawer();});
            details=Label(content,"",18,220);
            guideTargets["drawer:details"]=(RectTransform)details.transform;
            if(game.Drawer=="设备")
            {
                Button(content,"采购缺料",()=>{var n=game.World.Node(game.SelectedNode);var r=game.Config.Economy.Recipe(n?.RecipeId);if(r!=null){var missing=r.Inputs.FirstOrDefault(i=>n.Input.Count(g=>g.Material==i.Material)<i.Quantity&&game.Config.Economy.Hardware.Materials.Any(m=>m.Id==i.Material&&m.Price>0));if(missing!=null)material=missing.Material;}game.Drawer="采购";RebuildDrawer();},actionId:"drawer:shortage");
                Button(content,"切换配方",()=>{var n=game.World.Node(game.SelectedNode);if(n==null)return;var rs=game.Config.Economy.Recipes.Where(r=>r.Machine==n.Kind).ToArray();if(rs.Length>0)game.Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=n.Id,Recipe=rs[(Array.FindIndex(rs,r=>r.Id==n.RecipeId)+1)%rs.Length].Id});});
                Button(content,"查看上游",()=>{var route=game.World.State.Routes.FirstOrDefault(r=>r.To==game.SelectedNode);if(route!=null)game.SelectedNode=route.From;});
                Button(content,"移动 / 旋转",game.BeginMove);Button(content,"升级",()=>game.Submit(new WorldCommand{Kind=CommandKind.Upgrade,Node=game.SelectedNode}));
                Button(content,"拆除：回收库存，返还投入75%",()=>game.Submit(new WorldCommand{Kind=CommandKind.Remove,Node=game.SelectedNode}));Button(content,"取消等待改建",()=>game.Submit(new WorldCommand{Kind=CommandKind.CancelChange,Node=game.SelectedNode}));
            }
            if(game.Drawer=="订单")
            {
                foreach(var pair in new[]{("接单",CommandKind.AcceptOrder),("预留合格库存",CommandKind.ReserveOrder),("整单交付",CommandKind.DeliverOrder),("释放预留",CommandKind.ReleaseOrder),("取消订单（库存保留）",CommandKind.CancelOrder)}){var kind=pair.Item2;orderActions[pair.Item1]=Button(content,pair.Item1,()=>{game.Submit(new WorldCommand{Kind=kind,Order=game.SelectedOrder,Factory=game.Factory});RebuildDrawer();},actionId:"drawer:"+pair.Item1);}
                foreach(var o in game.World.State.Hardware.Orders.Where(o=>o.Phase!=HardwareOrderPhase.Delivered&&o.Phase!=HardwareOrderPhase.Cancelled&&o.Phase!=HardwareOrderPhase.Expired)){var id=o.Id;var card=Column(Box("Order card "+id,content));Button(card,o.Name,()=>{game.SelectedOrder=id;Refresh();},0,48,"drawer:order:"+id);var status=Label(card,"",15,38);var track=Box("Order progress",card);track.GetComponent<UnityEngine.UI.Image>().color=new Color(.12f,.2f,.27f);track.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=6;var fill=Box("Available product fill",track);Stretch(fill);var image=fill.GetComponent<UnityEngine.UI.Image>();image.color=accent;image.type=UnityEngine.UI.Image.Type.Filled;image.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;image.fillOrigin=0;image.raycastTarget=false;orderCards[id]=(status,image);}
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
                ProductCard(content,HardwareCatalog.Standard);ProductCard(content,HardwareCatalog.Quiet);
                Label(content,"PCB 裸板  →  核心 / 显存贴装\n→  散热总装  →  检测合格  →  品牌包装",15,60);
                Button(content,"切换产线：标准 / 静音",()=>{var nodes=game.World.State.Nodes.Where(n=>n.Factory==game.Factory&&(n.Kind==BuildingKind.GPUAssembly||n.Kind==BuildingKind.Tester||n.Kind==BuildingKind.Packaging)).ToArray();var quiet=nodes.Any(n=>!n.RecipeId.StartsWith("quiet-"));foreach(var n in nodes){var id=n.Kind==BuildingKind.GPUAssembly?"gpu":n.Kind==BuildingKind.Tester?"test":"pack";game.Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=n.Id,Recipe=(quiet?"quiet-":"")+id});}},actionId:"drawer:recipe");
                Button(content,"回收 1 张未预留标准版",()=>game.Submit(new WorldCommand{Kind=CommandKind.SpotSell,Factory=game.Factory,Material=HardwareCatalog.Standard,Quantity=1}));
                Button(content,"回收 1 张未预留静音版",()=>game.Submit(new WorldCommand{Kind=CommandKind.SpotSell,Factory=game.Factory,Material=HardwareCatalog.Quiet,Quantity=1}));
                Button(content,"回收 1 份铝原料（恢复现金）",()=>game.Submit(new WorldCommand{Kind=CommandKind.SpotSell,Factory=game.Factory,Material=HardwareCatalog.Ore,Quantity=1}));
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
        private static string Phase(HardwareOrderPhase p)=>new[]{"可接","进行中","可交付","已交付","已过期","已取消"}[(int)p];
        public void FollowGuide()
        {
            var hint=HardwareGuide.Evaluate(game.World,game.Factory);
            var order=game.World.State.Hardware.Orders.FirstOrDefault(o=>o.Id==hint.Order);
            if(order!=null&&game.Factory!=order.Factory){game.Factory=order.Factory;game.FocusFactory();game.View.Rebuild();}
            if(hint.Order>0)game.SelectedOrder=hint.Order;
            if(hint.Key=="build"){game.BeginBuild(hint.Building);return;}
            if(hint.Node>0){game.FocusNode(hint.Node);return;}
            if(hint.Key=="supply"){material=hint.Material;quantity=Math.Clamp(hint.Quantity,1,80);delivery=0;game.Drawer="采购";}
            else game.Drawer=hint.Key=="order"||hint.Key=="reserve"||hint.Key=="deliver"||hint.Key=="free"?"订单":hint.Key=="pickup"||hint.Key=="mining"?"运输":hint.Key=="arrival"?"采购":"产品";
            RebuildDrawer();
        }
        public void Refresh()
        {
            if(top==null||game.World==null)return;var w=game.World;var h=w.State.Hardware;
            top.text="<color=#16B8D1>星铸  /  GPU FACTORY</color>   "+HardwareGame.Money(w.State.Money)+"    5分钟净流入 "+HardwareGame.Money(w.RecentCash)+"    "+(w.State.Tick/3600)+":"+(w.State.Tick/60%60).ToString("00")+"   "+(game.Paused?"暂停":game.Speed+"×")+"   "+game.Factory+"号厂";
            var hint=HardwareGuide.Evaluate(w,game.Factory);var tracked=h.Orders.FirstOrDefault(o=>o.Id==hint.Order);
            tutorialHint=hint;
            goal.text="<color=#16B8D1>当前目标 / "+hint.Title+"</color>\n"+hint.Detail+(tracked!=null&&tracked.Deadline>0?"\n交期剩余 "+Math.Max(0,(tracked.Deadline-w.State.Tick)/60)+" 秒":"");guideAction.text=hint.Action;
            notice.text=string.Join("\n",game.Notices.Reverse());notices.gameObject.SetActive(game.Drawer==""&&game.Notices.Length>0&&!game.Truck.Manual);
            build.gameObject.SetActive(true);goals.gameObject.SetActive(true);
            selection.text=game.Placing.HasValue?"放置 "+game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==game.Placing).Name+" · R旋转设备 · Esc取消 · Q/E旋转视角":"WASD移动 · Q/E旋转视角 · 右键/中键拖动 · 滚轮缩放 · H引导";
            var v=game.Truck;
            foreach(var card in orderCards)
            {
                var order=h.Orders.FirstOrDefault(o=>o.Id==card.Key);if(order==null)continue;var reserved=w.ReservedCount(order.Id);var free=w.FinishedStock(order.Factory).Count(g=>g.Material==order.Material&&g.ReservedOrder==0);
                card.Value.Status.text=Phase(order.Phase)+" · 预留 "+reserved+" / "+order.Quantity+"\n可用成品 "+free+" 件";card.Value.Fill.fillAmount=Mathf.Clamp01((reserved+free)/(float)order.Quantity);card.Value.Fill.color=order.Phase==HardwareOrderPhase.Ready?new Color(.22f,.78f,.52f):accent;
            }
            if(game.Drawer=="订单")
            {
                var order=h.Orders.FirstOrDefault(o=>o.Id==game.SelectedOrder);var active=order!=null&&(order.Phase==HardwareOrderPhase.Active||order.Phase==HardwareOrderPhase.Ready);
                foreach(var button in orderActions)
                {
                    button.Value.interactable=button.Key=="接单"?order!=null&&order.Phase==HardwareOrderPhase.Available&&(order.Template!=2||h.Orders.Any(o=>o.Template==1&&o.Phase==HardwareOrderPhase.Delivered)):
                        button.Key=="整单交付"?active&&w.ReservedCount(order.Id)>=order.Quantity&&w.State.Nodes.Any(n=>n.Factory==order.Factory&&n.Kind==BuildingKind.Sales):
                        button.Key=="释放预留"?active&&w.ReservedCount(order.Id)>0:active;
                }
            }
            if(details==null)return;
            if(game.Drawer=="设备")
            {
                var n=w.Node(game.SelectedNode);if(n==null){details.text="未选择设备";return;}var recipe=game.Config.Economy.Recipe(n.RecipeId);
                details.text=game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name+" L"+n.Level+"\n生产："+(recipe?.Name??"物流服务")+"\n状态："+n.Status+"\n"+(recipe==null?"":string.Join("\n",recipe.Inputs.Select(i=>HardwareCatalog.Name(i.Material)+" 缺 "+Math.Max(0,i.Quantity-n.Input.Count(g=>g.Material==i.Material)))))+"\n输入 "+n.Input.Count+" / 输出 "+n.Output.Count+" / 加工 "+n.WorkRemaining/60+"s\n"+(recipe==null?"": "理论周期 "+recipe.Seconds+"s；未计入缺料/堵塞")+"\n"+(n.PendingRemove||n.PendingRecipe!=""?"改建等待当前批次完成":"");
            }
            if(game.Drawer=="订单")
            {
                var o=h.Orders.FirstOrDefault(o=>o.Id==game.SelectedOrder);if(o==null)return;var estimate=w.ReplacementCost(o.Material)*o.Quantity;
                details.text=o.Name+"\n"+HardwareVisualCatalog.Caption(o.Material)+"\n"+HardwareCatalog.Name(o.Material)+" ×"+o.Quantity+" · "+Phase(o.Phase)+"\n可用合格库存 "+w.FinishedStock(o.Factory).Count(g=>g.Material==o.Material&&g.ReservedOrder==0)+" / 已预留 "+w.ReservedCount(o.Id)+"\n收入 "+HardwareGame.Money((long)o.Quantity*o.UnitPrice)+"\n预计补货+加工 "+HardwareGame.Money(estimate)+"\n预估毛利 "+HardwareGame.Money((long)o.Quantity*o.UnitPrice-estimate)+"（未计运费）\n交期估计未计缺料；超期无收入，无罚金\n期限 "+(o.Deadline==0?SettingsOrderSeconds(o.Template):Math.Max(0,(o.Deadline-w.State.Tick)/60))+" 秒";
            }
            if(game.Drawer=="采购")
            {
                var m=game.Config.Economy.Hardware.Materials.FirstOrDefault(m=>m.Id==material&&m.Price>0);if(m==null){material=HardwareCatalog.Heat;m=game.Config.Economy.Hardware.Materials.First(m=>m.Id==material);}var config=game.Config.Economy.Hardware;var fee=delivery==0?config.DeliveryFee:delivery==1?config.ExpressFee:config.PickupFee;
                details.text="青港元件供应站\n"+HardwareVisualCatalog.Caption(material)+"\n"+m.Name+" ×"+quantity+"\n单价 "+HardwareGame.Money(m.Price)+" · 库存 "+h.Stock.First(s=>s.Material==material).Quantity+"\n"+new[]{"常规 90秒","加急 25秒","自提：空闲皮卡自动装运"}[delivery]+"\n运费 "+HardwareGame.Money(fee)+"\n合计 "+HardwareGame.Money((long)m.Price*quantity+fee)+"\n固定120秒补货；仓满则等待\n在途 / 待提："+h.Deliveries.Sum(d=>d.Cargo.Count)+" 件\n皮卡忙碌时，在运输页另行派遣";
            }
            if(game.Drawer=="产品")details.text="星铸 S1 / 显卡产品线\n标准：双风扇 · 石墨外壳\n静音：三风扇 · 浅色外壳 · 加厚散热\nGPU核心与显存分别采购\n\n当前回收额度 "+h.SpotRemaining+" 件 / 每5分钟\n切换等待批次完成；分别检查三台设备状态";
            if(game.Drawer=="运输")details.text="<color=#16B8D1>自动物流 / PICKUP 01</color>\n"+v.Status+"\n载货 "+v.Cargo.Count+" / "+game.Config.Economy.VehicleCapacity+" · 送往 "+v.TargetFactory+"号厂\n任务："+(v.PickupTask?"取回采购":v.Loop?"循环采集":"单次 / 待命")+"\n\n选择矿点与目标工厂后自动执行\n采满 → 返厂 → 卸货 → 再采集\n仓满或矿空会等待，不会丢货\n停止任务会返厂卸货后待命\n待自提 "+h.Deliveries.Where(d=>d.Method==2&&d.Factory==game.Factory).Sum(d=>d.Cargo.Count)+" 件\nTab 打开运输；手动驾驶暂时冻结";
            if(game.Drawer=="报表")details.text="现金流 ≠ 利润\n近5分钟净流入 "+HardwareGame.Money(w.RecentCash)+"\n累计收入 "+HardwareGame.Money(w.State.Revenue)+"\n累计现金支出 "+HardwareGame.Money(w.State.Expenses)+"\n设备投资 "+HardwareGame.Money(w.State.Nodes.Sum(n=>n.Investment))+"\n\n"+string.Join("\n",h.Settlements.TakeLast(3).Select(s=>"订单 #"+s.Order+" 收入 "+HardwareGame.Money(s.Income)+"\n材料 "+HardwareGame.Money(s.MaterialCost)+" 加工 "+HardwareGame.Money(s.ProcessingCost)+" 运费 "+HardwareGame.Money(s.FreightCost)+"\n实际毛利 "+HardwareGame.Money(s.Margin)));
        }
        private void LateUpdate()
        {
            if(canvas==null)return;var width=((RectTransform)canvas.transform).rect.width;
            if(Mathf.Abs(layoutWidth-width)>.5f)
            {
                layoutWidth=width;var bw=Mathf.Min(1200,width-24);
                build.offsetMin=new Vector2(-bw*.5f,12);build.offsetMax=new Vector2(bw*.5f,116);
            }
            if(details!=null){var e=details.GetComponent<UnityEngine.UI.LayoutElement>();var height=Mathf.Max(220,details.preferredHeight+8);if(Mathf.Abs(e.preferredHeight-height)>1)e.preferredHeight=height;}
            UpdateTutorial();
        }
        private int SettingsOrderSeconds(int id)=>game.Config.Economy.Hardware.Orders.First(t=>t.Id==id).Seconds;
    }
}
