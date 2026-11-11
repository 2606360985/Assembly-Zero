using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyZero.Unity.EP04
{
    /// <summary>Founder on site: a presentation layer over authoritative business state.</summary>
    public sealed class HardwareFounder : MonoBehaviour
    {
        private HardwareGame game;
        private Canvas canvas;
        private RectTransform root,sceneUi,overlay,choices;
        private UnityEngine.UI.RawImage backdrop,productPreview;
        private UnityEngine.UI.Image portrait,partner;
        private TMP_Text heading,speaker,body,card,nextLabel;
        private CanvasGroup sceneAlpha;
        private TMP_InputField nameInput;
        private readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        private readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        private readonly Color paper=new Color(.93f,.92f,.87f),copper=new Color(.76f,.48f,.29f),panel=new Color(.075f,.085f,.09f,.94f);
        private string mode="",aux="",sceneId="";
        private bool replay,auto,hidden,resolving,decisionsVisible;
        private readonly UnityEngine.UI.Button[] identityButtons=new UnityEngine.UI.Button[2];
        private int selectedFounder;
        private float elapsed,autoWait;
        private FounderScene Scene=>FounderStory.Find(sceneId);
        public bool PresentationEnabled {get;private set;}=true;
        public bool IsBlocking=>PresentationEnabled&&root!=null&&root.gameObject.activeSelf;
        public bool AllowsCommands=>!IsBlocking||resolving;
        public bool ScenePlaying=>mode=="scene"&&IsBlocking;
        public string CurrentScene=>sceneId;
        public int CurrentLine=>game.World.Narrative.Line;
        public bool Replaying=>replay;
        public bool DialogueVisible=>!hidden;
        public string DisplayedText=>body==null?"":body.text;
        private NarrativeProgress N=>game.World.Narrative;

        public void Initialize(HardwareGame owner)
        {
            game=owner;
            var obj=new GameObject("Founder on site",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            obj.transform.SetParent(transform,false);canvas=obj.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=300;
            var scaler=obj.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            root=Rect("Founder screen",obj.transform,0,0,1,1);Fill(root,new Color(.035f,.04f,.045f),true);
            backdrop=Rect("Event environment",root,0,0,1,1).gameObject.AddComponent<UnityEngine.UI.RawImage>();backdrop.raycastTarget=false;
            sceneUi=Rect("Dialogue interface",root,0,0,1,1);sceneAlpha=sceneUi.gameObject.AddComponent<CanvasGroup>();
            partner=Rect("Supporting character",sceneUi,.015f,.215f,.405f,.86f).gameObject.AddComponent<UnityEngine.UI.Image>();partner.preserveAspect=true;partner.raycastTarget=false;
            portrait=Rect("Current speaker",sceneUi,.40f,.18f,.99f,.91f).gameObject.AddComponent<UnityEngine.UI.Image>();portrait.preserveAspect=true;portrait.raycastTarget=false;
            Fill(Rect("Header shade",sceneUi,0,.90f,1,1),new Color(.035f,.04f,.045f,.84f));
            heading=Text("Chapter and location",sceneUi,.035f,.914f,.58f,.982f,27);
            Button(sceneUi,"剧情记录",.65f,.935f,.755f,.98f,OpenHistory);
            Button(sceneUi,"设置",.765f,.935f,.84f,.98f,OpenSettings);
            Button(sceneUi,"剧情档案",.85f,.935f,.965f,.98f,OpenArchive);
            var dossier=Rect("Temporary dossier",sceneUi,.04f,.58f,.345f,.855f);Fill(dossier,new Color(.07f,.08f,.085f,.91f));Border(dossier,copper);
            card=Text("Event facts",dossier,.055f,.08f,.945f,.92f,25);
            card.enableAutoSizing=true;card.fontSizeMin=20;card.fontSizeMax=25;
            productPreview=Rect("Actual SKU preview",sceneUi,.04f,.285f,.39f,.565f).gameObject.AddComponent<UnityEngine.UI.RawImage>();productPreview.raycastTarget=false;productPreview.gameObject.SetActive(false);
            choices=Rect("Decisions",sceneUi,.04f,.275f,.445f,.535f);
            var dialogue=Rect("Dialogue panel",sceneUi,0,0,1,.23f);Fill(dialogue,panel);Fill(Rect("Copper rule",dialogue,0,.994f,1,1),copper);
            speaker=Text("Name and role",dialogue,.04f,.73f,.85f,.96f,27);
            body=Text("Dialogue body",dialogue,.055f,.23f,.925f,.72f,31);body.enableAutoSizing=true;body.fontSizeMin=26;body.fontSizeMax=31;
            Button(dialogue,"回看",.035f,.035f,.105f,.19f,OpenHistory);
            Button(dialogue,"自动",.115f,.035f,.185f,.19f,()=>{auto=!auto;});
            Button(dialogue,"隐藏界面",.195f,.035f,.30f,.19f,ToggleHidden);
            Button(dialogue,"跳过对白",.31f,.035f,.415f,.19f,SkipDialogue);
            nextLabel=Button(dialogue,"继续  >",.86f,.035f,.97f,.19f,Next).GetComponentInChildren<TMP_Text>();
            overlay=Rect("Overlay",root,0,0,1,1);overlay.gameObject.SetActive(false);root.gameObject.SetActive(false);
        }
        private RectTransform Rect(string name,Transform parent,float x0,float y0,float x1,float y1)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;return r;
        }
        private void Fill(RectTransform r,Color color,bool block=false){var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.color=color;i.raycastTarget=block;}
        private TMP_Text Text(string name,Transform parent,float x0,float y0,float x1,float y1,int size)
        {
            var t=Rect(name,parent,x0,y0,x1,y1).gameObject.AddComponent<TextMeshProUGUI>();t.font=game.Font;t.fontSize=size;t.color=paper;t.raycastTarget=false;t.richText=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;return t;
        }
        private UnityEngine.UI.Button Button(Transform parent,string label,float x0,float y0,float x1,float y1,Action callback)
        {
            var r=Rect(label,parent,x0,y0,x1,y1);Fill(r,new Color(.10f,.115f,.12f,.90f),true);Border(r,new Color(.40f,.42f,.40f,.6f));
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=r.GetComponent<UnityEngine.UI.Image>();var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.77f,.50f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.75f,.57f,.4f);b.colors=colors;b.onClick.AddListener(()=>{HardwareAudio.Play("click",.4f);callback();});
            var t=Text("Label",r,.035f,.04f,.965f,.96f,24);t.text=label;t.alignment=TextAlignmentOptions.Midline;t.enableAutoSizing=true;t.fontSizeMin=18;t.fontSizeMax=24;
            return b;
        }
        private void Border(RectTransform r,Color color)
        {
            foreach(var side in new[]{new[]{0f,0f,1f,.012f},new[]{0f,.988f,1f,1f},new[]{0f,0f,.003f,1f},new[]{.997f,0f,1f,1f}})Fill(Rect("Fine border",r,side[0],side[1],side[2],side[3]),color);
        }
        private void Clear(Transform r){for(var i=r.childCount-1;i>=0;i--){var child=r.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}}
        private Texture2D Texture(string id)
        {
            if(!textures.TryGetValue(id,out var t)){t=Resources.Load<Texture2D>("Founder/Art/"+id);textures[id]=t;}return t;
        }
        private Sprite Portrait(string id,string expression)
        {
            if(id=="studio"||id=="gaming"||id=="research")id="client_"+id;
            var index=Array.IndexOf(new[]{"calm","thinking","questioning","tense","tired","relieved"},expression);index=Math.Max(0,index);var key=id+index;
            if(sprites.TryGetValue(key,out var s))return s;var t=Texture(id);if(t==null)return null;
            var w=t.width/3f;var h=t.height/2f;s=Sprite.Create(t,new UnityEngine.Rect((index%3)*w,(1-index/3)*h,w,h),new Vector2(.5f,0),100);sprites[key]=s;return s;
        }
        private void Background(string art)
        {
            backdrop.texture=Texture(art);backdrop.color=backdrop.texture==null?new Color(.12f,.15f,.16f):Color.white;
            if(backdrop.texture!=null){var screen=(float)Screen.width/Math.Max(1,Screen.height);var aspect=(float)backdrop.texture.width/backdrop.texture.height;backdrop.uvRect=aspect>screen?new UnityEngine.Rect((1-screen/aspect)*.5f,0,screen/aspect,1):new UnityEngine.Rect(0,(1-aspect/screen)*.5f,1,aspect/screen);}
        }
        public void SetPresentationEnabled(bool enabled)
        {
            PresentationEnabled=enabled;if(!enabled){root.gameObject.SetActive(false);mode="";aux="";}
        }
        public void ResetPresentation()
        {
            root.gameObject.SetActive(false);mode="";aux="";sceneId="";replay=false;hidden=false;
        }
        private bool SafeToOpen=>!game.Showcasing&&!game.Paused&&!game.Truck.Manual&&!game.Placing.HasValue&&!game.Hud.EquipmentDragging&&!game.Hud.EquipmentLibraryOpen&&!game.Hud.ProcurementOpen;
        private void Update()
        {
            if(game?.World==null||!PresentationEnabled)return;
            var other=game.Hud.GetComponentInChildren<UnityEngine.UI.CanvasScaler>();if(other!=null)canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution=other.referenceResolution;
            if(!IsBlocking)
            {
                if(SafeToOpen){if(!N.IdentityChosen)OpenIdentity();else if(!string.IsNullOrEmpty(N.Active))Play(N.Active,false);else if(!N.Disabled&&N.Pending.Count>0)Play(N.Pending[0],false);}
                return;
            }
            backdrop.rectTransform.localScale=Vector3.one*(1.012f+Mathf.Sin(Time.unscaledTime*.2f)*.003f);
            if(mode!="scene"||aux!="")return;
            var k=Keyboard.current;
            if(k!=null&&(nameInput==null||!nameInput.isFocused))
            {
                if(k.tabKey.wasPressedThisFrame){ToggleHidden();return;}
                if(hidden){if(k.spaceKey.wasPressedThisFrame||k.enterKey.wasPressedThisFrame)ToggleHidden();return;}
                if(k.escapeKey.wasPressedThisFrame){OpenSettings();return;}
                if(k.spaceKey.wasPressedThisFrame||k.enterKey.wasPressedThisFrame){Next();return;}
            }
            if(hidden)return;
            elapsed+=Time.unscaledDeltaTime;body.maxVisibleCharacters=Math.Min(body.text.Length,(int)(elapsed*32));
            portrait.color=new Color(1,1,1,Mathf.Clamp01(elapsed*5));
            if(auto&&body.maxVisibleCharacters>=body.text.Length){autoWait+=Time.unscaledDeltaTime;if(autoWait>1.6f&&!decisionsVisible){if(LineIndex<Scene.Lines.Length-1)Next();else ShowDecisions();}}
        }
        private void LateUpdate()
        {
            if(productPreview==null)return;
            var show=ScenePlaying&&sceneId=="product"&&aux==""&&!hidden&&!decisionsVisible;
            productPreview.gameObject.SetActive(show);
            if(show)
            {
                var sku=game.World.Sku(N.ProductId);
                if(sku!=null)productPreview.texture=game.View.SkuPreview(HardwareVisualKey.Of(HardwareCatalog.RawGPU,sku,WorldSimulation.GradeA));
            }
        }
        public void OpenIdentity()
        {
            root.gameObject.SetActive(true);mode="identity";sceneUi.gameObject.SetActive(false);overlay.gameObject.SetActive(true);Clear(overlay);Background("workshop");
            Fill(Rect("Identity shade",overlay,0,0,1,1),new Color(.025f,.035f,.04f,.60f));
            Text("Title",overlay,.05f,.87f,.95f,.96f,43).text="创始人现场 / 选择经营者";
            Text("Promise",overlay,.05f,.79f,.9f,.86f,24).text="同一份事业，不同的目光。经营能力与可选路线一致，另一位将作为联合创始人陪伴你。";
            for(var i=0;i<2;i++)
            {
                var id=i;var left=.08f+i*.48f;var image=Rect("Founder portrait "+i,overlay,left,.30f,left+.35f,.77f).gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=Portrait(i==0?"lin":"shen","calm");image.preserveAspect=true;image.raycastTarget=false;
                identityButtons[i]=Button(overlay,i==0?"林序 / 32 岁 / 产品工程师":"沈知微 / 30 岁 / 制造工程师",left,.235f,left+.35f,.30f,()=>SelectIdentity(id));
            }
            Text("Biography",overlay,.10f,.19f,.43f,.23f,20).text="把产品做好，学会让组织可靠运转。";
            Text("Biography",overlay,.58f,.19f,.91f,.23f,20).text="让产线可靠，学会为长期事业承担风险。";
            var inputRect=Rect("Founder name",overlay,.28f,.12f,.61f,.185f);Fill(inputRect,panel,true);var text=Text("Name",inputRect,.04f,.08f,.96f,.92f,28);nameInput=inputRect.gameObject.AddComponent<TMP_InputField>();nameInput.textViewport=inputRect;nameInput.textComponent=(TextMeshProUGUI)text;nameInput.fontAsset=game.Font;nameInput.characterLimit=12;nameInput.lineType=TMP_InputField.LineType.SingleLine;
            Button(overlay,"以此身份开始",.64f,.12f,.88f,.185f,()=>ConfirmIdentity());
            Text("Identity note",overlay,.10f,.035f,.90f,.09f,20).text="姓名可修改 · 本战役身份固定 · 旧员工证与腕表，将陪你走过每个阶段";SelectIdentity(0);
        }
        private void SelectIdentity(int value){selectedFounder=value;nameInput.text=FounderStory.DefaultName(value);for(var i=0;i<2;i++)identityButtons[i].GetComponent<UnityEngine.UI.Image>().color=i==value?new Color(.30f,.20f,.14f,.95f):new Color(.10f,.115f,.12f,.90f);}
        public void ConfirmIdentity(int? founder=null,string name=null)
        {
            game.World.ChooseFounder(founder??selectedFounder,name??nameInput?.text);nameInput=null;overlay.gameObject.SetActive(false);root.gameObject.SetActive(false);mode="";
        }
        public void RequestContract(int order)
        {
            if(IsBlocking)return;var o=game.World.State.Hardware.Orders.FirstOrDefault(x=>x.Id==order);if(o==null)return;
            if(!PresentationEnabled||!N.IdentityChosen||o.Template<=0||N.Completed.Contains(o.Template==1?"contract1":"contract2"))
            {game.Submit(new WorldCommand{Kind=CommandKind.AcceptOrder,Order=order,Factory=game.Factory});game.Hud.RebuildDrawer();return;}
            N.Order=order;N.ContextFactory=game.Factory;Play(o.Template==1?"contract1":"contract2",false);
        }
        public void Play(string id,bool asReplay)
        {
            var scene=FounderStory.Find(id);if(scene==null)return;
            sceneId=id;replay=asReplay;mode="scene";aux="";hidden=false;autoWait=0;
            if(asReplay)replayLine=0;else if(N.Active!=id){N.Active=id;N.Line=0;N.Pending.Remove(id);}
            root.gameObject.SetActive(true);sceneUi.gameObject.SetActive(true);overlay.gameObject.SetActive(false);sceneAlpha.alpha=1;sceneAlpha.blocksRaycasts=true;Background(scene.Art);RenderLine();
        }
        private int replayLine;
        private int LineIndex=>replay?replayLine:N.Line;
        private string ActorId(string actor)=>actor=="player"?(N.Founder==0?"lin":"shen"):actor=="partner"?(N.Founder==0?"shen":"lin"):actor;
        private string ActorName(string actor)=>actor=="player"?N.FounderName:actor=="partner"?FounderStory.PartnerName(N.Founder):actor=="studio"?"许岚":actor=="gaming"?"周正":actor=="research"?"顾清禾":actor;
        private string Role(string actor)=>actor=="player"?"创始人":actor=="partner"?"联合创始人":actor=="studio"?"晨湾 / 制作负责人":actor=="gaming"?"星河 / 经营者":"深脉 / 系统研究员";
        private string Resolve(string text)=>(text??"").Replace("{delivery}",string.IsNullOrEmpty(N.LastDelivery)?"交付结果已记录在正式结算中。":N.LastDelivery).Replace("{product}",N.LastProduct??"");
        private void RenderLine()
        {
            var index=Math.Clamp(LineIndex,0,Scene.Lines.Length-1);if(replay)replayLine=index;else N.Line=index;
            var line=Scene.Lines[index];heading.text="创始人现场\n"+Scene.Chapter+"  /  "+Scene.Location;
            var content=Resolve(line.Actor=="player"?FounderStory.Personalize(sceneId,index,N.Founder,line.Text):line.Text);
            speaker.text=ActorName(line.Actor)+"   /   "+Role(line.Actor);body.text=content;body.maxVisibleCharacters=0;elapsed=0;autoWait=0;
            portrait.sprite=Portrait(ActorId(line.Actor),line.Expression);portrait.enabled=portrait.sprite!=null;
            partner.sprite=Portrait(ActorId(line.Actor=="player"?"partner":"player"),"thinking");partner.color=new Color(.82f,.83f,.80f,1);partner.enabled=partner.sprite!=null&&sceneId=="intro";
            card.text=Facts();card.transform.parent.gameObject.SetActive(sceneId=="contract1"||sceneId=="contract2"||sceneId=="ai_invite"||sceneId=="first"||sceneId=="product");Clear(choices);decisionsVisible=false;nextLabel.text=index==Scene.Lines.Length-1?"作出决定  >":"继续  >";
            var record=ActorName(line.Actor)+"："+content;
            var logKey=sceneId+":"+index;
            if(!replay&&N.LogKey!=logKey){N.History.Add(record);N.LogKey=logKey;if(N.History.Count>160)N.History.RemoveAt(0);}
        }
        private string Facts()
        {
            if(sceneId=="contract1"||sceneId=="contract2")
            {
                var o=game.World.State.Hardware.Orders.FirstOrDefault(x=>x.Id==N.Order);
                if(o!=null)return "合同资料 / "+o.Name+"\n\n数量  "+o.Quantity+" 张\n总价  "+HardwareGame.Money(game.World.OrderValue(o))+"\n交期  "+o.Seconds+" 模拟秒\n\n"+(o.Template==1?"验收：检测工位到位，连续 3 张合格":"核对物料，按正式订单条件交付");
            }
            return Resolve(Scene.Card)+"\n\n"+game.World.NarrativeGoal();
        }
        public void Next()
        {
            if(!ScenePlaying||aux!=""||hidden)return;
            if(body.maxVisibleCharacters<body.text.Length){body.maxVisibleCharacters=body.text.Length;elapsed=1000;return;}
            if(LineIndex<Scene.Lines.Length-1){if(replay)replayLine++;else N.Line++;RenderLine();return;}ShowDecisions();
        }
        public void SkipDialogue()
        {
            if(!ScenePlaying||aux!="")return;
            if(replay)replayLine=Scene.Lines.Length-1;else N.Line=Scene.Lines.Length-1;RenderLine();body.maxVisibleCharacters=body.text.Length;elapsed=1000;ShowDecisions();
        }
        private void ShowDecisions()
        {
            decisionsVisible=true;
            Clear(choices);nextLabel.text="请选择";
            if(replay){Decision("结束回放",0,()=>{Close();OpenArchive();});return;}
            if(sceneId=="contract1")
            {
                Decision("先稳定良率 / 1 个检测工位 · 签署",0,()=>Sign(1));
                Decision("并行检测 / 2 工位 · 加投 "+HardwareGame.Money(game.World.Settings.Cost(BuildingKind.Tester))+" 并签署",1,()=>Sign(2));
                Decision("暂缓，先核对产线",2,()=>DeferContract());
            }
            else if(sceneId=="contract2"){Decision("确认条件并签署",0,()=>Sign(0));Decision("暂缓，先检查物料",1,DeferContract);}
            else if(sceneId=="ai_invite")
            {
                Decision("承接渲染验证 / 返回工厂准备",0,()=>{game.World.StartNarrativeValidation(game.Factory);Finish();game.SetDrawer("算力");});
                Decision("稍后决定，先继续经营",1,()=>{N.AiDeferred=true;N.Active="";N.Line=0;Close();});
            }
            else Decision(sceneId=="horizon"?"继续经营 / 准备下一段事业":"回到工厂 / 继续目标",0,Finish);
        }
        private void Decision(string text,int row,Action action){Button(choices,text,0,.70f-row*.32f,1,.96f-row*.32f,action);}
        private void DeferContract(){N.Active="";N.Line=0;Close();}
        public void Sign(int qualityMode)
        {
            if(replay||!(sceneId=="contract1"||sceneId=="contract2"))return;
            resolving=true;CommandResult result;
            try{result=game.Submit(new WorldCommand{Kind=CommandKind.AcceptOrder,Order=N.Order,Factory=N.ContextFactory});}finally{resolving=false;}
            if(!result.Success){card.text="签署未完成\n\n"+result.Message;Clear(choices);Decision("返回订单板",0,DeferContract);return;}
            if(sceneId=="contract1"){N.QualityMode=qualityMode;N.QualityStreak=0;N.Factory=N.ContextFactory;}
            Finish();game.Hud.RebuildDrawer();
        }
        private void Finish(){game.World.CompleteNarrative(sceneId);game.Message=game.World.NarrativeGoal();Close();game.Hud.Refresh();}
        private void Close(){if(sceneId=="product")game.View.HideSkuPreview();mode="";aux="";replay=false;hidden=false;root.gameObject.SetActive(false);overlay.gameObject.SetActive(false);}
        public void ToggleHidden()
        {
            if(mode!="scene")return;hidden=!hidden;sceneAlpha.alpha=hidden?0:1;sceneAlpha.blocksRaycasts=!hidden;
            if(hidden){overlay.gameObject.SetActive(true);Clear(overlay);Button(overlay,"显示界面 [Tab]",.80f,.94f,.97f,.985f,ToggleHidden);}else overlay.gameObject.SetActive(false);
        }
        private RectTransform OpenPanel(string type,string title)
        {
            root.gameObject.SetActive(true);if(mode==""){mode="archive";sceneUi.gameObject.SetActive(false);Background("workshop");}
            aux=type;overlay.gameObject.SetActive(true);Clear(overlay);Fill(Rect("Panel shade",overlay,0,0,1,1),new Color(.025f,.035f,.04f,.94f),true);
            Text("Title",overlay,.06f,.88f,.85f,.97f,36).text=title;
            Button(overlay,"返回",.86f,.90f,.96f,.96f,()=>{aux="";overlay.gameObject.SetActive(false);if(mode=="archive")Close();else if(Scene!=null)Background(Scene.Art);});return overlay;
        }
        public void OpenArchive()
        {
            if(!PresentationEnabled)return;if(!N.IdentityChosen){OpenIdentity();return;}
            var p=OpenPanel("archive","创始人现场 / 剧情档案");Text("Objective",p,.06f,.80f,.94f,.87f,26).text=game.World.NarrativeGoal();
            var scenes=FounderStory.Scenes.Where(s=>s.Id!="recap").ToArray();
            for(var i=0;i<scenes.Length;i++)
            {
                var s=scenes[i];var done=N.Completed.Contains(s.Id);var y=.72f-i*.063f;
                var b=Button(p,(done?"已读 / ":"未完成 / ")+s.Chapter+" · "+s.Title,.06f,y,.69f,y+.05f,()=>Play(s.Id,true));b.interactable=done;
            }
            Button(p,"查看当前任务",.73f,.68f,.94f,.75f,()=>{Close();game.SetDrawer(N.ValidationAccepted?"算力":"订单");});
            if(game.World.State.Hardware.ScriptedDone>=2&&!N.ValidationAccepted)Button(p,"会见研究团队",.73f,.58f,.94f,.65f,()=>Play("ai_invite",false));
            Button(p,N.Disabled?"启用剧情触发":"暂停新剧情触发",.73f,.47f,.94f,.54f,()=>{N.Disabled=!N.Disabled;OpenArchive();});
            Button(p,"能源篇 / 场景预览",.73f,.32f,.94f,.39f,()=>Preview("energy_control","地球能源体系 / 后续篇章预览"));
            Button(p,"轨道篇 / 场景预览",.73f,.22f,.94f,.29f,()=>Preview("orbital","星际工业 / 后续篇章预览"));
            Button(p,"深空篇 / 场景预览",.73f,.12f,.94f,.19f,()=>Preview("deep_space","文明协作 / 后续篇章预览"));
        }
        private void Preview(string art,string title){OpenPanel("preview",title);Background(art);overlay.GetChild(0).GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.12f);Text("Preview note",overlay,.06f,.06f,.9f,.15f,25).text="概念预览 · 首版经营内容止于创业与算力验证，后续功能尚未开放。";}
        public void OpenHistory()
        {
            ShowHistory(0);
        }
        private void ShowHistory(int page)
        {
            page=Math.Clamp(page,0,Math.Max(0,(N.History.Count-1)/8));var end=Math.Max(0,N.History.Count-page*8);var start=Math.Max(0,end-8);
            var p=OpenPanel("history","剧情记录 / 第 "+(page+1)+" 页");var text=Text("Recent dialogue",p,.07f,.15f,.93f,.84f,26);text.text=N.History.Count==0?"尚无对话记录":string.Join("\n\n",N.History.Skip(start).Take(end-start));text.enableAutoSizing=true;text.fontSizeMin=20;text.fontSizeMax=26;
            var previous=Button(p,"更早的对话",.07f,.04f,.30f,.11f,()=>ShowHistory(page+1));previous.interactable=start>0;
            var recent=Button(p,"更新的对话",.33f,.04f,.56f,.11f,()=>ShowHistory(page-1));recent.interactable=page>0;
        }
        public void OpenSettings()
        {
            var p=OpenPanel("settings","创始人现场 / 设置");
            Button(p,auto?"自动播放：开":"自动播放：关",.15f,.65f,.65f,.74f,()=>{auto=!auto;OpenSettings();});
            Button(p,"保存当前战役与对白位置",.15f,.50f,.65f,.59f,()=>{game.Save();});
            Text("Controls",p,.15f,.25f,.85f,.45f,26).text="空格 / 回车：显示完整对白，再次按下继续\nTab：隐藏或显示界面    Esc：设置\n关键对话暂停经营时间；自动播放不会替你作决定。";
        }
        private void OnDestroy(){foreach(var s in sprites.Values)if(s!=null)Destroy(s);}
    }
}
