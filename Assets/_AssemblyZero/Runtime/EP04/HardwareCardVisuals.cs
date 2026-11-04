using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Procedural SKU cards: length follows memory, fans and fin stack follow the cooler, the shroud uses the
    // SKU colour, newer generations add a backplate and light bar, and inspected cards carry a grade badge.
    public sealed partial class HardwareFactoryView
    {
        public static readonly Color[] ShellPalette={new Color(.09f,.14f,.2f),new Color(.83f,.9f,.94f),new Color(.62f,.12f,.14f),new Color(.12f,.3f,.62f),new Color(.1f,.45f,.32f),new Color(.78f,.6f,.22f),new Color(.4f,.26f,.58f),new Color(.93f,.94f,.95f)};
        public static readonly string[] ShellNames={"石墨","冰川白","赤焰","钴蓝","翡翠","香槟金","暮紫","珍珠白"};
        public static Color GradeColor(int grade)=>grade==WorldSimulation.GradeA?new Color(.95f,.75f,.2f):grade==WorldSimulation.GradeB?HardwareVisualCatalog.Cyan:grade==WorldSimulation.GradeC?new Color(.55f,.6f,.64f):new Color(.95f,.62f,.15f);
        private static Color GenColor(int gen)=>Color.HSVToRGB(((gen-1)*.17f+.5f)%1f,.75f,1);
        private bool CreateCardGeometry(int key,HardwareProductStyle s,Transform t)
        {
            if(!HardwareVisualKey.Styled(key)||s.Form!=HardwareProductForm.Card&&s.Form!=HardwareProductForm.Retail)return false;
            var memory=HardwareVisualKey.MemoryTier(key);var cooler=HardwareVisualKey.Cooler(key);var gen=HardwareVisualKey.Gen(key);var grade=HardwareVisualKey.Grade(key);
            var shell=ShellPalette[HardwareVisualKey.Shell(key)];var length=.92f*(1+.12f*memory+.12f*HardwareVisualKey.Length(key));var gold=new Color(.82f,.62f,.24f);
            if(s.Form==HardwareProductForm.Retail)
            {
                Case("SKU retail box",new Vector3(0,.12f,0),new Vector3(length+.02f,.3f,.5f),shell,t);
                Cube("Grade band",new Vector3(-length*.32f,.275f,0),new Vector3(.16f,.025f,.5f),GradeColor(grade),t).GetComponent<Renderer>().sharedMaterial=Glow(GradeColor(grade));
                Cube("Generation band",new Vector3(0,.12f,-.255f),new Vector3(length+.02f,.08f,.012f),GenColor(gen),t);
                Cube("GPU printed silhouette",new Vector3(.1f,.279f,0),new Vector3(length*.52f,.02f,.28f),cooler==1?dark:steel,t);
                for(var i=0;i<=memory;i++)Cube("Memory pip",new Vector3(length*.3f-i*.07f,.286f,.15f),new Vector3(.045f,.02f,.045f),green,t);
                return true;
            }
            var tall=cooler==2?.32f:cooler==1?.23f:.18f;
            Cube("PCB",Vector3.zero,new Vector3(length,.055f,.46f),new Color(.035f,.32f,.2f),t);
            for(int i=0;i<9;i++)Cube("Gold contact",new Vector3(-length*.35f+i*.065f,.033f,-.21f),new Vector3(.035f,.015f,.09f),gold,t);
            Case("Cooling shroud",new Vector3(0,.06f+tall/2,0),new Vector3(length-.01f,tall,.43f),shell,t);
            var fins=cooler==2?14:8;for(var i=0;i<fins;i++)Cube("Cooling fin",new Vector3(-length*.4f+i*length*.8f/(fins-1),.06f+tall*.4f,.224f),new Vector3(.025f,tall*.75f,.035f),steel,t);
            var count=HardwareVisualKey.Fans(key)>0?HardwareVisualKey.Fans(key):cooler==0?2:3;var diameter=Mathf.Min(.45f,length/count*.85f);
            for(var i=0;i<count;i++)Fan(new Vector3((i-(count-1)*.5f)*length/count,.07f+tall,0),diameter,t);
            if(HardwareVisualKey.Backplate(key))Cube("Backplate",new Vector3(0,-.04f,0),new Vector3(length,.02f,.45f),steel,t);
            if(gen>=4)Cube("Generation light bar",new Vector3(0,.06f+tall*.5f,-.225f),new Vector3(length*.85f,.05f,.02f),GenColor(gen),t).GetComponent<Renderer>().sharedMaterial=Glow(GenColor(gen));
            Cube("SKU light strip",new Vector3(0,.18f,-.24f),new Vector3(length*.6f,.06f,.03f),s.Color,t).GetComponent<Renderer>().sharedMaterial=Glow(s.Color);
            Cube(grade>0?"Grade badge":s.Qualified?"QC passed badge":"Pending inspection badge",new Vector3(length*.45f,.08f+tall,.18f),new Vector3(.09f,.03f,.09f),grade>0?GradeColor(grade):s.Qualified?green:amber,t);
            Cube("IO bracket",new Vector3(-length*.52f,.1f,0),new Vector3(.035f,.25f,.5f),steel,t);
            return true;
        }
        private Camera previewCamera;private RenderTexture previewTexture;private Transform previewStage,previewModel;private int previewKey=-1;
        // Renders a SKU card on an isolated stage for the designer; reuses one texture and rebuilds only on change.
        public Texture SkuPreview(int key)
        {
            if(previewTexture==null)
            {
                previewTexture=new RenderTexture(320,200,16){name="SKU preview"};
                previewStage=new GameObject("SKU preview stage").transform;previewStage.SetParent(transform,false);previewStage.position=new Vector3(-400,-200,-400);
                previewCamera=new GameObject("SKU preview camera",typeof(Camera)).GetComponent<Camera>();previewCamera.transform.SetParent(previewStage,false);
                previewCamera.transform.localPosition=new Vector3(1.2f,2f,-2.4f);previewCamera.transform.LookAt(previewStage.position+Vector3.up*.25f);
                previewCamera.targetTexture=previewTexture;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.05f,.09f,.13f);
                previewCamera.fieldOfView=30;previewCamera.nearClipPlane=.1f;previewCamera.farClipPlane=10;
            }
            if(key!=previewKey)
            {
                if(previewModel!=null)Destroy(previewModel.gameObject);
                previewModel=BuildProduct(key,previewStage);previewModel.localPosition=Vector3.zero;previewModel.localScale=Vector3.one*2.2f;previewKey=key;
            }
            previewCamera.enabled=true;return previewTexture;
        }
        public void HideSkuPreview(){if(previewCamera!=null)previewCamera.enabled=false;}
        private void ReleasePreview(){if(previewTexture!=null){previewTexture.Release();Destroy(previewTexture);previewTexture=null;}}
    }
}
