using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace AssemblyZero.Unity.EP04
{
    // Per-camera radial blur injected at runtime, so shared URP renderer assets stay untouched.
    public sealed class HardwareRadialBlurPass : ScriptableRenderPass
    {
        public Material Material;
        public HardwareRadialBlurPass(){renderPassEvent=RenderPassEvent.BeforeRenderingPostProcessing;requiresIntermediateTexture=true;}
        public override void RecordRenderGraph(RenderGraph renderGraph,ContextContainer frameData)
        {
            if(Material==null)return;var resources=frameData.Get<UniversalResourceData>();if(resources.isActiveTargetBackBuffer)return;
            var source=resources.activeColorTexture;var desc=renderGraph.GetTextureDesc(source);desc.name="Hardware radial focus";desc.clearBuffer=false;
            var destination=renderGraph.CreateTexture(desc);
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source,destination,Material,0),"Hardware radial focus");
            resources.cameraColor=destination;
        }
    }
    // Macro "miniature" look: bokeh depth of field around the focus plane (tilted camera gives
    // top/bottom falloff), radial edge blur, vignette and light grading. Hardware scene only.
    public sealed class HardwareLens : MonoBehaviour
    {
        private const string Prefs="AssemblyZero.Hardware.Lens";
        public static readonly float[] Strengths={0,.5f,1};
        private const float NodeFocusHeight=2.8f;
        private Camera view;private HardwareGame game;private Volume volume;private VolumeProfile profile;private Material material;private HardwareRadialBlurPass pass;
        private DepthOfField dof;private Vignette vignette;private Bloom bloom;private ColorAdjustments grading;
        public float Strength {get;private set;}=.5f;
        public bool Night {get;private set;}
        public bool RadialAvailable=>material!=null;
        public bool Active=>Strength>0&&!Suspended;
        public bool Suspended;
        public Volume Volume=>volume;
        public void Initialize(Camera camera)
        {
            view=camera;game=GetComponent<HardwareGame>();Strength=PlayerPrefs.GetFloat(Prefs,.5f);
            var data=view.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
            volume=new GameObject("Hardware lens volume").AddComponent<Volume>();volume.transform.SetParent(transform,false);volume.isGlobal=true;volume.priority=50;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Hardware lens profile";volume.sharedProfile=profile;
            dof=profile.Add<DepthOfField>(true);dof.mode.Override(DepthOfFieldMode.Bokeh);
            vignette=profile.Add<Vignette>(true);vignette.smoothness.Override(.5f);vignette.color.Override(new Color(.02f,.05f,.08f));
            bloom=profile.Add<Bloom>(true);bloom.threshold.Override(.95f);bloom.scatter.Override(.6f);
            grading=profile.Add<ColorAdjustments>(true);grading.saturation.Override(10);grading.contrast.Override(8);
            var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);
            var shader=Resources.Load<Shader>("Hardware/RadialFocusBlur");
            if(shader!=null&&shader.isSupported){material=CoreUtils.CreateEngineMaterial(shader);pass=new HardwareRadialBlurPass{Material=material};}
            else Debug.LogWarning("Hardware radial focus shader unavailable; using depth of field and vignette only.");
            RenderPipelineManager.beginCameraRendering+=BeginCamera;Apply();
        }
        public void CycleStrength(){var i=System.Array.IndexOf(Strengths,Strength);Strength=Strengths[(i<0?1:i+1)%Strengths.Length];PlayerPrefs.SetFloat(Prefs,Strength);PlayerPrefs.Save();Apply();}
        public void SetStrength(float value){Strength=Mathf.Clamp01(value);Apply();}
        public void SetNight(bool night){Night=night;Apply();}
        private void Apply()
        {
            if(volume==null)return;var on=Active;
            dof.active=on;vignette.active=true;
            vignette.intensity.Override(on?.22f+.18f*Strength:Night?.3f:.12f);
            bloom.intensity.Override(Night?1.6f:on?.35f:.2f);
            grading.postExposure.Override(Night?-.35f:0);
        }
        private void LateUpdate()
        {
            if(view==null||dof==null)return;
            // Focus on the surface under the screen centre. Machines fill the frame at close zoom,
            // so node cells focus at machine-top height instead of the ground plane.
            var distance=57f;
            if(new Plane(Vector3.up,Vector3.zero).Raycast(view.ViewportPointToRay(new Vector3(.5f,.5f)),out var d))
            {
                var point=view.ViewportPointToRay(new Vector3(.5f,.5f)).GetPoint(d);distance=d;
                var f=game?.World?.Factory(game.Factory);
                if(f!=null&&game.NodeAt(Mathf.RoundToInt(point.x/1.2f)-f.X,Mathf.RoundToInt(point.z/1.2f)-f.Z)!=0)
                    distance=Vector3.Distance(view.transform.position,point+Vector3.up*NodeFocusHeight);
            }
            dof.focusDistance.Override(distance);dof.aperture.Override(Mathf.Lerp(5.6f,1.4f,Strength));
            // Calibrated on the orthographic factory camera: ~5mm is sharp. Close zoom reduces
            // blur so the centre stays crisp; pulling back restores the miniature falloff.
            dof.focalLength.Override(Mathf.Lerp(4f,13f,Strength)*Mathf.Clamp(11/Mathf.Max(1,view.orthographicSize),.6f,1));
        }
        private void BeginCamera(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=view||pass==null||!Active)return;
            material.SetFloat("_Strength",.0045f+.0075f*Strength);material.SetFloat("_FocusRadius",.26f);material.SetFloat("_Feather",.42f);
            material.SetFloat("_Aspect",camera.pixelHeight>0?camera.pixelWidth/(float)camera.pixelHeight:1.78f);material.SetVector("_FocusCenter",new Vector4(.5f,.52f,0,0));
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }
        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering-=BeginCamera;
            if(material!=null)CoreUtils.Destroy(material);if(profile!=null)Destroy(profile);
        }
    }
}
