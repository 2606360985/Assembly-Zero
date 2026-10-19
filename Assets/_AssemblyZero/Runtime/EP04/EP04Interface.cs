using System;
using System.Linq;
using AssemblyZero.Domain.EP04;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class EP04Game
    {
        private UnityEngine.UI.Text header,notice,guide,nodeInfo,worldInfo,resourceInfo,policyLabel,stockInfo;
        private GameObject nodePanel,mapPanel,buildPanel;
        private int policyMaterial=17,policySupplier,policyPriority,policyReserve,transferMaterial=8,transferSource=1,transferTarget=2;
        private readonly int[] materials={8,9,10,13,16,17,18,19};
        private RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));g.transform.SetParent(parent,false);
            var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=position;r.sizeDelta=size;
            g.GetComponent<UnityEngine.UI.Image>().color=color;return r;
        }
        private UnityEngine.UI.Text Text(Transform parent,string value,float x,float y,float width,float height,int size=18)
        {
            var g=new GameObject("Label",typeof(RectTransform),typeof(UnityEngine.UI.Text));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);
            var t=g.GetComponent<UnityEngine.UI.Text>();t.font=Config.Font;t.text=value;t.fontSize=size;t.color=new Color(.86f,.92f,.94f);t.raycastTarget=false;t.supportRichText=true;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        private UnityEngine.UI.Button Button(Transform parent,string label,float x,float y,float width,Action action,float height=38)
        {
            var r=Panel(label,parent,new Vector2(0,1),new Vector2(x,-y),new Vector2(width,height),new Color(.16f,.27f,.32f,.98f));
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.onClick.AddListener(()=>action());
            var text=Text(r,label,7,3,width-14,height-6,width<145?16:18);text.alignment=TextAnchor.MiddleCenter;return button;
        }
        private void CreateUI()
        {
            var canvas=new GameObject("EP04 Interface",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            if(FindFirstObjectByType<EventSystem>()==null){var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform,false);}
            var dark=new Color(.035f,.075f,.10f,.97f);
            var top=Panel("Top",canvas.transform,new Vector2(.5f,1),Vector2.zero,new Vector2(1920,72),dark);
            header=Text(top,"",24,14,880,52,22);
            Button(top,"一号厂",905,16,95,()=>SelectFactory(1));Button(top,"二号厂",1008,16,95,()=>SelectFactory(2));Button(top,"地图 / Tab",1111,16,125,()=>SetView(!MapMode));
            Button(top,"暂停 / Space",1244,16,135,()=>Paused=!Paused);Button(top,"1× / 2×",1387,16,95,()=>GameSpeed=GameSpeed==1?2:1);
            Button(top,"保存",1490,16,90,()=>Save());Button(top,"继续",1588,16,90,()=>Load());Button(top,"新游戏",1686,16,100,NewGame);
            var left=Panel("Build",canvas.transform,new Vector2(0,1),new Vector2(18,-90),new Vector2(270,850),dark);buildPanel=left.gameObject;
            Text(left,"零号工厂 / EP04",16,16,238,35,24);Text(left,"规划 · 生产 · 探索 · 扩张",16,54,240,34,17);
            Text(left,"建设节点",16,103,238,30,21);
            Button(left,"仓库 · 首座免费",16,146,238,()=>BeginBuild(BuildingKind.Warehouse));
            Button(left,"制造机 · "+Config.Economy.MachineCost,16,194,238,()=>BeginBuild(BuildingKind.Manufacturer));
            Button(left,"加工机 · "+Config.Economy.ProcessorCost,16,242,238,()=>BeginBuild(BuildingKind.Processor));
            Button(left,"销售站 · 首座免费",16,290,238,()=>BeginBuild(BuildingKind.Sales));
            Text(left,"点击空地放置\nR：旋转 90°\nEsc：取消放置\n点击设备：查看与调整",16,350,238,120);
            stockInfo=Text(left,"",16,493,238,270,18);
            Button(left,"前往世界地图",16,790,238,()=>SetView(true));
            var right=Panel("Node",canvas.transform,new Vector2(1,1),new Vector2(-18,-90),new Vector2(310,850),dark);nodePanel=right.gameObject;
            Text(right,"设备与物流",16,16,278,34,24);nodeInfo=Text(right,"",16,62,278,277,18);
            Button(right,"升级设备",16,347,134,()=>Submit(new WorldCommand{Kind=CommandKind.Upgrade,Node=SelectedNode}));
            Button(right,"切换配方",160,347,134,CycleRecipe);
            Button(right,"移动 / 旋转",16,395,134,BeginMove);Button(right,"拆除 · 返还75%",160,395,134,()=>Submit(new WorldCommand{Kind=CommandKind.Remove,Node=SelectedNode}));
            Button(right,"取消等待改建",16,443,278,()=>Submit(new WorldCommand{Kind=CommandKind.CancelChange,Node=SelectedNode}));
            Text(right,"供货与销售策略",16,500,278,30,21);policyLabel=Text(right,"",16,540,278,122,17);
            Button(right,"选择物料",16,674,134,()=>{policyMaterial=Next(materials,policyMaterial);ReadPolicy();});
            Button(right,"选择供应方",160,674,134,CycleSupplier);
            Button(right,"优先级 +1",16,722,134,()=>policyPriority=(policyPriority+1)%10);
            Button(right,"保留量 +2",160,722,134,()=>policyReserve=(policyReserve+2)%22);
            Button(right,"应用物流策略",16,780,278,()=>Submit(new WorldCommand{Kind=CommandKind.Policy,Node=SelectedNode,Material=policyMaterial,Supplier=policySupplier,Priority=policyPriority,Reserve=policyReserve}));
            var map=Panel("Expedition",canvas.transform,new Vector2(1,1),new Vector2(-18,-90),new Vector2(310,850),dark);mapPanel=map.gameObject;
            Text(map,"探索与运输",16,16,278,34,24);worldInfo=Text(map,"",16,60,278,162,17);
            Button(map,"切换车辆",16,226,134,()=>SelectedVehicle=Next(World.State.Vehicles.Select(v=>v.Id).ToArray(),SelectedVehicle));
            Button(map,"车辆加速",160,226,134,()=>Submit(new WorldCommand{Kind=CommandKind.Boost,Vehicle=SelectedVehicle}));
            resourceInfo=Text(map,"",16,276,278,105,17);
            Button(map,"切换资源点",16,386,134,()=>SelectedResource=Next(World.State.Resources.Where(r=>r.Discovered).Select(r=>r.Id).ToArray(),SelectedResource));
            Button(map,"采集至当前厂",160,386,134,()=>Submit(new WorldCommand{Kind=CommandKind.Collect,Vehicle=SelectedVehicle,Resource=SelectedResource,TargetFactory=SelectedFactory}));
            Button(map,"返当前厂卸货",16,434,134,()=>Submit(new WorldCommand{Kind=CommandKind.Return,Vehicle=SelectedVehicle,TargetFactory=SelectedFactory}));
            Button(map,"建设采集站",160,434,134,()=>Submit(new WorldCommand{Kind=CommandKind.Station,Resource=SelectedResource}));
            Button(map,"雇采集队 · "+Config.Economy.CollectorCost,16,482,278,()=>Submit(new WorldCommand{Kind=CommandKind.HireCollector,Resource=SelectedResource,Factory=SelectedFactory,TargetFactory=SelectedFactory}));
            Button(map,"切换调拨物料",16,538,134,()=>transferMaterial=Next(materials,transferMaterial));
            Button(map,"交换两厂方向",160,538,134,()=>{var t=transferSource;transferSource=transferTarget;transferTarget=t;});
            Button(map,"当前车辆调拨",16,586,278,()=>Submit(new WorldCommand{Kind=CommandKind.Transfer,Vehicle=SelectedVehicle,Factory=transferSource,TargetFactory=transferTarget,Material=transferMaterial}));
            Button(map,"雇运输队 · "+Config.Economy.HaulerCost,16,634,278,()=>Submit(new WorldCommand{Kind=CommandKind.HireHauler,Factory=transferSource,TargetFactory=transferTarget,Material=transferMaterial}));
            Button(map,"购买第二厂 · "+Config.Economy.SecondFactoryCost,16,690,278,()=>Submit(new WorldCommand{Kind=CommandKind.BuyFactory,Factory=2}));
            Button(map,"返回当前工厂",16,782,278,()=>SetView(false));
            var bottom=Panel("Goals",canvas.transform,new Vector2(.5f,0),Vector2.zero,new Vector2(1920,118),dark);
            guide=Text(bottom,"",24,12,1872,45,19);notice=Text(bottom,"",24,62,1872,45,20);
        }
        private static int Next(int[] values,int current){if(values.Length==0)return current;return values[(Math.Max(-1,Array.IndexOf(values,current))+1)%values.Length];}
        private void CycleRecipe()
        {
            var n=World.Node(SelectedNode);if(n==null)return;
            var recipes=Config.Economy.Recipes.Where(r=>r.Machine==n.Kind&&r.Level<=n.Level&&r.Level<=World.State.Unlocked).ToArray();if(recipes.Length==0)return;
            var index=Array.FindIndex(recipes,r=>r.Id==n.RecipeId);Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=n.Id,Recipe=recipes[(index+1)%recipes.Length].Id});
        }
        private void ReadPolicy(){var p=World.Node(SelectedNode)?.Policy(policyMaterial);policySupplier=p?.Supplier??0;policyPriority=p?.Priority??0;policyReserve=p?.Reserve??0;}
        private void CycleSupplier()
        {
            var n=World.Node(SelectedNode);if(n==null)return;
            var options=new[]{0}.Concat(World.State.Nodes.Where(s=>s.Factory==n.Factory&&s.Id!=n.Id&&(s.Kind==BuildingKind.Warehouse||Config.Economy.Recipe(s.RecipeId)?.Output==policyMaterial)).Select(s=>s.Id)).ToArray();policySupplier=Next(options,policySupplier);
        }
        private string Inventory(System.Collections.Generic.IEnumerable<Goods> goods)=>string.Join("  ",goods.GroupBy(i=>i.Material).OrderBy(g=>g.Key).Select(g=>EconomySettings.MaterialName(g.Key)+"×"+g.Count()));
        private static string KindName(BuildingKind kind)=>kind==BuildingKind.Warehouse?"仓库":kind==BuildingKind.Manufacturer?"制造机":kind==BuildingKind.Processor?"加工机":"销售站";
        private void RefreshUI()
        {
            if(header==null||World==null)return;var s=World.State;
            header.text="ASSEMBLY ZERO  /  EP04      ¥ "+s.Money+"     累计净收益 "+(s.Revenue-s.Expenses)+"     "+(Paused?"已暂停":GameSpeed+"×")+"  ·  "+(MapMode?"世界地图":SelectedFactory+"号厂");
            notice.text=Message;nodePanel.SetActive(!MapMode);mapPanel.SetActive(MapMode);
            guide.text=(s.Gathered?"✓":"○")+" 采集   →   "+(s.Built?"✓":"○")+" 建造   →   "+(s.Sold>0?"✓":"○")+" 首次销售   →   "+(s.Upgraded?"✓":"○")+" 升级   →   "+(s.Hired?"✓":"○")+" 雇佣   →   "+(s.Complete?"✓ 第二厂销售：目标完成！":"○ 第二厂销售")+"     |     已解锁：T"+Math.Min(3,s.Unlocked)+(s.Unlocked>=4?" / 第二厂":"");
            stockInfo.text="当前厂库存\n"+Inventory(s.Nodes.Where(n=>n.Factory==SelectedFactory&&n.Kind==BuildingKind.Warehouse).SelectMany(n=>n.Output))+"\n\n仓库每种物料占用≤25%\n预留空间用于改建回收\n售价  T1 25 / T2 90 / T3 220\n\n最近动态\n"+string.Join("\n",s.Events.Skip(Math.Max(0,s.Events.Count-2)));
            var n=World.Node(SelectedNode);
            nodeInfo.text=n==null?"点击厂区中的设备。\n\n从仓库、制造机、销售站开始。\n系统会自动接好输送线。\n\n先放下游也可以，缺料时等待。\n不同生产链共享供料节点。":
                "#"+n.Id+" "+KindName(n.Kind)+"  L"+n.Level+"\n"+(Config.Economy.Recipe(n.RecipeId)?.Name??"物流节点")+"\n状态："+n.Status+"\n输入："+Inventory(n.Input)+"\n输出："+Inventory(n.Output)+"\n加工："+(n.Batch.Count>0?((n.WorkTotal-n.WorkRemaining)*100/Math.Max(1,n.WorkTotal))+"%":"—")+"\n连接："+s.Routes.Count(r=>r.From==n.Id||r.To==n.Id)+" 条\n"+(n.PendingRemove||n.PendingRecipe!=""?"等待本批次完成后改建":"")+"\n升级费用："+(n.Level==1?Config.Economy.Level2Cost:n.Level==2?Config.Economy.Level3Cost:0);
            policyLabel.text="物料："+EconomySettings.MaterialName(policyMaterial)+"\n供应方："+(policySupplier==0?"自动":"#"+policySupplier)+"\n接收优先级："+policyPriority+"\n销售前保留："+policyReserve+" 件";
            var v=s.Vehicles.FirstOrDefault(x=>x.Id==SelectedVehicle);var resource=s.Resources.FirstOrDefault(r=>r.Id==SelectedResource);
            if(v!=null)worldInfo.text="车辆 #"+v.Id+" · "+(v.Role==VehicleRole.Pickup?"皮卡":v.Role==VehicleRole.Collector?"采集队":"运输队")+"\n"+v.Status+" · "+(v.Boost?"加速":"普通速度")+"\n货舱 "+v.Cargo.Count+"/"+Config.Economy.VehicleCapacity+"  "+Inventory(v.Cargo)+"\n预计行驶 "+Math.Max(0,v.Path.Count-v.PathIndex-1)*(v.Boost?5:10)/60+" 秒\n地图空地：派遣皮卡探索\n送货目标："+SelectedFactory+"号厂";
            resourceInfo.text=(resource==null?"未选择资源":EconomySettings.MaterialName(resource.Material)+"  储量 "+resource.Stock+"/"+resource.Capacity+"\n"+(resource.Station?"采集站已建":"建站费用 "+Config.Economy.StationCost))+"\n调拨 "+transferSource+" → "+transferTarget+"："+EconomySettings.MaterialName(transferMaterial)+"\n团队每趟费用 "+Config.Economy.TripFee;
        }
    }
}
