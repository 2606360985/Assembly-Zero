using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareHud
    {
        private RectTransform procurementBlocker,procurementPanel,procurementList;
        private UnityEngine.UI.ScrollRect procurementScroll;
        private TMP_Text procurementSummary,procurementNote,procurementQuote,procurementFeedback,procurementEmpty,procurementFilter;
        private TMP_InputField procurementQuantity;
        private UnityEngine.UI.Button procurementPay,procurementFill;
        private ProcurementOverview procurementSnapshot;
        private bool procurementOnlyMissing;
        private int procurementFactory;
        private readonly Dictionary<int,ProcurementRow> procurementRows=new Dictionary<int,ProcurementRow>();
        private static readonly float[] ProcurementColumns={0,.25f,.35f,.46f,.57f,.69f,.86f,1};

        private sealed class ProcurementRow
        {
            public RectTransform Root;
            public TMP_Text[] Cells;
            public UnityEngine.UI.Image Stripe,Fill,Background;
        }

        public bool ProcurementOpen=>procurementBlocker!=null&&procurementBlocker.gameObject.activeSelf;

        public void OpenProcurementOverview()
        {
            if(game.Drawer!="采购"||game.Showcasing||game.Truck.Manual)return;
            CloseEquipmentLibrary();
            var firstOpen=procurementBlocker==null;
            if(firstOpen)BuildProcurementOverview();
            procurementFactory=game.Factory;
            game.HoverNode=0;
            procurementBlocker.gameObject.SetActive(true);procurementBlocker.SetAsLastSibling();modal.SetAsLastSibling();
            game.Interaction=HardwareInteraction.Modal;
            RefreshProcurementOverview();
            Canvas.ForceUpdateCanvases();
            if(firstOpen)procurementScroll.verticalNormalizedPosition=1;
        }

        public void CloseProcurementOverview()
        {
            if(!ProcurementOpen)return;
            if(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(procurementBlocker))
                EventSystem.current.SetSelectedGameObject(null);
            procurementBlocker.gameObject.SetActive(false);
            procurementSnapshot=null;
            game.Interaction=game.Paused?HardwareInteraction.Modal:HardwareInteraction.Browsing;
        }

        private TMP_Text ProcurementText(Transform parent,string text,int size=15)
        {
            var rect=Box("Text",parent,false);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font=game.Font;label.fontSize=size;label.color=ink;label.text=text;label.raycastTarget=false;
            label.overflowMode=TextOverflowModes.Ellipsis;label.textWrappingMode=TextWrappingModes.Normal;
            return label;
        }

        private TMP_Text ProcurementCell(Transform parent,string text,int column,int size=15)
        {
            var label=ProcurementText(parent,text,size);label.alignment=column==0?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center;
            Anchor((RectTransform)label.transform,new Vector2(ProcurementColumns[column],0),new Vector2(ProcurementColumns[column+1],1),new Vector2(10,14),new Vector2(-4,-4));
            return label;
        }

        private static string ProcurementCaption(int material)
        {
            var style=HardwareVisualCatalog.Get(material);var tint=style.Color;
            if(tint.grayscale<.35f)tint=Color.Lerp(tint,new Color(.83f,.9f,.94f),.75f);
            return "<color=#"+ColorUtility.ToHtmlStringRGB(tint)+">[ "+style.Mark+" ]</color> · "+style.Stage;
        }

        private void BuildProcurementOverview()
        {
            procurementBlocker=Box("Procurement blocker",canvas.transform);Stretch(procurementBlocker);
            procurementBlocker.GetComponent<UnityEngine.UI.Image>().color=new Color(.015f,.025f,.04f,.82f);
            var dismiss=procurementBlocker.gameObject.AddComponent<UnityEngine.UI.Button>();dismiss.transition=UnityEngine.UI.Selectable.Transition.None;
            dismiss.onClick.AddListener(CloseProcurementOverview);
            procurementPanel=Box("Procurement overview",procurementBlocker);
            procurementPanel.anchorMin=procurementPanel.anchorMax=new Vector2(.5f,.5f);
            procurementPanel.GetComponent<UnityEngine.UI.Image>().color=panel;
            // Consume clicks in blank panel space instead of bubbling them to the dismissing backdrop.
            procurementPanel.gameObject.AddComponent<UnityEngine.UI.Button>().transition=UnityEngine.UI.Selectable.Transition.None;

            var heading=ProcurementText(procurementPanel,"<color=#16B8D1>采购总览</color>  <size=14>青港元件 / SUPPLY DESK</size>",27);
            Anchor((RectTransform)heading.transform,new Vector2(0,1),Vector2.one,new Vector2(24,-66),new Vector2(-126,-14));
            var close=Button(procurementPanel,"关闭",CloseProcurementOverview,88,40);
            Anchor((RectTransform)close.transform,Vector2.one,Vector2.one,new Vector2(-110,-58),new Vector2(-22,-18));
            procurementSummary=ProcurementText(procurementPanel,"",20);
            Anchor((RectTransform)procurementSummary.transform,new Vector2(0,1),Vector2.one,new Vector2(24,-116),new Vector2(-24,-68));

            var table=Box("Materials table",procurementPanel,false);
            Anchor(table,Vector2.zero,Vector2.one,new Vector2(20,124),new Vector2(-344,-124));
            var tools=Row(Box("Filter",table,false));
            Anchor(tools,new Vector2(0,1),Vector2.one,new Vector2(0,-44),Vector2.zero);
            var explanation=Label(tools,"按交期分配 · 缺料优先",14,34,240);explanation.color=new Color(.6f,.72f,.79f);
            procurementFilter=Button(tools,"仅缺料：关",()=>{procurementOnlyMissing=!procurementOnlyMissing;RefreshProcurementOverview();},130,34).GetComponentInChildren<TMP_Text>();
            var headers=Box("Column headers",table);headers.GetComponent<UnityEngine.UI.Image>().color=new Color(.09f,.16f,.22f);
            Anchor(headers,new Vector2(0,1),Vector2.one,new Vector2(0,-90),new Vector2(-14,-48));
            var names=new[]{"采购件 / 状态","本厂现货","在途/待提","剩余需求","待采购缺口","当前单价","供应商库存"};
            for(var i=0;i<names.Length;i++){var label=ProcurementCell(headers,names[i],i,13);((RectTransform)label.transform).offsetMin=new Vector2(10,0);label.color=new Color(.6f,.74f,.81f);}
            var scrollRoot=Box("Materials scroll",table,false);
            Anchor(scrollRoot,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,-94));
            procurementScroll=scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();procurementScroll.horizontal=false;
            procurementScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;procurementScroll.scrollSensitivity=38;
            var viewport=Box("Viewport",scrollRoot);Stretch(viewport);viewport.offsetMax=new Vector2(-14,0);viewport.GetComponent<UnityEngine.UI.Image>().color=new Color(.045f,.075f,.11f);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            procurementList=Box("Material rows",viewport,false);procurementList.anchorMin=new Vector2(0,1);procurementList.anchorMax=Vector2.one;
            procurementList.pivot=new Vector2(.5f,1);procurementList.sizeDelta=Vector2.zero;procurementList.anchoredPosition=Vector2.zero;
            var layout=procurementList.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=6;
            layout.padding=new RectOffset(0,0,4,4);layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            procurementList.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            procurementScroll.viewport=viewport;procurementScroll.content=procurementList;
            var scrollbarRoot=Box("Materials scrollbar",scrollRoot);Anchor(scrollbarRoot,new Vector2(1,0),Vector2.one,new Vector2(-8,0),Vector2.zero);
            scrollbarRoot.GetComponent<UnityEngine.UI.Image>().color=new Color(.12f,.19f,.24f);
            var handle=Box("Handle",scrollbarRoot);Stretch(handle);handle.GetComponent<UnityEngine.UI.Image>().color=new Color(.21f,.51f,.59f);
            var scrollbar=scrollbarRoot.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            procurementScroll.verticalScrollbar=scrollbar;procurementScroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            procurementEmpty=ProcurementText(viewport,"没有待采购缺口\n可切换“全部”查看库存。",19);Stretch((RectTransform)procurementEmpty.transform,20);procurementEmpty.alignment=TextAlignmentOptions.Center;

            var checkout=Box("Purchase checkout",procurementPanel);
            Anchor(checkout,new Vector2(1,0),Vector2.one,new Vector2(-328,124),new Vector2(-20,-124));
            checkout.GetComponent<UnityEngine.UI.Image>().color=new Color(.075f,.13f,.18f);
            var checkoutScroll=checkout.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();checkoutScroll.horizontal=false;checkoutScroll.scrollSensitivity=30;checkoutScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var checkoutViewport=Box("Viewport",checkout);Stretch(checkoutViewport,6);checkoutViewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var controls=Box("Checkout controls",checkoutViewport,false);controls.anchorMin=new Vector2(0,1);controls.anchorMax=Vector2.one;controls.pivot=new Vector2(.5f,1);controls.sizeDelta=Vector2.zero;controls.anchoredPosition=Vector2.zero;
            Column(controls);controls.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            checkoutScroll.viewport=checkoutViewport;checkoutScroll.content=controls;
            Label(controls,"选中采购件",18,30);
            procurementQuote=Label(controls,"",17,188);
            var quantityRow=Row(Box("Purchase quantity",controls,false));
            Button(quantityRow,"−1",()=>SetProcurementQuantity(quantity-1),54,40);
            var inputRoot=Box("Quantity input",quantityRow);var inputSize=inputRoot.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();inputSize.flexibleWidth=1;inputSize.minWidth=60;
            inputRoot.GetComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.1f);
            procurementQuantity=inputRoot.gameObject.AddComponent<TMP_InputField>();
            var inputText=ProcurementText(inputRoot,quantity.ToString(),22);Stretch((RectTransform)inputText.transform,6);inputText.alignment=TextAlignmentOptions.Center;
            procurementQuantity.textViewport=(RectTransform)inputText.transform;procurementQuantity.textComponent=(TextMeshProUGUI)inputText;
            procurementQuantity.contentType=TMP_InputField.ContentType.IntegerNumber;procurementQuantity.characterLimit=3;
            procurementQuantity.onEndEdit.AddListener(value=>SetProcurementQuantity(int.TryParse(value,out var parsed)?parsed:quantity));
            Button(quantityRow,"+1",()=>SetProcurementQuantity(quantity+1),54,40);
            procurementFill=Button(controls,"填入缺口（最多 80 件）",()=>
            {
                var row=procurementSnapshot?.Materials.FirstOrDefault(m=>m.Material==material);
                if(row!=null)SetProcurementQuantity(row.Shortage);
            },0,38);
            var deliveryButton=Button(controls,"",()=>{delivery=(delivery+1)%3;RefreshProcurementOverview();},0,46);
            deliveryButton.name="Procurement delivery";
            procurementDelivery=deliveryButton.GetComponentInChildren<TMP_Text>();
            procurementPay=Button(controls,"支付并采购",()=>
            {
                var result=PurchaseSelectedMaterial();procurementFeedback.text=result.Message;procurementFeedback.color=result.Success?Good:Bad;RefreshProcurementOverview();
            },0,48);
            procurementPay.name="Procurement purchase";
            procurementFeedback=Label(controls,"选择数量与配送方式后支付。",14,76);
            procurementNote=ProcurementText(procurementPanel,"",13);
            Anchor((RectTransform)procurementNote.transform,Vector2.zero,new Vector2(1,0),new Vector2(24,18),new Vector2(-24,114));
            procurementBlocker.gameObject.SetActive(false);
        }

        private TMP_Text procurementDelivery;

        private void SetProcurementQuantity(int value)
        {
            quantity=Math.Clamp(value,1,80);procurementQuantity.SetTextWithoutNotify(quantity.ToString());RefreshProcurementOverview();
        }

        // Both purchase surfaces submit the same authoritative command and use the same pickup behavior.
        private CommandResult PurchaseSelectedMaterial()
        {
            var result=game.Submit(new WorldCommand{Kind=CommandKind.Purchase,Factory=game.Factory,Material=material,Quantity=quantity,Delivery=delivery});
            if(result.Success&&delivery==2&&game.Truck.Phase==VehiclePhase.Idle&&game.Truck.Cargo.Count==0)
                game.Submit(new WorldCommand{Kind=CommandKind.PickupPurchase,TargetFactory=game.Factory});
            return result;
        }

        private ProcurementRow CreateProcurementRow(ProcurementMaterial item)
        {
            var root=Box("Procurement material "+item.Material,procurementList);var e=root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.preferredHeight=e.minHeight=86;
            var button=root.gameObject.AddComponent<UnityEngine.UI.Button>();var colors=button.colors;colors.highlightedColor=new Color(.8f,1,1);colors.pressedColor=new Color(.6f,.9f,1);button.colors=colors;
            button.onClick.AddListener(()=>{material=item.Material;procurementFeedback.text="";RefreshProcurementOverview();});
            var stripe=Box("Status stripe",root);Anchor(stripe,new Vector2(0,0),new Vector2(0,1),Vector2.zero,new Vector2(3,0));stripe.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var cells=new TMP_Text[7];for(var i=0;i<cells.Length;i++)cells[i]=ProcurementCell(root,"",i,i==0?16:18);
            var track=Box("Coverage track",root);Anchor(track,new Vector2(.26f,0),new Vector2(1,0),new Vector2(0,8),new Vector2(-12,12));
            track.GetComponent<UnityEngine.UI.Image>().color=new Color(.16f,.22f,.28f);track.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var fill=Box("Coverage",track);Stretch(fill);fill.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            return new ProcurementRow{Root=root,Cells=cells,Stripe=stripe.GetComponent<UnityEngine.UI.Image>(),Fill=fill.GetComponent<UnityEngine.UI.Image>(),Background=root.GetComponent<UnityEngine.UI.Image>()};
        }

        private void RefreshProcurementOverview()
        {
            if(!ProcurementOpen)return;
            if(game.Drawer!="采购"||game.Showcasing||game.Factory!=procurementFactory){CloseProcurementOverview();return;}
            var bounds=((RectTransform)canvas.transform).rect;
            procurementPanel.sizeDelta=new Vector2(Mathf.Min(1440,bounds.width-48),Mathf.Min(820,bounds.height-48));
            procurementSnapshot=HardwareProcurement.Calculate(game.World,game.Factory);
            var snapshot=procurementSnapshot;
            if(!snapshot.Materials.Any(m=>m.Material==material))material=snapshot.Materials.FirstOrDefault()?.Material??0;
            procurementSummary.text="<b>"+snapshot.Factory+" 号厂</b>    进行中 <b>"+snapshot.ActiveOrders+"</b> 单    缺料 <color=#EB5545><b>"+snapshot.ShortageKinds+"</b></color> 类    <size=16>补料货款</size> <color=#F2A33A><b>"+HardwareGame.Money(snapshot.EstimatedGoodsCost)+"</b></color> <size=13>不含运费</size>";
            procurementFilter.text=procurementOnlyMissing?"仅缺料：开":"仅缺料：关";
            var scrollPosition=procurementScroll.verticalNormalizedPosition;
            var index=0;
            foreach(var item in snapshot.Materials.OrderBy(m=>m.Shortage>0?0:1))
            {
                if(!procurementRows.TryGetValue(item.Material,out var row)){row=CreateProcurementRow(item);procurementRows.Add(item.Material,row);}
                var shown=!procurementOnlyMissing||item.Shortage>0;row.Root.gameObject.SetActive(shown);if(shown)row.Root.SetSiblingIndex(index++);
                var color=item.Shortage>0?Bad:item.ProductionDemand>0||item.InTransitUsed>0?Gold:Good;
                var status=item.Shortage>0?"待补料 "+item.Shortage+" 件":item.ProductionDemand>0?"含自制 "+item.ProductionDemand+" 件":item.InTransitUsed>0?"已购 · 等待到货":item.Demand==0?"暂无订单需求":"已满足";
                row.Cells[0].text="<size=11>"+ProcurementCaption(item.Material)+"</size>\n<b>"+item.Name+"</b>\n<size=12><color=#"+ColorUtility.ToHtmlStringRGB(color)+">"+status+"</color></size>";
                row.Cells[1].text=item.Stock.ToString();row.Cells[2].text=item.InTransit.ToString();row.Cells[3].text=item.Demand.ToString();
                row.Cells[4].text="<b>"+item.Shortage+"</b>";row.Cells[4].color=color;
                row.Cells[5].text=HardwareGame.Money(item.UnitPrice);row.Cells[5].fontSize=15;
                row.Cells[6].text=item.SupplierStock.ToString();row.Cells[6].color=item.SupplierStock<item.Shortage?Gold:ink;
                row.Stripe.color=color;row.Fill.color=color;
                // A promised self-made cooler is still outstanding until upstream materials are processed.
                var outstanding=item.Material==HardwareCatalog.Heat||item.Material==HardwareCatalog.QuietHeat?item.ProductionDemand:0;
                var coverage=item.Demand==0?1:Mathf.Clamp01((item.Demand-item.Shortage-outstanding)/(float)item.Demand);
                row.Fill.rectTransform.anchorMax=new Vector2(coverage,1);row.Fill.rectTransform.offsetMax=Vector2.zero;
                row.Background.color=material==item.Material?new Color(.1f,.27f,.33f):new Color(.07f,.115f,.16f);
            }
            var ids=new HashSet<int>(snapshot.Materials.Select(m=>m.Material));
            foreach(var stale in procurementRows.Keys.Where(id=>!ids.Contains(id)).ToArray()){Destroy(procurementRows[stale].Root.gameObject);procurementRows.Remove(stale);}
            procurementEmpty.gameObject.SetActive(index==0);
            Canvas.ForceUpdateCanvases();procurementScroll.verticalNormalizedPosition=scrollPosition;
            procurementNote.text="共 "+snapshot.Materials.Count+" 类采购件 · 滚动查看    <color=#EB5545>● 待采购</color>    <color=#F2A33A>● 等到货 / 待自制</color>    <color=#38C785>● 已满足</color>\n"
                +"现货含仓库、设备缓存和厂内输送线；在途含配送、待自提及运往本厂的车载物资。剩余需求已扣除下游成品和半成品。\n"
                +"按配方理论用量估算；生产批次按预计产出抵扣，不预测后续采矿和额外良率损耗。"
                +(snapshot.NonPurchasableShortages.Count==0?"":"\n<color=#F2A33A>无法直接采购："+string.Join(" · ",snapshot.NonPurchasableShortages.Select(s=>HardwareCatalog.Name(s.Key)+" 仍缺 "+s.Value))+"</color>");
            var selected=snapshot.Materials.FirstOrDefault(m=>m.Material==material);
            procurementPay.interactable=selected!=null&&!game.Paused;
            if(selected==null){procurementQuote.text="暂无可采购物料";return;}
            if(!procurementQuantity.isFocused)procurementQuantity.SetTextWithoutNotify(quantity.ToString());
            var config=game.Config.Economy.Hardware;
            var fee=delivery==0?config.DeliveryFee:delivery==1?config.ExpressFee:config.PickupFee;
            var total=(long)selected.UnitPrice*quantity+fee;
            procurementQuote.text=ProcurementCaption(material)+"\n<size=23><b>"+selected.Name+"</b></size>\n"
                +"采购缺口 <color=#EB5545>"+selected.Shortage+"</color> · 供应商余量 "+selected.SupplierStock+"\n"
                +"单价 "+HardwareGame.Money(selected.UnitPrice)+"\n货款 "+HardwareGame.Money((long)selected.UnitPrice*quantity)+"\n运费 "+HardwareGame.Money(fee)
                +"\n<size=22>合计 <color=#F2A33A><b>"+HardwareGame.Money(total)+"</b></color></size>";
            procurementDelivery.text=new[]{"常规配送 · "+config.DeliverySeconds+" 秒","加急配送 · "+config.ExpressSeconds+" 秒","自提 · 空闲皮卡自动装运"}[delivery];
            procurementFill.interactable=selected.Shortage>0;
            var reason=selected.SupplierStock<quantity?"供应商库存不足，等待补货":game.World.State.Money<total?"采购资金不足":game.Paused?"已暂停，恢复后可采购":"";
            procurementPay.interactable=reason=="";
            procurementPay.GetComponentInChildren<TMP_Text>().text=reason==""?"支付并采购":"暂不可采购";
            if(reason!=""){procurementFeedback.text=reason;procurementFeedback.color=Bad;}
            else if(procurementFeedback.text=="供应商库存不足，等待补货"||procurementFeedback.text=="采购资金不足"||procurementFeedback.text=="已暂停，恢复后可采购")
            {procurementFeedback.text="选择数量与配送方式后支付。";procurementFeedback.color=ink;}
        }
    }
}
