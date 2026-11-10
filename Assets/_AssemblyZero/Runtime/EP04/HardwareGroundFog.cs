using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AssemblyZero.Domain.EP04;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AssemblyZero.Unity.EP04
{
    [Serializable]
    public sealed class GroundFogSettings
    {
        [Range(128,1024)] public int Resolution = 768;
        [Min(.1f)] public float Height = 1.6f;
        [Range(0,2)] public float Density = .55f;
        [Tooltip("厂内雾量保留比例：0.3 表示比厂外降低 70%。")]
        [Range(0,1)] public float FactoryDensityMultiplier = .3f;
        public Color Color = new Color(.85f,.9f,.91f);
        public Color ShadowColor = new Color(.36f,.44f,.46f);
        public Vector2 Wind = new Vector2(.18f,.07f);
        [Range(.01f,2)] public float Recovery = .32f;
        [Min(.1f)] public float ClickRadius = 1.25f;
        [Range(0,5)] public float ClickStrength = 1.5f;
        [Min(.1f)] public float DragRadius = .7f;
        [Range(0,5)] public float DragStrength = 1.1f;
        [Tooltip("旋涡半径倍率：1 为原始大小。增大时同时拉开间距、减慢旋转；不改变清雾笔刷或车体半径。")]
        [Range(.5f,4)] public float VortexRadiusScale = 2f;
        [Range(0,3)] public float WakeSwirl = 1.4f;
        [Range(.5f,6)] public float WakePersistence = 3.2f;
        [Min(.1f)] public float ImpulseDuration = 1.2f;
        [Min(.1f)] public float TruckRadius = 1.1f;
        [Range(0,5)] public float TruckStrength = 1.2f;
        [Tooltip("节点成功落地时推开雾的范围，使用世界单位。")]
        [Min(.1f)] public float PlacementRadius = 2.4f;
        [Range(0,5)] public float PlacementStrength = 1.9f;
        [Range(.1f,3)] public float PlacementDuration = .75f;
    }

    /// <summary>Presentation-only world-space advection. No authoritative state or save data.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class HardwareGroundFog : MonoBehaviour
    {
        private const int MaxImpulses = 16, MaxMovements = 64, MaxVortices = 48;
        private const float FixedStep = 1f / 60;
        [StructLayout(LayoutKind.Sequential)]
        private struct Impulse { public Vector2 Position; public float Radius,Strength,Age,Duration,Placement,Padding; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Movement { public Vector2 Start,End; public float Radius,Strength,Speed,Dragged; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Vortex { public Vector2 Position,Drift; public float Radius,Spin,Age,Duration; }
        private readonly List<Vortex> vortices = new List<Vortex>(MaxVortices);
        private readonly Vortex[] vortexData = new Vortex[MaxVortices];
        private float dragDistance;
        private int dragSequence;
        private readonly List<Impulse> impulses = new List<Impulse>(MaxImpulses);
        private readonly List<Movement> movements = new List<Movement>(MaxMovements);
        private readonly Impulse[] impulseData = new Impulse[MaxImpulses];
        private readonly Movement[] movementData = new Movement[MaxMovements];
        private RenderTexture[] density;
        private RTHandle[] handles;
        private ComputeBuffer impulseBuffer,movementBuffer,vortexBuffer;
        private ComputeShader compute;
        private Material material;
        private GroundFogPass pass;
        private HardwareGame game;
        private WorldSimulation world;
        private GroundFogInteractor truck;
        private int current,initializeKernel,advectKernel,resolution;
        private float accumulator;
        private bool warned,wasPaused,wasRescuing;
        private readonly Vector4[] factoryAreas=new Vector4[16];
        public GroundFogSettings Settings { get; private set; }
        public RenderTexture DensityTexture => density == null ? null : density[current];
        public bool IsReady => density != null && material != null;
        public float SimulationTime { get; private set; }
        public int ActiveImpulseCount => impulses.Count;
        public int ActiveVortexCount => vortices.Count;
        public int StepCount { get; private set; }
        // Includes the playable map and a feathered margin; fixed in world space as cameras move.
        public Vector4 Area => new Vector4(-8,-8,GridRouting.WorldWidth*1.2f+16,GridRouting.WorldDepth*1.2f+16);

        public void Initialize(HardwareGame owner,GroundFogSettings settings)
        {
            game=owner;Settings=settings??new GroundFogSettings();
            truck=game.View.TruckVisual.GetComponent<GroundFogInteractor>();
            if(truck==null)truck=game.View.TruckVisual.gameObject.AddComponent<GroundFogInteractor>();
            truck.Fog=this;truck.Radius=Settings.TruckRadius;truck.Strength=Settings.TruckStrength;
            Allocate();
        }
        private void OnEnable(){if(game!=null)Allocate();}
        private void Allocate()
        {
            if(IsReady)return;
            var format=GraphicsFormat.R32_SFloat;
            if(!SystemInfo.supportsComputeShaders||!SystemInfo.IsFormatSupported(format,GraphicsFormatUsage.LoadStore))
            { Warn("Ground fog requires compute shaders and writable R32 textures; fog is disabled on this device.");return; }
            var source=Resources.Load<ComputeShader>("Hardware/GroundFog");
            var shader=Resources.Load<Shader>("Hardware/GroundFog");
            if(source==null||shader==null||!shader.isSupported){Warn("Ground fog shaders are unavailable; fog is disabled.");return;}
            try
            {
                compute=Instantiate(source);material=CoreUtils.CreateEngineMaterial(shader);
                initializeKernel=compute.FindKernel("Initialize");advectKernel=compute.FindKernel("Advect");
                resolution=Mathf.Clamp(Settings.Resolution,128,1024);
                density=new RenderTexture[2];handles=new RTHandle[2];
                for(var i=0;i<2;i++)
                {
                    density[i]=new RenderTexture(resolution,resolution,0){name="Ground fog density "+i,graphicsFormat=format,enableRandomWrite=true,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                    if(!density[i].Create())throw new InvalidOperationException("Could not allocate ground fog density texture.");
                    handles[i]=RTHandles.Alloc(density[i]);
                }
                impulseBuffer=new ComputeBuffer(MaxImpulses,Marshal.SizeOf<Impulse>());
                movementBuffer=new ComputeBuffer(MaxMovements,Marshal.SizeOf<Movement>());
                vortexBuffer=new ComputeBuffer(MaxVortices,Marshal.SizeOf<Vortex>());
                compute.SetBuffer(advectKernel,"_Vortices",vortexBuffer);
                compute.SetBuffer(advectKernel,"_Impulses",impulseBuffer);compute.SetBuffer(advectKernel,"_Movements",movementBuffer);
                pass=new GroundFogPass(material);
                RenderPipelineManager.beginCameraRendering+=BeginCamera;
                ResetField();
            }
            catch(Exception error){Release();Warn("Ground fog initialization failed: "+error.Message);}
        }
        private void Warn(string text){if(warned)return;warned=true;Debug.LogWarning(text,this);}
        public void ResetField()
        {
            impulses.Clear();movements.Clear();vortices.Clear();EndDragStroke();accumulator=0;SimulationTime=0;StepCount=0;current=0;
            world=game==null?null:game.World;wasPaused=game!=null&&game.Paused;wasRescuing=false;
            foreach(var item in GroundFogInteractor.Active)if(item!=null&&(item.Fog==null||item.Fog==this))item.ResetMotion();
            if(!IsReady)return;
            SetSimulationParameters();
            foreach(var texture in density){compute.SetTexture(initializeKernel,"_Result",texture);compute.Dispatch(initializeKernel,(resolution+7)/8,(resolution+7)/8,1);}
        }
        public void AddImpulse(Vector3 worldPosition,float radius,float strength)
        {
            if(!IsReady||game.Paused||!Contains(worldPosition)||!float.IsFinite(radius)||!float.IsFinite(strength)||radius<=0||strength<=0)return;
            if(impulses.Count==MaxImpulses)impulses.RemoveAt(0);
            impulses.Add(new Impulse{Position=new Vector2(worldPosition.x,worldPosition.z),Radius=radius,Strength=strength,Duration=Mathf.Max(.1f,Settings.ImpulseDuration)});
        }
        /// <summary>A newly placed body displaces fog outward and rolls it around its perimeter.</summary>
        public void AddPlacementImpact(Vector3 worldPosition)
        {
            if(!IsReady||game.Paused||!Contains(worldPosition)||Settings.PlacementStrength<=0)return;
            var radius=Mathf.Max(.1f,Settings.PlacementRadius);
            var strength=Settings.PlacementStrength;
            var scale=Mathf.Clamp(Settings.VortexRadiusScale,.5f,4);
            var center=new Vector2(worldPosition.x,worldPosition.z);
            if(impulses.Count==MaxImpulses)impulses.RemoveAt(0);
            impulses.Add(new Impulse{Position=center,Radius=radius,Strength=strength,Duration=Mathf.Max(.1f,Settings.PlacementDuration),Placement=1});
            for(var i=0;i<8;i++)
            {
                var angle=i*Mathf.PI*.25f;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                if(vortices.Count==MaxVortices)vortices.RemoveAt(0);
                vortices.Add(new Vortex
                {
                    Position=center+direction*radius,
                    Drift=direction*radius*.6f,
                    Radius=Mathf.Max(.45f,radius*.27f)*scale,
                    Spin=(i%2==0?1:-1)*strength*Settings.WakeSwirl*3/Mathf.Sqrt(scale),
                    Duration=Mathf.Max(.3f,Settings.WakePersistence)*(i%2==0?.7f:1.1f)
                });
            }
        }
        private bool Contains(Vector3 p){var a=Area;return p.x>=a.x&&p.z>=a.y&&p.x<=a.x+a.z&&p.z<=a.y+a.w;}
        /// <summary>A swept, directional brush. Call EndDragStroke when UI or another gesture interrupts it.</summary>
        public void AddDragStroke(Vector3 from,Vector3 to,float deltaTime)
        {
            if(!IsReady||game.Paused||!Contains(from)||!Contains(to)||deltaTime<=0)return;
            if(Vector3.Distance(from,to)>18){EndDragStroke();return;}
            if(AddMovement(from,to,Settings.DragRadius,Settings.DragStrength,deltaTime,true))
                ShedWake(from,to,Settings.DragRadius,Settings.DragStrength,deltaTime,ref dragDistance,ref dragSequence,true);
        }
        public void EndDragStroke(){dragDistance=0;}
        internal bool AddMovement(Vector3 from,Vector3 to,float radius,float strength,float dt,bool dragged=false)
        {
            var start=new Vector2(from.x,from.z);var end=new Vector2(to.x,to.z);
            if(!IsReady||game.Paused||(end-start).sqrMagnitude<.000001f||radius<=0||strength<=0||movements.Count==MaxMovements)return false;
            if(Mathf.Min(from.y,to.y)>Settings.Height+radius||Mathf.Max(from.y,to.y)<-.5f-radius)return false;
            movements.Add(new Movement{Start=start,End=end,Radius=radius,Strength=strength,Speed=Vector2.Distance(start,end)/Mathf.Max(dt,.0001f),Dragged=dragged?1:0});
            return true;
        }
        // Shed spatially spaced, counter-rotating pairs. Their independent lifetimes preserve
        // rolling motion after the body/brush has passed, including when the pickup stops.
        internal void ShedWake(Vector3 from,Vector3 to,float radius,float strength,float dt,ref float distance,ref int sequence,bool dragged=false)
        {
            var start=new Vector2(from.x,from.z);var travel=new Vector2(to.x-from.x,to.z-from.z);
            var length=travel.magnitude;if(length<.0001f)return;
            var forward=travel/length;var side=new Vector2(-forward.y,forward.x);
            var radiusScale=Mathf.Clamp(Settings.VortexRadiusScale,.5f,4);
            var spacing=Mathf.Max(.45f,radius*.9f)*radiusScale;var speed=Mathf.Min(length/Mathf.Max(.0001f,dt),24);
            var emitted=0;
            for(var along=spacing-distance;along<=length&&emitted<8;along+=spacing,emitted++)
            {
                var center=start+forward*along-forward*radius*.45f*radiusScale;
                for(var sign=-1;sign<=1;sign+=2)
                {
                    var small=(sequence+(sign>0?1:0))%3!=0;
                    var variation=.88f+.12f*Mathf.Sin(sequence*2.399f+sign);
                    var size=Mathf.Max(.45f,radius*(small?.58f:1.03f)*variation)*radiusScale;
                    var spin=(2.2f+speed*.45f)*Settings.WakeSwirl*strength*(small?1.2f:.48f)*sign/Mathf.Sqrt(radiusScale);
                    if(vortices.Count==MaxVortices)vortices.RemoveAt(0);
                    vortices.Add(new Vortex{Position=center+side*(sign*radius*.78f*radiusScale),Drift=forward*speed*(dragged?.1f:.07f),Radius=size,Spin=spin,Duration=Settings.WakePersistence*(small?.52f:1.18f)});
                }
                sequence++;
            }
            distance=(distance+length)%spacing;
        }
        private void LateUpdate()
        {
            if(!IsReady||game==null||game.World==null)return;
            if(world!=game.World)ResetField();
            var paused=game.Paused;
            if(paused||wasPaused!=paused)
            {
                movements.Clear();accumulator=0;
                foreach(var item in GroundFogInteractor.Active)if(item!=null&&(item.Fog==null||item.Fog==this))item.ResetMotion();
                wasPaused=paused;if(paused)return;
            }
            var rescuing=game.Truck.RescueTicks>0;
            if(rescuing||rescuing!=wasRescuing)truck?.ResetMotion();
            wasRescuing=rescuing;
            if(truck!=null){truck.Radius=Settings.TruckRadius;truck.Strength=Settings.TruckStrength;}
            var dt=Mathf.Min(Time.unscaledDeltaTime,.066667f);
            foreach(var item in GroundFogInteractor.Active)if(item!=null)item.Sample(this,dt);
            accumulator=Mathf.Min(accumulator+dt,FixedStep*4);
            if(accumulator<FixedStep)return;
            movementBuffer.SetData(CopyMovements());compute.SetInt("_MovementCount",movements.Count);
            while(accumulator>=FixedStep){Step(FixedStep);accumulator-=FixedStep;}
            movements.Clear();
        }
        private Movement[] CopyMovements(){movements.CopyTo(movementData);return movementData;}
        private void SetSimulationParameters()
        {
            compute.SetInt("_Resolution",resolution);compute.SetVector("_Area",Area);
            compute.SetVector("_Wind",new Vector4(Settings.Wind.x,Settings.Wind.y,0,0));
            compute.SetFloat("_VortexRadiusScale",Mathf.Clamp(Settings.VortexRadiusScale,.5f,4));
            compute.SetFloat("_Clock",SimulationTime);compute.SetFloat("_Recovery",Mathf.Max(.01f,Settings.Recovery));
        }
        private void Step(float dt)
        {
            SetSimulationParameters();compute.SetFloat("_Dt",dt);
            vortices.CopyTo(vortexData);vortexBuffer.SetData(vortexData);compute.SetInt("_VortexCount",vortices.Count);
            impulses.CopyTo(impulseData);impulseBuffer.SetData(impulseData);compute.SetInt("_ImpulseCount",impulses.Count);
            var next=1-current;compute.SetTexture(advectKernel,"_Source",density[current]);compute.SetTexture(advectKernel,"_Result",density[next]);
            compute.Dispatch(advectKernel,(resolution+7)/8,(resolution+7)/8,1);current=next;SimulationTime+=dt;StepCount++;
            for(var i=vortices.Count-1;i>=0;i--)
            {
                var v=vortices[i];v.Age+=dt;
                v.Position+=(Settings.Wind+v.Drift*Mathf.Exp(-v.Age*1.3f))*dt;
                if(v.Age>=v.Duration)vortices.RemoveAt(i);else vortices[i]=v;
            }
            for(var i=impulses.Count-1;i>=0;i--){var a=impulses[i];a.Age+=dt;if(a.Age>=a.Duration)impulses.RemoveAt(i);else impulses[i]=a;}
        }
        private void BeginCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!IsReady||game==null||camera!=game.ViewCamera||!isActiveAndEnabled)return;
            material.SetVector("_FogArea",Area);material.SetFloat("_FogBottom",-.5f);material.SetFloat("_FogTop",Mathf.Max(.1f,Settings.Height));
            material.SetFloat("_FogExtinction",Mathf.Max(0,Settings.Density));material.SetFloat("_FogEdge",5);
            material.SetFloat("_FogClock",SimulationTime);
            var count=Mathf.Min(factoryAreas.Length,game.World.State.Factories.Count);
            for(var i=0;i<count;i++){var f=game.World.State.Factories[i];factoryAreas[i]=new Vector4((f.X-.75f)*1.2f,(f.Z-.75f)*1.2f,(f.X+GridRouting.Width+.75f)*1.2f,(f.Z+GridRouting.Depth+.75f)*1.2f);}
            material.SetVectorArray("_FactoryAreas",factoryAreas);material.SetInt("_FactoryAreaCount",count);
            material.SetFloat("_FactoryFogMultiplier",Mathf.Clamp01(Settings.FactoryDensityMultiplier));
            // The existing night lighting supplies a cooler, dimmer scattering palette.
            var night=game.View.Night;
            material.SetColor("_FogColor",night?Settings.Color*.42f:Settings.Color);
            material.SetColor("_FogShadow",night?Settings.ShadowColor*.32f:Settings.ShadowColor);
            pass.Density=handles[current];camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }
        private void OnDisable(){Release();}
        private void OnDestroy(){Release();if(truck!=null&&truck.Fog==this)truck.Fog=null;}
        private void Release()
        {
            RenderPipelineManager.beginCameraRendering-=BeginCamera;
            vortexBuffer?.Release();vortexBuffer=null;
            impulseBuffer?.Release();impulseBuffer=null;movementBuffer?.Release();movementBuffer=null;
            if(handles!=null)foreach(var handle in handles)handle?.Release();handles=null;
            if(density!=null)foreach(var texture in density)if(texture!=null){texture.Release();Destroy(texture);}density=null;
            if(material!=null)CoreUtils.Destroy(material);material=null;
            if(compute!=null)Destroy(compute);compute=null;pass=null;
            impulses.Clear();movements.Clear();vortices.Clear();EndDragStroke();
        }
    }

    internal sealed class GroundFogPass : ScriptableRenderPass
    {
        public RTHandle Density;
        private readonly Material material;
        private sealed class Data { public TextureHandle Source,Depth,Density; public Material Material; }
        public GroundFogPass(Material value)
        {
            material=value;renderPassEvent=RenderPassEvent.BeforeRenderingTransparents;
            requiresIntermediateTexture=true;ConfigureInput(ScriptableRenderPassInput.Depth);
            profilingSampler=new ProfilingSampler("Hardware ground fog");
        }
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
        {
            var resources=frameData.Get<UniversalResourceData>();
            if(Density==null||resources.isActiveTargetBackBuffer||!resources.cameraDepthTexture.IsValid())return;
            var source=resources.activeColorTexture;var desc=graph.GetTextureDesc(source);desc.name="Ground fog composite";desc.clearBuffer=false;
            var destination=graph.CreateTexture(desc);
            using(var builder=graph.AddRasterRenderPass<Data>("Hardware ground fog",out var data,profilingSampler))
            {
                data.Material=material;data.Source=source;data.Depth=resources.cameraDepthTexture;data.Density=graph.ImportTexture(Density);
                builder.UseTexture(data.Source);builder.UseTexture(data.Depth);builder.UseTexture(data.Density);
                builder.SetRenderAttachment(destination,0);builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((Data d,RasterGraphContext context)=>
                {
                    context.cmd.SetGlobalTexture("_GroundFogDensity",d.Density);
                    context.cmd.SetGlobalTexture("_CameraDepthTexture",d.Depth);
                    Blitter.BlitTexture(context.cmd,d.Source,new Vector4(1,1,0,0),d.Material,0);
                });
            }
            resources.cameraColor=destination;
        }
    }
}
