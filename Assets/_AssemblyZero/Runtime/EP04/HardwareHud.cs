using System;
using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AssemblyZero.Unity.EP04
{
    public sealed class HardwareHud : MonoBehaviour
    {
        private HardwareGame game;private Canvas canvas;private RectTransform drawer,content,pause,build,goals,notices,driving;
        private TMP_Text top,goal,notice,details,driveInfo,selection;private int material=HardwareCatalog.Heat,quantity=6,delivery;private float zoom=1;private bool confirmNew;
        private readonly Color panel=new Color(.075f,.085f,.082f,.97f),ink=new Color(.91f,.91f,.85f),accent=new Color(.9f,.55f,.17f);
        public void Initialize(HardwareGame g)
        {
            game=g;canvas=new GameObject("Hardware HUD",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<Canvas>();canvas.transform.SetParent(transform,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            if(FindAnyObjectByType<EventSystem>()==null)new GameObject("Hardware EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var bar=Box("Status",canvas.transform);Anchor(bar,new Vector2(0,1),Vector2.one,new Vector2(0,-56),Vector2.zero);var row=Row(bar);top=Label(row,"",23,50,1000);Button(row,"工厂",()=>{game.ChangeFactory();},82);Button(row,"1× / 2×",()=>{if(!game.Truck.Manual)game.Speed=game.Speed==1?2:1;},105);Button(row,"菜单",game.TogglePause,82);
            var nav=Box("Navigation",canvas.transform);Anchor(nav,new Vector2(0,1),new Vector2(0,1),new Vector2(8,-496),new Vector2(68,-72));var list=Column(nav);
            foreach(var label in new[]{"工厂","订单","产品","采购","运输","报表"}){var target=label;Button(list,target,()=>{if(target=="工厂"){game.FocusFactory();game.Drawer="";RebuildDrawer();}else game.SetDrawer(target);},0,56);}
            goals=Box("Tracked order",canvas.transform);Anchor(goals,new Vector2(0,1),new Vector2(0,1),new Vector2(80,-220),new Vector2(360,-72));goal=Label(Column(goals),"",18,132);
            drawer=Box("Context drawer",canvas.transform);Anchor(drawer,new Vector2(1,0),Vector2.one,new Vector2(-356,116),new Vector2(-12,-72));
            var scroll=drawer.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport=Box("Viewport",drawer);Stretch(viewport,8);viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            content=Box("Content",viewport,false);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;Column(content);var fit=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=content;
            build=Box("Construction",canvas.transform);Anchor(build,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-600,12),new Vector2(600,108));var buildColumn=Column(build);selection=Label(buildColumn,"建设设备 · 右键/中键拖动 · 滚轮缩放",16,24);var buildRow=Row(Box("Categories",buildColumn,false));
            foreach(var b in game.Config.Economy.Hardware.Buildings){var kind=b.Kind;Button(buildRow,b.Name+"\n"+HardwareGame.Money(b.Cost),()=>game.BeginBuild(kind),136,48);}
            notices=Box("Notifications",canvas.transform);Anchor(notices,new Vector2(1,1),Vector2.one,new Vector2(-356,-226),new Vector2(-12,-72));var nc=Column(notices);notice=Label(nc,"",16,92);Button(nc,"处理供料 / 查看采购",()=>game.SetDrawer("采购"),0,34);
            driving=Box("Driving",canvas.transform);Anchor(driving,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-370,12),new Vector2(370,128));var dc=Column(driving);driveInfo=Label(dc,"",18,50);var dr=Row(Box("Actions",dc,false));Button(dr,"装卸 E",()=>game.Submit(new WorldCommand{Kind=CommandKind.Interact}),130);Button(dr,"原地回正",()=>game.Submit(new WorldCommand{Kind=CommandKind.RightVehicle}),140);Button(dr,"停靠 / Tab",game.ToggleDriving,140);
            pause=Box("Pause",canvas.transform);Anchor(pause,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-220,-280),new Vector2(220,280));var pc=Column(pause);Label(pc,"已暂停 · 全部业务冻结",24,48);Button(pc,"继续",game.TogglePause);Button(pc,"保存显卡战役",()=>game.Save());Button(pc,"读取显卡战役",()=>game.Load());Button(pc,"新战役（再次点击确认）",()=>{if(confirmNew){game.NewGame();confirmNew=false;}else{confirmNew=true;game.Message="再次点击新战役确认；下次保存才更新显卡存档";}});Button(pc,"旧 EP04 经营原型",game.OpenLegacy);Button(pc,"界面缩放 100 / 115 / 130%",()=>{zoom=zoom>=1.3f?1:zoom+.15f;scaler.referenceResolution=new Vector2(1920/zoom,1080/zoom);});Button(pc,"退出",()=>{game.Save();Application.Quit();});pause.gameObject.SetActive(false);
            RebuildDrawer();Refresh();
        }
        private RectTransform Box(string name,Transform parent,bool background=true)
        {
            var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);var rt=(RectTransform)obj.transform;if(background){var image=obj.AddComponent<UnityEngine.UI.Image>();image.color=panel;}return rt;
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
        private void Button(Transform parent,string label,Action action,float width=0,float height=40)
        {
            var r=Box(label,parent);r.GetComponent<UnityEngine.UI.Image>().color=new Color(.18f,.21f,.19f);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.onClick.AddListener(()=>action());var e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.preferredHeight=height;e.minHeight=height;if(width>0)e.preferredWidth=width;else e.flexibleWidth=1;
            var t=Label(r,label,width>0?16:18,height);Stretch((RectTransform)t.transform,4);t.alignment=TextAlignmentOptions.Center;
            if(label.Contains("\n¥"))
            {
                var index=Array.FindIndex(game.Config.Economy.Hardware.Buildings,b=>label.StartsWith(b.Name+"\n"));
                for(int i=0;i<4;i++)
                {
                    var mark=Box("Category icon",r);mark.GetComponent<UnityEngine.UI.Image>().color=accent;mark.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
                    mark.anchorMin=mark.anchorMax=new Vector2(0,.5f);mark.sizeDelta=new Vector2(index%2==0?6:18,index%2==0?18:3);mark.anchoredPosition=new Vector2(index%2==0?7+i*6:17,index%2==0?(i%2)*4-2:-10+i*6);
                }
                var rt=(RectTransform)t.transform;rt.offsetMin=new Vector2(34,4);
            }
        }
        public void ShowPause(bool value){pause.gameObject.SetActive(value);if(value)pause.SetAsLastSibling();}
        public void RebuildDrawer()
        {
            foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}details=null;drawer.gameObject.SetActive(game.Drawer!=""&&!game.Truck.Manual);if(!drawer.gameObject.activeSelf)return;
            Label(content,game.Drawer,26,42);Button(content,"关闭",()=>{game.Drawer="";RebuildDrawer();});
            details=Label(content,"",18,220);
            if(game.Drawer=="设备")
            {
                Button(content,"采购缺料",()=>{var n=game.World.Node(game.SelectedNode);var r=game.Config.Economy.Recipe(n?.RecipeId);if(r!=null)material=r.Inputs.First().Material;game.Drawer="采购";RebuildDrawer();});
                Button(content,"切换配方",()=>{var n=game.World.Node(game.SelectedNode);if(n==null)return;var rs=game.Config.Economy.Recipes.Where(r=>r.Machine==n.Kind).ToArray();if(rs.Length>0)game.Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=n.Id,Recipe=rs[(Array.FindIndex(rs,r=>r.Id==n.RecipeId)+1)%rs.Length].Id});});
                Button(content,"查看上游",()=>{var route=game.World.State.Routes.FirstOrDefault(r=>r.To==game.SelectedNode);if(route!=null)game.SelectedNode=route.From;});
                Button(content,"移动 / 旋转",game.BeginMove);Button(content,"升级",()=>game.Submit(new WorldCommand{Kind=CommandKind.Upgrade,Node=game.SelectedNode}));
                Button(content,"拆除：回收库存，返还投入75%",()=>game.Submit(new WorldCommand{Kind=CommandKind.Remove,Node=game.SelectedNode}));Button(content,"取消等待改建",()=>game.Submit(new WorldCommand{Kind=CommandKind.CancelChange,Node=game.SelectedNode}));
            }
            if(game.Drawer=="订单")
            {
                foreach(var o in game.World.State.Hardware.Orders.Where(o=>o.Phase!=HardwareOrderPhase.Delivered&&o.Phase!=HardwareOrderPhase.Cancelled&&o.Phase!=HardwareOrderPhase.Expired)){var id=o.Id;Button(content,o.Name,()=>{game.SelectedOrder=id;Refresh();},0,48);}
                foreach(var pair in new[]{("接单",CommandKind.AcceptOrder),("预留合格库存",CommandKind.ReserveOrder),("整单交付",CommandKind.DeliverOrder),("释放预留",CommandKind.ReleaseOrder),("取消订单（库存保留）",CommandKind.CancelOrder)}){var kind=pair.Item2;Button(content,pair.Item1,()=>{game.Submit(new WorldCommand{Kind=kind,Order=game.SelectedOrder,Factory=game.Factory});RebuildDrawer();});}
            }
            if(game.Drawer=="采购")
            {
                Button(content,"切换采购物料",()=>{var ms=game.Config.Economy.Hardware.Materials.Where(m=>m.Price>0).Select(m=>m.Id).ToArray();material=ms[(Array.IndexOf(ms,material)+1)%ms.Length];});
                var qr=Row(Box("Quantity",content,false));Button(qr,"−1",()=>quantity=Math.Max(1,quantity-1),80);Button(qr,"+1",()=>quantity=Math.Min(80,quantity+1),80);Button(qr,"数量 6 / 8 / 12",()=>quantity=quantity==6?8:quantity==8?12:6,132);
                Button(content,"切换：常规 / 加急 / 自提",()=>delivery=(delivery+1)%3);
                Button(content,"支付并采购",()=>game.Submit(new WorldCommand{Kind=CommandKind.Purchase,Factory=game.Factory,Material=material,Quantity=quantity,Delivery=delivery}));
            }
            if(game.Drawer=="产品")
            {
                Button(content,"切换产线：标准 / 静音",()=>{var nodes=game.World.State.Nodes.Where(n=>n.Factory==game.Factory&&(n.Kind==BuildingKind.GPUAssembly||n.Kind==BuildingKind.Tester||n.Kind==BuildingKind.Packaging)).ToArray();var quiet=nodes.Any(n=>!n.RecipeId.StartsWith("quiet-"));foreach(var n in nodes){var id=n.Kind==BuildingKind.GPUAssembly?"gpu":n.Kind==BuildingKind.Tester?"test":"pack";game.Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=n.Id,Recipe=(quiet?"quiet-":"")+id});}});
                Button(content,"回收 1 张未预留标准版",()=>game.Submit(new WorldCommand{Kind=CommandKind.SpotSell,Factory=game.Factory,Material=HardwareCatalog.Standard,Quantity=1}));
                Button(content,"回收 1 张未预留静音版",()=>game.Submit(new WorldCommand{Kind=CommandKind.SpotSell,Factory=game.Factory,Material=HardwareCatalog.Quiet,Quantity=1}));
                Button(content,"回收 1 份铝原料（恢复现金）",()=>game.Submit(new WorldCommand{Kind=CommandKind.SpotSell,Factory=game.Factory,Material=HardwareCatalog.Ore,Quantity=1}));
            }
            if(game.Drawer=="运输")
            {
                Button(content,"接管 / 继续驾驶 Tab",game.ToggleDriving);Button(content,"救援返回车库 · 耗时60秒",()=>game.Submit(new WorldCommand{Kind=CommandKind.RecoverVehicle}));
                Button(content,"自动采集铝原料",()=>game.Submit(new WorldCommand{Kind=CommandKind.Collect,Resource=1,TargetFactory=game.Factory}));
                Button(content,"自动返回当前工厂",()=>game.Submit(new WorldCommand{Kind=CommandKind.Return,TargetFactory=game.Factory}));
                Button(content,"购买第二工厂",()=>game.Submit(new WorldCommand{Kind=CommandKind.BuyFactory,Factory=2}));
                Button(content,"调拨铝材到另一工厂",()=>game.Submit(new WorldCommand{Kind=CommandKind.Transfer,Factory=game.Factory,TargetFactory=game.Factory==1?2:1,Material=HardwareCatalog.Aluminum}));
            }
            Refresh();
        }
        private static string Phase(HardwareOrderPhase p)=>new[]{"可接","进行中","可交付","已交付","已过期","已取消"}[(int)p];
        public void Refresh()
        {
            if(top==null||game.World==null)return;var w=game.World;var h=w.State.Hardware;
            top.text="ASSEMBLY ZERO     "+HardwareGame.Money(w.State.Money)+"    近5分钟现金 "+HardwareGame.Money(w.RecentCash)+"    "+(w.State.Tick/3600)+":"+(w.State.Tick/60%60).ToString("00")+"   "+(game.Paused?"暂停":game.Speed+"×")+"   "+game.Factory+"号厂";
            var tracked=h.Orders.FirstOrDefault(o=>o.Phase==HardwareOrderPhase.Active||o.Phase==HardwareOrderPhase.Ready);
            goal.text=tracked==null?"当前目标\n接下首批显卡订单\n首单缺 6 个散热器\n可自制，或采购补料":tracked.Name+"\n预留 "+w.ReservedCount(tracked.Id)+" / "+tracked.Quantity+"   剩余 "+Math.Max(0,(tracked.Deadline-w.State.Tick)/60)+" 秒\n下一步："+(w.ReservedCount(tracked.Id)==tracked.Quantity?"订单页交付":"补料 → 生产 → 预留成品");
            notice.text=string.Join("\n",game.Notices.Reverse());notices.gameObject.SetActive(game.Drawer==""&&game.Notices.Length>0&&!game.Truck.Manual);
            build.gameObject.SetActive(!game.Truck.Manual);driving.gameObject.SetActive(game.Truck.Manual);goals.gameObject.SetActive(true);
            selection.text=game.Placing.HasValue?"放置 "+game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==game.Placing).Name+" · 输入青色 / 输出琥珀 · R旋转 · Esc取消":"建设设备 · 右键/中键拖动 · 滚轮缩放";
            var v=game.Truck;driveInfo.text="货车  "+Math.Abs(v.Speed*4.32).ToString("F0")+" km/h    载货 "+v.Cargo.Count+" / "+game.Config.Economy.VehicleCapacity+"\nWASD 行驶 · 北向道路：铝矿 → 采购站；南向返回车库";
            if(details==null)return;
            if(game.Drawer=="设备")
            {
                var n=w.Node(game.SelectedNode);if(n==null){details.text="未选择设备";return;}var recipe=game.Config.Economy.Recipe(n.RecipeId);
                details.text=game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name+" L"+n.Level+"\n生产："+(recipe?.Name??"物流服务")+"\n状态："+n.Status+"\n"+(recipe==null?"":string.Join("\n",recipe.Inputs.Select(i=>HardwareCatalog.Name(i.Material)+" 缺 "+Math.Max(0,i.Quantity-n.Input.Count(g=>g.Material==i.Material)))))+"\n输入 "+n.Input.Count+" / 输出 "+n.Output.Count+" / 加工 "+n.WorkRemaining/60+"s\n"+(recipe==null?"": "理论周期 "+recipe.Seconds+"s；未计入缺料/堵塞")+"\n"+(n.PendingRemove||n.PendingRecipe!=""?"改建等待当前批次完成":"");
            }
            if(game.Drawer=="订单")
            {
                var o=h.Orders.FirstOrDefault(o=>o.Id==game.SelectedOrder);if(o==null)return;var estimate=w.ReplacementCost(o.Material)*o.Quantity;
                details.text=o.Name+"\n"+HardwareCatalog.Name(o.Material)+" ×"+o.Quantity+" · "+Phase(o.Phase)+"\n可用合格库存 "+w.FinishedStock(o.Factory).Count(g=>g.Material==o.Material&&g.ReservedOrder==0)+" / 已预留 "+w.ReservedCount(o.Id)+"\n收入 "+HardwareGame.Money((long)o.Quantity*o.UnitPrice)+"\n预计补货+加工 "+HardwareGame.Money(estimate)+"\n预估毛利 "+HardwareGame.Money((long)o.Quantity*o.UnitPrice-estimate)+"（未计运费）\n交期估计未计缺料；超期无收入，无罚金\n期限 "+(o.Deadline==0?SettingsOrderSeconds(o.Template):Math.Max(0,(o.Deadline-w.State.Tick)/60))+" 秒";
            }
            if(game.Drawer=="采购")
            {
                var m=game.Config.Economy.Hardware.Materials.FirstOrDefault(m=>m.Id==material&&m.Price>0);if(m==null){material=HardwareCatalog.Heat;m=game.Config.Economy.Hardware.Materials.First(m=>m.Id==material);}var config=game.Config.Economy.Hardware;var fee=delivery==0?config.DeliveryFee:delivery==1?config.ExpressFee:config.PickupFee;
                details.text="青港元件供应站\n"+m.Name+" ×"+quantity+"\n单价 "+HardwareGame.Money(m.Price)+" · 库存 "+h.Stock.First(s=>s.Material==material).Quantity+"\n"+new[]{"常规 90秒","加急 25秒","自提：驾驶到采购站"}[delivery]+"\n运费 "+HardwareGame.Money(fee)+"\n合计 "+HardwareGame.Money((long)m.Price*quantity+fee)+"\n固定120秒补货；仓满则等待\n在途 / 待提："+h.Deliveries.Sum(d=>d.Cargo.Count)+" 件\n采购即扣款；自提允许分次装载";
            }
            if(game.Drawer=="产品")details.text="星铸 S1 · 原创游戏规格\n标准：成本较低，普通订单\n静音：低噪散热器，低噪客户\nGPU核心与显存分别采购\n\n开局赠送：6座基础设施，8套核心/显存/板材/外壳/包装，2个标准散热器；账面成本为0。\n当前回收额度 "+h.SpotRemaining+" 件 / 每5分钟\n切换等待批次完成；分别检查三台设备状态";
            if(game.Drawer=="运输")details.text="货车 #1 · "+v.Status+"\n载货 "+v.Cargo.Count+" / "+game.Config.Economy.VehicleCapacity+"\n车库 (1,16)\n铝原料点 (1,27)\n采购站 (1,37)\n沿西侧道路向北行驶\n自提货物 "+h.Deliveries.Where(d=>d.Method==2).Sum(d=>d.Cargo.Count)+" 件\n驾驶固定1×；Tab停靠后工厂继续\n二厂需完成首单后购买";
            if(game.Drawer=="报表")details.text="现金流 ≠ 利润\n近5分钟净流入 "+HardwareGame.Money(w.RecentCash)+"\n累计收入 "+HardwareGame.Money(w.State.Revenue)+"\n累计现金支出 "+HardwareGame.Money(w.State.Expenses)+"\n设备投资 "+HardwareGame.Money(w.State.Nodes.Sum(n=>n.Investment))+"\n\n"+string.Join("\n",h.Settlements.TakeLast(3).Select(s=>"订单 #"+s.Order+" 收入 "+HardwareGame.Money(s.Income)+"\n材料 "+HardwareGame.Money(s.MaterialCost)+" 加工 "+HardwareGame.Money(s.ProcessingCost)+" 运费 "+HardwareGame.Money(s.FreightCost)+"\n实际毛利 "+HardwareGame.Money(s.Margin)));
        }
        private void LateUpdate(){if(details!=null){var e=details.GetComponent<UnityEngine.UI.LayoutElement>();e.preferredHeight=Mathf.Max(220,details.preferredHeight+8);}}
        private int SettingsOrderSeconds(int id)=>game.Config.Economy.Hardware.Orders.First(t=>t.Id==id).Seconds;
    }
}
