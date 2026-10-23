using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareHud
    {
        public const string TutorialPreference="AssemblyZero.Hardware.TutorialEnabled";
        private readonly Dictionary<string,RectTransform> guideTargets=new Dictionary<string,RectTransform>();
        private TMP_Text tutorialText,tutorialToggle,menuTutorialToggle;
        private RectTransform guideBorder;
        private CanvasGroup borderAlpha;
        private RectTransform highlighted;
        private bool completedNotified;
        private HardwareHint tutorialHint;
        public bool TutorialEnabled {get;private set;}
        public string CurrentGuideTarget {get;private set;}="";
        public bool TutorialComplete=>game.World.State.Hardware.Orders.Any(o=>o.Template==1&&o.Phase==HardwareOrderPhase.Delivered);

        private void InitializeNavigation()
        {
            TutorialEnabled=PlayerPrefs.GetInt(TutorialPreference,1)!=0;
            goals.offsetMin=new Vector2(84,-416);
            var column=goal.transform.parent;
            tutorialText=Label(column,"",15,64);
            tutorialToggle=Button(column,"",ToggleTutorial,0,32).GetComponentInChildren<TMP_Text>();
            menuTutorialToggle=Button(pause.GetComponentInChildren<VerticalLayoutGroup>().transform,"",ToggleTutorial).GetComponentInChildren<TMP_Text>();
            var mapPanel=Box("Minimap panel",canvas.transform);
            mapPanel.pivot=Vector2.zero;
            Anchor(mapPanel,Vector2.zero,Vector2.zero,new Vector2(84,128),new Vector2(354,392));
            drawer.offsetMin=new Vector2(-356,128);
            var title=Label(mapPanel,"全局小地图 · 点击定位   ↑北",15,26);Anchor((RectTransform)title.transform,new Vector2(0,1),Vector2.one,new Vector2(8,-30),new Vector2(-8,-4));
            var mapObject=new GameObject("Minimap",typeof(RectTransform));mapObject.transform.SetParent(mapPanel,false);
            var mapRect=(RectTransform)mapObject.transform;Stretch(mapRect);mapRect.offsetMin=new Vector2(8,42);mapRect.offsetMax=new Vector2(-8,-30);
            mapObject.AddComponent<RectMask2D>();mapObject.AddComponent<HardwareMinimap>().Initialize(game);
            var legend=Label(mapPanel,"青：工厂  金：矿区  紫：采购站\n白：皮卡  青框：当前视野",12,34);Anchor((RectTransform)legend.transform,Vector2.zero,new Vector2(1,0),new Vector2(8,4),new Vector2(-8,38));
            guideTargets["next"]=(RectTransform)guideAction.transform.parent;
            guideTargets["progress"]=(RectTransform)goal.transform;
            guideBorder=Box("Tutorial highlight",canvas.transform,false);
            guideBorder.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;borderAlpha=guideBorder.gameObject.AddComponent<CanvasGroup>();borderAlpha.blocksRaycasts=false;borderAlpha.interactable=false;
            for(var i=0;i<4;i++)
            {
                var edge=Box("Highlight edge",guideBorder);var image=edge.GetComponent<Image>();image.color=accent;image.raycastTarget=false;
                if(i<2)Anchor(edge,new Vector2(0,i),new Vector2(1,i),new Vector2(0,i==0?0:-2),new Vector2(0,i==0?2:0));
                else Anchor(edge,new Vector2(i-2,0),new Vector2(i-2,1),new Vector2(i==2?0:-2,0),new Vector2(i==2?2:0,0));
            }
            guideBorder.gameObject.SetActive(false);
            // Keep pause and its blocker above map, labels, and tutorial decoration.
            modal.SetAsLastSibling();
        }
        public void SetTutorialEnabled(bool value)
        {
            TutorialEnabled=value;PlayerPrefs.SetInt(TutorialPreference,value?1:0);PlayerPrefs.Save();UpdateTutorial();
        }
        public void ToggleTutorial()=>SetTutorialEnabled(!TutorialEnabled);
        private void ClearDrawerGuide()
        {
            if(guideBorder!=null){guideBorder.SetParent(canvas.transform,false);guideBorder.gameObject.SetActive(false);}highlighted=null;CurrentGuideTarget="";
            foreach(var key in guideTargets.Keys.Where(k=>k.StartsWith("drawer:")).ToArray())guideTargets.Remove(key);
        }
        private string TargetFor(HardwareHint hint,out string instruction)
        {
            instruction="点击高亮位置，继续当前步骤。";
            var order=game.World.State.Hardware.Orders.FirstOrDefault(o=>o.Id==hint.Order);
            if(order!=null&&order.Factory!=game.Factory){instruction="点击“下一步”，前往订单所属工厂。";return "next";}
            string page=hint.Key=="order"||hint.Key=="reserve"||hint.Key=="deliver"?"订单":hint.Key=="supply"||hint.Key=="arrival"?"采购":hint.Key=="mining"||hint.Key=="pickup"?"运输":hint.Key=="recipe"?"产品":"设备";
            if(hint.Key=="arrival"||hint.Key=="produce")
            {
                instruction=hint.Key=="arrival"?"物料已采购，等待到货即可，无需重复付款。":"产线正在自动生产；可用顶部 2× 加速。";return "progress";
            }
            if(hint.Key=="build")
            {
                var placingRequired=game.Placing==hint.Building&&!game.Moving;
                instruction=placingRequired?"在工厂空地左键放置；R旋转设备，Esc取消。":"点击高亮设备，再选择工厂空地放置。";
                return placingRequired?"progress":"build:"+hint.Building;
            }
            if(hint.Key=="blocked")
            {
                instruction="点击“下一步”定位等待设备，检查缺料或堵塞原因。";
                if(game.SelectedNode!=hint.Node||game.Drawer!="设备")return "next";
                instruction="查看设备等待原因；可采购的原料通过“采购缺料”补充。";
                return "drawer:details";
            }
            if(game.Drawer!=page){instruction="打开“"+page+"”，或点击“下一步”自动定位。";return "nav:"+page;}
            if(page=="订单")
            {
                if(game.SelectedOrder!=hint.Order){instruction="先选择当前目标订单。";return "drawer:order:"+hint.Order;}
                return hint.Key=="order"?"drawer:接单":hint.Key=="reserve"?"drawer:预留合格库存":"drawer:整单交付";
            }
            if(hint.Key=="supply")
            {
                if(material!=hint.Material){instruction="切换到所缺物料，或点击“下一步”自动选好物料和数量。";return "drawer:material";}
                if(quantity!=Mathf.Clamp(hint.Quantity,1,80)){instruction="调整采购数量至 "+Mathf.Clamp(hint.Quantity,1,80)+"，或点击“下一步”自动填写。";return quantity<hint.Quantity?"drawer:plus":"drawer:minus";}
                instruction="核对物料、数量、配送和费用，点击支付并采购。";return "drawer:purchase";
            }
            if(hint.Key=="recipe")return "drawer:recipe";
            if(hint.Key=="mining")
            {
                if(game.Truck.Loop&&game.Truck.TargetFactory==game.Factory){instruction="皮卡已规划循环采集，等待采满返厂即可，无需重复派遣。";return "progress";}
                return "drawer:mining";
            }
            if(hint.Key=="pickup")
            {
                if(game.Truck.PickupTask){instruction="皮卡正在自动自提，等待装载、返厂和卸货即可。";return "progress";}
                return "drawer:pickup";
            }
            return "next";
        }
        private void UpdateTutorial()
        {
            if(tutorialText==null||game.World==null)return;
            tutorialToggle.text=TutorialEnabled?"关闭新手引导 [H]":"开启新手引导 [H]";
            menuTutorialToggle.text=TutorialEnabled?"新手引导：开 [H]":"新手引导：关 [H]";
            var complete=TutorialComplete;
            if(!complete)completedNotified=false;
            if(complete&&!completedNotified)
            {
                completedNotified=true;
                if(TutorialEnabled){game.Message="新手引导已完成！首单已交付，可以自由经营。";game.Notices=game.Notices.Concat(new[]{game.Message}).TakeLast(3).ToArray();}
            }
            string instruction="";var key=TutorialEnabled&&!complete?TargetFor(tutorialHint??HardwareGuide.Evaluate(game.World,game.Factory),out instruction):"";
            tutorialText.text=complete?"首单已交付 · 新手引导完成":!TutorialEnabled?"引导已关闭 · 按 H 可重新开启":instruction;
            if(TutorialEnabled&&!complete&&game.LastActionFailed)tutorialText.text="操作未完成："+game.Message;
            CurrentGuideTarget=key;
            guideTargets.TryGetValue(key,out var target);
            if(target==null||!target.gameObject.activeInHierarchy||game.Paused||!TutorialEnabled||complete)
            {
                guideBorder.gameObject.SetActive(false);highlighted=null;return;
            }
            if(highlighted!=target)
            {
                highlighted=target;guideBorder.SetParent(target,false);Stretch(guideBorder,1);guideBorder.SetAsLastSibling();
                Canvas.ForceUpdateCanvases();
                var scroll=target.GetComponentInParent<ScrollRect>();
                if(scroll!=null&&scroll.viewport!=null&&scroll.content!=null)
                {
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,target);var view=scroll.viewport.rect;
                    var shift=bounds.min.y<view.yMin?view.yMin-bounds.min.y:bounds.max.y>view.yMax?view.yMax-bounds.max.y:0;
                    var position=scroll.content.anchoredPosition;
                    if(scroll.vertical)position.y=Mathf.Clamp(position.y+shift,0,Mathf.Max(0,scroll.content.rect.height-view.height));
                    shift=bounds.min.x<view.xMin?view.xMin-bounds.min.x:bounds.max.x>view.xMax?view.xMax-bounds.max.x:0;
                    if(scroll.horizontal)position.x=Mathf.Clamp(position.x+shift,-Mathf.Max(0,scroll.content.rect.width-view.width),0);
                    scroll.content.anchoredPosition=position;scroll.StopMovement();
                }
            }
            guideBorder.gameObject.SetActive(true);borderAlpha.alpha=.65f+.35f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*3));
        }
    }
}
