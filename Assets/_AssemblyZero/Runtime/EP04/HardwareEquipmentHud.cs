using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareHud
    {
        public const string BuildShortcutPreference="AssemblyZero.Hardware.BuildShortcuts.v1";
        [Serializable] private sealed class ShortcutSettings { public int[] Slots; }
        private HardwareBuildShortcuts buildShortcuts;
        private readonly UnityEngine.UI.Button[] shortcutButtons=new UnityEngine.UI.Button[HardwareBuildShortcuts.SlotCount];
        private readonly TMP_Text[] shortcutTexts=new TMP_Text[HardwareBuildShortcuts.SlotCount];
        private readonly UnityEngine.UI.Image[] shortcutIcons=new UnityEngine.UI.Image[HardwareBuildShortcuts.SlotCount];
        private RectTransform equipmentBackdrop,equipmentPanel,equipmentGrid,equipmentDragGhost;
        private UnityEngine.UI.GridLayoutGroup equipmentGridLayout;
        private UnityEngine.UI.ScrollRect equipmentScroll;
        private UnityEngine.UI.Button allEquipmentButton;
        private TMP_Text equipmentTitle,equipmentHint,equipmentEmpty;
        private TMP_InputField equipmentSearch;
        private string equipmentCategory="全部",equipmentQuery="";
        private int replaceShortcut=-1;
        private BuildingKind? pinEquipment,dragEquipment,lastEquipmentGuide;
        private readonly Dictionary<BuildingKind,(RectTransform Root,TMP_Text Pin)> equipmentCards=new Dictionary<BuildingKind,(RectTransform,TMP_Text)>();
        private readonly Dictionary<string,UnityEngine.UI.Button> equipmentTabs=new Dictionary<string,UnityEngine.UI.Button>();
        private static readonly Key[] ShortcutKeys={Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7};
        public bool EquipmentLibraryOpen=>equipmentBackdrop!=null&&equipmentBackdrop.gameObject.activeSelf;
        public bool EquipmentDragging=>dragEquipment.HasValue;
        public BuildingKind? BuildShortcutAt(int slot)=>buildShortcuts?.At(slot);

        private static string EquipmentCategory(BuildingKind kind)
        {
            switch(kind)
            {
                case BuildingKind.Warehouse:return "仓储物流";
                case BuildingKind.Processor:case BuildingKind.Manufacturer:return "材料加工";
                case BuildingKind.BoardAssembly:case BuildingKind.GPUAssembly:return "装配制造";
                case BuildingKind.Tester:case BuildingKind.Rework:return "检测返修";
                case BuildingKind.Packaging:case BuildingKind.Sales:return "包装交付";
                default:return "其他";
            }
        }

        private static string EquipmentIcon(BuildingKind kind)
        {
            switch(kind)
            {
                case BuildingKind.Warehouse:case BuildingKind.Packaging:return "box";
                case BuildingKind.Processor:return "factory";
                case BuildingKind.Manufacturer:return "heat";
                case BuildingKind.BoardAssembly:return "chip";
                case BuildingKind.GPUAssembly:return "gpu";
                case BuildingKind.Tester:return "order";
                case BuildingKind.Sales:return "market";
                default:return "wrench";
            }
        }

        private TMP_Text EquipmentText(Transform parent,string text,int size=16)
        {
            var rect=Box("Text",parent,false);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font=game.Font;label.fontSize=size;label.color=ink;label.text=text;label.raycastTarget=false;
            label.overflowMode=TextOverflowModes.Ellipsis;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }

        private void InitializeBuildShortcuts(RectTransform parent)
        {
            int[] saved=null;
            try{if(PlayerPrefs.HasKey(BuildShortcutPreference))saved=JsonUtility.FromJson<ShortcutSettings>(PlayerPrefs.GetString(BuildShortcutPreference))?.Slots;}
            catch(ArgumentException){/* A damaged UI preference must not prevent the campaign from opening. */}
            buildShortcuts=new HardwareBuildShortcuts(game.Config.Economy.Hardware.Buildings,saved);
            buildViewport=Row(Box("Build shortcuts",parent,false));
            buildViewport.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=56;
            for(var slot=0;slot<HardwareBuildShortcuts.SlotCount;slot++)
            {
                var index=slot;var button=Button(buildViewport,"",()=>ActivateBuildShortcut(index),0,48);
                button.name="Build shortcut "+(slot+1);shortcutButtons[slot]=button;
                var text=button.GetComponentInChildren<TMP_Text>();shortcutTexts[slot]=text;text.fontSize=14;text.alignment=TextAlignmentOptions.MidlineLeft;
                var rect=(RectTransform)text.transform;rect.offsetMin=new Vector2(36,3);rect.offsetMax=new Vector2(-5,-3);
                shortcutIcons[slot]=Icon((RectTransform)button.transform,"dot",accent,23,Vector2.zero,new Vector2(0,.5f),new Vector2(17,0));
                var number=EquipmentText(button.transform,(slot+1).ToString(),10);number.color=new Color(.55f,.68f,.76f);
                Anchor((RectTransform)number.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(4,-14),new Vector2(14,-2));
                var handle=button.gameObject.AddComponent<HardwareBuildDragHandle>();handle.Hud=this;handle.Slot=slot;
            }
            allEquipmentButton=Button(buildViewport,"全部设备",ToggleEquipmentLibrary,138,48,"build:all");
            allEquipmentButton.name="全部设备";
            var colors=allEquipmentButton.colors;colors.normalColor=new Color(.045f,.4f,.48f);colors.highlightedColor=new Color(.06f,.55f,.63f);allEquipmentButton.colors=colors;
            RefreshBuildShortcuts();
        }

        private void RefreshBuildShortcuts()
        {
            for(var i=0;i<shortcutButtons.Length;i++)
            {
                var kind=buildShortcuts.At(i);var definition=game.Config.Economy.Hardware.Buildings.FirstOrDefault(b=>b.Kind==kind);
                shortcutTexts[i].text=definition==null?"空槽位\n<size=11>点击配置</size>":definition.Name+"\n<size=12>"+HardwareGame.Money(definition.Cost)+"</size>";
                shortcutIcons[i].sprite=HardwareIcons.Get(kind.HasValue?EquipmentIcon(kind.Value):"dot");
                var handle=shortcutButtons[i].GetComponent<HardwareBuildDragHandle>();handle.HasDevice=kind.HasValue;handle.Kind=kind??default;
            }
            RegisterEquipmentGuideTargets();
        }

        private void RegisterEquipmentGuideTargets()
        {
            if(buildShortcuts==null)return;
            foreach(var definition in game.Config.Economy.Hardware.Buildings)
            {
                var index=buildShortcuts.IndexOf(definition.Kind);
                guideTargets["build:"+definition.Kind]=index>=0?(RectTransform)shortcutButtons[index].transform:
                    EquipmentLibraryOpen&&equipmentCards.TryGetValue(definition.Kind,out var card)&&card.Root.gameObject.activeSelf?card.Root:(RectTransform)allEquipmentButton.transform;
            }
        }

        public void AssignBuildShortcut(int slot,BuildingKind kind)
        {
            buildShortcuts.Assign(slot,kind);
            PlayerPrefs.SetString(BuildShortcutPreference,JsonUtility.ToJson(new ShortcutSettings{Slots=buildShortcuts.Export()}));PlayerPrefs.Save();
            pinEquipment=null;replaceShortcut=-1;RefreshBuildShortcuts();RefreshEquipmentLibrary();
            if(equipmentHint!=null)equipmentHint.text="已将「"+game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==kind).Name+"」固定到槽位 "+(slot+1)+"。拖动槽位可交换位置。";
        }

        public void ActivateBuildShortcut(int slot)
        {
            if(game.Paused||game.Showcasing||game.Truck.Manual||slot<0||slot>=HardwareBuildShortcuts.SlotCount)return;
            if(pinEquipment.HasValue){AssignBuildShortcut(slot,pinEquipment.Value);return;}
            var kind=buildShortcuts.At(slot);
            if(kind.HasValue)game.BeginBuild(kind.Value);else OpenEquipmentLibraryForSlot(slot);
        }

        public bool HandleBuildShortcutKeys(Keyboard keyboard)
        {
            for(var i=0;i<ShortcutKeys.Length;i++)if(keyboard[ShortcutKeys[i]].wasPressedThisFrame){ActivateBuildShortcut(i);return true;}
            return false;
        }

        private void ToggleEquipmentLibrary()
        {
            if(EquipmentLibraryOpen)CloseEquipmentLibrary();else OpenEquipmentLibrary();
        }

        public void OpenEquipmentLibrary()
        {
            if(game.Paused||game.Showcasing||game.Truck.Manual)return;
            CloseProcurementOverview();
            var firstOpen=equipmentBackdrop==null;
            if(firstOpen)BuildEquipmentLibrary();
            equipmentBackdrop.gameObject.SetActive(true);equipmentBackdrop.SetAsLastSibling();build.SetAsLastSibling();modal.SetAsLastSibling();
            game.HoverNode=0;game.View?.HideGhost();game.Interaction=HardwareInteraction.Modal;
            RefreshEquipmentLibrary(firstOpen);
        }

        public void OpenEquipmentLibraryForSlot(int slot)
        {
            if(slot<0||slot>=HardwareBuildShortcuts.SlotCount||game.Paused)return;
            replaceShortcut=slot;pinEquipment=null;equipmentCategory="全部";equipmentQuery="";OpenEquipmentLibrary();
            if(equipmentSearch!=null)equipmentSearch.SetTextWithoutNotify("");
        }

        public void CloseEquipmentLibrary()
        {
            EndEquipmentDrag();
            if(!EquipmentLibraryOpen)return;
            if(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(equipmentBackdrop))EventSystem.current.SetSelectedGameObject(null);
            equipmentBackdrop.gameObject.SetActive(false);pinEquipment=null;replaceShortcut=-1;EndEquipmentDrag();
            game.Interaction=game.Paused?HardwareInteraction.Modal:game.Placing.HasValue?HardwareInteraction.Placing:game.Drawer=="设备"?HardwareInteraction.Selected:HardwareInteraction.Browsing;RegisterEquipmentGuideTargets();
        }

        private void BuildEquipmentLibrary()
        {
            equipmentBackdrop=Box("Equipment backdrop",canvas.transform);Stretch(equipmentBackdrop);equipmentBackdrop.GetComponent<UnityEngine.UI.Image>().color=new Color(0,.015f,.025f,.3f);
            var dismiss=equipmentBackdrop.gameObject.AddComponent<UnityEngine.UI.Button>();dismiss.transition=UnityEngine.UI.Selectable.Transition.None;dismiss.onClick.AddListener(CloseEquipmentLibrary);
            equipmentPanel=Box("Equipment library",equipmentBackdrop);equipmentPanel.anchorMin=equipmentPanel.anchorMax=new Vector2(.5f,0);equipmentPanel.pivot=new Vector2(.5f,0);equipmentPanel.anchoredPosition=new Vector2(0,128);
            equipmentPanel.gameObject.AddComponent<UnityEngine.UI.Button>().transition=UnityEngine.UI.Selectable.Transition.None;
            equipmentTitle=EquipmentText(equipmentPanel,"全部设备",24);Anchor((RectTransform)equipmentTitle.transform,new Vector2(0,1),Vector2.one,new Vector2(20,-50),new Vector2(-440,-10));
            var search=Box("Equipment search",equipmentPanel);Anchor(search,new Vector2(1,1),Vector2.one,new Vector2(-418,-47),new Vector2(-108,-11));
            search.GetComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.1f);
            var inputText=EquipmentText(search,"",15);Stretch((RectTransform)inputText.transform,8);
            var placeholder=EquipmentText(search,"搜索名称 / 类别",14);Stretch((RectTransform)placeholder.transform,8);placeholder.color=new Color(.47f,.61f,.7f);
            equipmentSearch=search.gameObject.AddComponent<TMP_InputField>();equipmentSearch.textViewport=(RectTransform)inputText.transform;equipmentSearch.textComponent=(TextMeshProUGUI)inputText;
            equipmentSearch.placeholder=placeholder;equipmentSearch.characterLimit=48;equipmentSearch.onValueChanged.AddListener(value=>{equipmentQuery=value.Trim();RefreshEquipmentLibrary(true);});
            var close=Button(equipmentPanel,"关闭",CloseEquipmentLibrary,76,36);Anchor((RectTransform)close.transform,Vector2.one,Vector2.one,new Vector2(-94,-47),new Vector2(-18,-11));
            var tabs=Row(Box("Equipment categories",equipmentPanel,false));Anchor(tabs,new Vector2(0,1),Vector2.one,new Vector2(12,-103),new Vector2(-12,-55));
            var categories=new[]{"全部","仓储物流","材料加工","装配制造","检测返修","包装交付","其他"};
            foreach(var category in categories.Where(c=>c=="全部"||game.Config.Economy.Hardware.Buildings.Any(b=>EquipmentCategory(b.Kind)==c)))
            {
                var target=category;var button=Button(tabs,category,()=>{equipmentCategory=target;RefreshEquipmentLibrary(true);},0,36);
                button.name="Equipment category "+category;button.GetComponentInChildren<TMP_Text>().fontSize=15;equipmentTabs[category]=button;
            }
            var scroll=Box("Equipment scroll",equipmentPanel,false);Anchor(scroll,Vector2.zero,Vector2.one,new Vector2(16,50),new Vector2(-16,-112));
            equipmentScroll=scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();equipmentScroll.horizontal=false;equipmentScroll.scrollSensitivity=38;equipmentScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport=Box("Viewport",scroll);Stretch(viewport);viewport.offsetMax=new Vector2(-14,0);viewport.GetComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.1f);viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            equipmentGrid=Box("Equipment cards",viewport,false);equipmentGrid.anchorMin=new Vector2(0,1);equipmentGrid.anchorMax=Vector2.one;equipmentGrid.pivot=new Vector2(.5f,1);equipmentGrid.sizeDelta=Vector2.zero;equipmentGrid.anchoredPosition=Vector2.zero;
            equipmentGridLayout=equipmentGrid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();equipmentGridLayout.padding=new RectOffset(8,8,8,8);equipmentGridLayout.spacing=new Vector2(10,10);equipmentGridLayout.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            equipmentGrid.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            equipmentScroll.viewport=viewport;equipmentScroll.content=equipmentGrid;
            var track=Box("Equipment scrollbar",scroll);Anchor(track,new Vector2(1,0),Vector2.one,new Vector2(-8,0),Vector2.zero);track.GetComponent<UnityEngine.UI.Image>().color=new Color(.12f,.19f,.24f);
            var handle=Box("Handle",track);Stretch(handle);handle.GetComponent<UnityEngine.UI.Image>().color=new Color(.21f,.51f,.59f);
            var scrollbar=track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            equipmentScroll.verticalScrollbar=scrollbar;equipmentScroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            equipmentEmpty=EquipmentText(viewport,"没有匹配设备\n试试其他分类或搜索词。",18);Stretch((RectTransform)equipmentEmpty.transform,20);equipmentEmpty.alignment=TextAlignmentOptions.Center;
            foreach(var definition in game.Config.Economy.Hardware.Buildings)
            {
                var kind=definition.Kind;var button=Button(equipmentGrid,"",()=>ChooseLibraryEquipment(kind),0,116);button.name="Equipment card "+kind;
                var title=button.GetComponentInChildren<TMP_Text>();title.fontSize=17;title.alignment=TextAlignmentOptions.MidlineLeft;
                title.text=definition.Name+"\n<size=14><color=#F2A33A>"+HardwareGame.Money(definition.Cost)+"</color></size>";
                var rect=(RectTransform)title.transform;rect.offsetMin=new Vector2(43,38);rect.offsetMax=new Vector2(-6,-8);
                Icon((RectTransform)button.transform,EquipmentIcon(kind),accent,26,new Vector2(0,17),new Vector2(0,.5f),new Vector2(23,0));
                var pin=Button(button.transform,"固定到快捷栏",()=>PinLibraryEquipment(kind),0,28);pin.name="Pin equipment "+kind;
                Anchor((RectTransform)pin.transform,Vector2.zero,new Vector2(1,0),new Vector2(6,6),new Vector2(-6,34));var pinText=pin.GetComponentInChildren<TMP_Text>();pinText.fontSize=12;
                var drag=button.gameObject.AddComponent<HardwareBuildDragHandle>();drag.Hud=this;drag.Kind=kind;
                equipmentCards[kind]=((RectTransform)button.transform,pinText);
            }
            equipmentHint=EquipmentText(equipmentPanel,"",13);Anchor((RectTransform)equipmentHint.transform,Vector2.zero,new Vector2(1,0),new Vector2(20,9),new Vector2(-20,43));
            equipmentBackdrop.gameObject.SetActive(false);
        }

        private void ChooseLibraryEquipment(BuildingKind kind)
        {
            if(game.Paused)return;
            if(replaceShortcut>=0){AssignBuildShortcut(replaceShortcut,kind);return;}
            game.BeginBuild(kind);
        }

        private void PinLibraryEquipment(BuildingKind kind)
        {
            if(game.Paused)return;
            if(replaceShortcut>=0){AssignBuildShortcut(replaceShortcut,kind);return;}
            pinEquipment=kind;equipmentTitle.text="选择底部槽位";equipmentHint.text="点击底部 1–7 任意槽位固定「"+game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==kind).Name+"」；已有同类设备会交换位置。";
        }

        private void RefreshEquipmentLibrary(bool resetScroll=false)
        {
            if(!EquipmentLibraryOpen)return;
            ResizeEquipmentLibrary();var position=equipmentScroll.verticalNormalizedPosition;var count=0;
            foreach(var definition in game.Config.Economy.Hardware.Buildings)
            {
                if(!equipmentCards.TryGetValue(definition.Kind,out var card))continue;
                var shown=(equipmentCategory=="全部"||EquipmentCategory(definition.Kind)==equipmentCategory)&&
                    (equipmentQuery.Length==0||definition.Name.IndexOf(equipmentQuery,StringComparison.OrdinalIgnoreCase)>=0||
                    EquipmentCategory(definition.Kind).IndexOf(equipmentQuery,StringComparison.OrdinalIgnoreCase)>=0||definition.Kind.ToString().IndexOf(equipmentQuery,StringComparison.OrdinalIgnoreCase)>=0);
                card.Root.gameObject.SetActive(shown);if(shown)count++;
                var slot=buildShortcuts.IndexOf(definition.Kind);card.Pin.text=replaceShortcut>=0?"替换槽位 "+(replaceShortcut+1):slot>=0?"常用槽位 "+(slot+1)+" · 更换":"固定到快捷栏";
            }
            equipmentTitle.text=replaceShortcut>=0?"配置槽位 "+(replaceShortcut+1):pinEquipment.HasValue?"选择底部槽位":"全部设备  <size=14>"+count+" / "+equipmentCards.Count+"</size>";
            foreach(var pair in equipmentTabs){var colors=pair.Value.colors;colors.normalColor=pair.Key==equipmentCategory?new Color(.045f,.4f,.48f):new Color(.13f,.21f,.29f);pair.Value.colors=colors;}
            equipmentEmpty.gameObject.SetActive(count==0);
            if(!pinEquipment.HasValue)equipmentHint.text=replaceShortcut>=0?"点击设备卡片替换该槽位。布局仅在你主动调整时改变。":"点击设备进入放置 · 拖到下方槽位可固定 · 拖动常用槽位可交换 · 右键槽位可替换";
            Canvas.ForceUpdateCanvases();equipmentScroll.verticalNormalizedPosition=resetScroll?1:position;RegisterEquipmentGuideTargets();
        }

        private void ResizeEquipmentLibrary()
        {
            if(!EquipmentLibraryOpen)return;
            var bounds=((RectTransform)canvas.transform).rect;
            equipmentPanel.sizeDelta=new Vector2(Mathf.Min(1200,bounds.width-48),Mathf.Min(500,bounds.height-220));
            var width=equipmentPanel.rect.width-46;var columns=Mathf.Clamp(Mathf.FloorToInt((width+10)/185),2,6);
            equipmentGridLayout.constraintCount=columns;equipmentGridLayout.cellSize=new Vector2((width-16-(columns-1)*10)/columns,116);
        }

        public void BeginEquipmentDrag(BuildingKind kind,Vector2 position)
        {
            if(game.Paused||game.Showcasing||game.Truck.Manual)return;
            EndEquipmentDrag();dragEquipment=kind;equipmentDragGhost=Box("Equipment drag ghost",canvas.transform);
            equipmentDragGhost.sizeDelta=new Vector2(166,48);equipmentDragGhost.GetComponent<UnityEngine.UI.Image>().color=new Color(.04f,.35f,.43f,.95f);
            var group=equipmentDragGhost.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            var text=EquipmentText(equipmentDragGhost,game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==kind).Name,17);Stretch((RectTransform)text.transform,6);text.alignment=TextAlignmentOptions.Center;
            MoveEquipmentDrag(position);modal.SetAsLastSibling();
        }
        public void MoveEquipmentDrag(Vector2 position)
        {
            if(equipmentDragGhost!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,position,null,out var local))equipmentDragGhost.anchoredPosition=local;
        }
        public void DropEquipmentShortcut(int slot)
        {
            if(dragEquipment.HasValue&&!game.Paused)AssignBuildShortcut(slot,dragEquipment.Value);
        }
        public void EndEquipmentDrag(){dragEquipment=null;if(equipmentDragGhost!=null){Destroy(equipmentDragGhost.gameObject);equipmentDragGhost=null;}}

        private void PrepareEquipmentGuide(HardwareHint hint)
        {
            if(hint.Key!="build"&&hint.Key!="bottleneck"){lastEquipmentGuide=null;return;}
            if(lastEquipmentGuide==hint.Building)return;
            lastEquipmentGuide=hint.Building;
            if(buildShortcuts.IndexOf(hint.Building)>=0||game.Placing.HasValue||game.Paused||ProcurementOpen)return;
            equipmentCategory=EquipmentCategory(hint.Building);equipmentQuery="";OpenEquipmentLibrary();
            if(equipmentSearch!=null)equipmentSearch.SetTextWithoutNotify("");RefreshEquipmentLibrary(true);
        }

        private void DeviceActionBar()
        {
            var node=game.World.Node(game.SelectedNode);if(node==null)return;
            var actions=Row(Box("Device quick actions",content,false));actions.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=44;
            var upgrade=Button(actions,"升级",()=>game.Submit(new WorldCommand{Kind=CommandKind.Upgrade,Node=game.SelectedNode}),0,36);upgrade.name="升级：提速，并降低次品率";
            var move=Button(actions,"移动",game.BeginMove,0,36);move.name="移动 / 旋转";
            UnityEngine.UI.Button third;
            if(node.Kind==BuildingKind.Rework)
                third=Button(actions,"补充显存",()=>{material=HardwareCatalog.Memory;game.Drawer="采购";RebuildDrawer();},0,36);
            else if(node.Kind==BuildingKind.Tester||node.Input.Concat(node.Output).Any(g=>g.Material==HardwareCatalog.Defect))
                third=Button(actions,"返修次品",()=>
                {
                    var rework=game.World.State.Nodes.FirstOrDefault(n=>n.Factory==game.Factory&&n.Kind==BuildingKind.Rework&&!n.PendingRemove);
                    if(rework!=null)game.FocusNode(rework.Id);else game.BeginBuild(BuildingKind.Rework);
                },0,36);
            else
            {third=Button(actions,"再建同类",()=>{var selected=game.World.Node(game.SelectedNode);if(selected!=null)game.BeginBuild(selected.Kind,selected.RecipeId);},0,36);third.name="再建一台同类设备";}
            foreach(var button in new[]{upgrade,move,third})button.GetComponentInChildren<TMP_Text>().fontSize=14;
        }
    }
}
