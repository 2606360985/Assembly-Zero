using System;
using System.Collections;
using System.IO;
using System.Linq;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Capsule/key-art showcase: a separate, funded simulation fills the factory with running lines
    // under night lighting. The player's world is parked untouched and saving is blocked meanwhile.
    public sealed partial class HardwareGame
    {
        public bool Showcasing {get;private set;}
        public HardwareLens Lens {get;private set;}
        public static readonly (string Name,int Width,int Height)[] CapsuleSizes=
        {
            ("MainCapsule",1232,706),("HeaderCapsule",920,430),("SmallCapsule",462,174),("VerticalCapsule",748,896),("LibraryHero",3840,1240),("KeyArt",1920,1080),
        };
        private static readonly (float Yaw,float Zoom,Vector2 Offset)[] ShowcaseShots={(0,12,new Vector2(0,0)),(-45,9.5f,new Vector2(-3,-2)),(45,15,new Vector2(2,1))};
        private WorldSimulation parkedWorld;private int parkedFactory,parkedSpeed,showcaseShot;private Vector3 parkedFocus;private float parkedZoom,parkedYaw;private bool parkedPaused;
        private Color dayBackground,dayAmbient;private float daySun;private Color daySunColor;
        public string LastCapsuleFolder {get;private set;}
        private void InitializeLens(){Lens=gameObject.AddComponent<HardwareLens>();Lens.Initialize(ViewCamera);}
        // Builds a fully running factory using normal commands. Showcase-only funds; never saved.
        public static WorldSimulation BuildShowcaseWorld(EconomySettings settings,int preRollSeconds=180)
        {
            var w=new WorldSimulation(settings,null,20261028);w.State.Money=50_000_000;
            void Do(WorldCommand c){w.Enqueue(c);w.FlushCommands();}
            foreach(var s in settings.Hardware.Slots)Do(new WorldCommand{Kind=CommandKind.Build,Building=s.Kind,X=s.X,Z=s.Z,Rotation=s.Rotation});
            foreach(var extra in new[]{(BuildingKind.BoardAssembly,12,24),(BuildingKind.Rework,20,24),(BuildingKind.Manufacturer,36,26),(BuildingKind.Processor,4,26)})
                Do(new WorldCommand{Kind=CommandKind.Build,Building=extra.Item1,X=extra.Item2,Z=extra.Item3});
            foreach(var m in new[]{HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board,HardwareCatalog.Shell,HardwareCatalog.Carton,HardwareCatalog.Heat,HardwareCatalog.Aluminum})
                Do(new WorldCommand{Kind=CommandKind.Purchase,Material=m,Quantity=40,Delivery=1});
            Do(new WorldCommand{Kind=CommandKind.Collect,Resource=1});
            for(var i=0;i<preRollSeconds*60;i++)w.Tick();
            return w;
        }
        public void EnterShowcase()
        {
            if(Showcasing||World==null)return;
            parkedWorld=World;parkedFactory=Factory;parkedSpeed=Speed;parkedFocus=cameraFocus;parkedZoom=ViewCamera.orthographicSize;parkedYaw=orbitTarget;parkedPaused=Paused;
            World=BuildShowcaseWorld(Config.Economy);Showcasing=true;Factory=1;Speed=1;Paused=false;ResetPlacement();SelectedNode=0;HoverNode=0;Drawer="";
            SyncNotifications();Hud.ShowPause(false);Hud.SetShowcase(true);View.Rebuild();SetNight(true);ShowcaseShot(0);Message="展示模式：夜景厂房，导出胶囊图不会影响存档";
        }
        public void ExitShowcase()
        {
            if(!Showcasing)return;
            World=parkedWorld;parkedWorld=null;Showcasing=false;Factory=parkedFactory;Speed=parkedSpeed;Paused=parkedPaused;cameraFocus=parkedFocus;ViewCamera.orthographicSize=parkedZoom;orbitFrom=cameraYaw=orbitTarget=parkedYaw;orbitTime=OrbitDuration;
            SetNight(false);SyncNotifications();View.Rebuild();Hud.SetShowcase(false);Hud.ShowPause(Paused);Hud.RebuildDrawer();Message="已退出展示模式";
        }
        public void ShowcaseShot(int index)
        {
            showcaseShot=(index%ShowcaseShots.Length+ShowcaseShots.Length)%ShowcaseShots.Length;var shot=ShowcaseShots[showcaseShot];
            FocusFactory();cameraFocus+=new Vector3(shot.Offset.x*1.2f,0,shot.Offset.y*1.2f);ViewCamera.orthographicSize=shot.Zoom;orbitFrom=cameraYaw;orbitTarget=shot.Yaw;orbitTime=0;
        }
        public void NextShowcaseShot()=>ShowcaseShot(showcaseShot+1);
        private void SetNight(bool night)
        {
            var sun=FindObjectsByType<Light>().FirstOrDefault(l=>l.type==LightType.Directional);
            if(night)
            {
                dayBackground=ViewCamera.backgroundColor;dayAmbient=RenderSettings.ambientLight;if(sun!=null){daySun=sun.intensity;daySunColor=sun.color;}
                ViewCamera.backgroundColor=new Color(.02f,.035f,.07f);RenderSettings.ambientLight=new Color(.1f,.14f,.24f);if(sun!=null){sun.intensity=.35f;sun.color=new Color(.55f,.66f,1);}
            }
            else
            {
                ViewCamera.backgroundColor=dayBackground;RenderSettings.ambientLight=dayAmbient;if(sun!=null){sun.intensity=daySun;sun.color=daySunColor;}
            }
            View.SetNight(night);Lens?.SetNight(night);
        }
        public IEnumerator ExportCapsules(string folder=null)
        {
            folder??=Path.Combine(Application.isEditor?Directory.GetParent(Application.dataPath).FullName:Application.persistentDataPath,"Reports","Hardware","Capsule");
            Directory.CreateDirectory(folder);LastCapsuleFolder=folder;var hudCanvas=Hud.GetComponentInChildren<Canvas>();var hud=hudCanvas.enabled;hudCanvas.enabled=false;
            var baseZoom=ViewCamera.orthographicSize;var wasPaused=Paused;Paused=true;
            yield return null;
            foreach(var size in CapsuleSizes)
            {
                var aspect=size.Width/(float)size.Height;
                // Keep a similar horizontal coverage for tall formats and avoid empty sky in very wide ones.
                ViewCamera.orthographicSize=baseZoom*Mathf.Clamp(1.78f/aspect,.62f,1.9f);
                var rt=new RenderTexture(size.Width,size.Height,24,RenderTextureFormat.ARGB32){antiAliasing=4};
                ViewCamera.targetTexture=rt;ViewCamera.Render();
                var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(size.Width,size.Height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,size.Width,size.Height),0,0);tex.Apply();RenderTexture.active=previous;
                ViewCamera.targetTexture=null;File.WriteAllBytes(Path.Combine(folder,size.Name+"_"+size.Width+"x"+size.Height+".png"),tex.EncodeToPNG());
                Destroy(tex);rt.Release();Destroy(rt);yield return null;
            }
            ViewCamera.orthographicSize=baseZoom;Paused=wasPaused;hudCanvas.enabled=hud;HardwareAudio.Play("ready");Message="胶囊图已导出到 "+folder;
        }
        private IEnumerator CapsuleCommandLine()
        {
            yield return null;EnterShowcase();for(var t=0f;t<2;t+=Time.unscaledDeltaTime)yield return null;
            for(var i=0;i<ShowcaseShots.Length;i++)
            {
                ShowcaseShot(i);for(var t=0f;t<.6f;t+=Time.unscaledDeltaTime)yield return null;
                yield return ExportCapsules(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Reports","Hardware","Capsule","Shot"+(i+1)));
            }
            Application.Quit(0);
        }
    }
}
