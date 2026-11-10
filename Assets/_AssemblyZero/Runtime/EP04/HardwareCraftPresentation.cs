using System.Collections.Generic;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Presentation only: every production pose is sampled from the authoritative batch progress.
    public sealed partial class HardwareFactoryView
    {
        private Mesh fanBlade, fanRing;
        private readonly Dictionary<int,int> inspectedKeys=new Dictionary<int,int>();
        private readonly Dictionary<int, Transform[]> precisionTools = new Dictionary<int, Transform[]>();
        private readonly Dictionary<int, Transform> scanBeams = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform[]> testRotors = new Dictionary<int, Transform[]>();
        private Transform[] previewRotors = new Transform[0];

        private Transform MeshPart(string name, Mesh mesh, Transform parent, Color color)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
            part.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = Paint(color);
            return part;
        }

        private Mesh BladeMesh()
        {
            if (fanBlade != null) return fanBlade;
            var vertices = new List<Vector3>(); var indices = new List<int>();
            for (var i = 0; i <= 8; i++)
            {
                float t = i / 8f, radius = Mathf.Lerp(.12f, .45f, t);
                float angle = Mathf.Lerp(-.3f, .38f, t), width = Mathf.Lerp(.15f, .39f, t);
                foreach (var a in new[] { angle, angle + width })
                    vertices.Add(new Vector3(Mathf.Sin(a) * radius, Mathf.Sin(t * Mathf.PI) * .025f, Mathf.Cos(a) * radius));
                if (i == 0) continue;
                int k = i * 2;
                indices.AddRange(new[] { k-2,k,k-1,k-1,k,k+1,k-1,k,k-2,k+1,k,k-1 });
            }
            fanBlade = new Mesh { name = "Swept GPU fan blade" };
            fanBlade.SetVertices(vertices); fanBlade.SetTriangles(indices, 0); fanBlade.RecalculateNormals(); fanBlade.RecalculateBounds();
            ownedMeshes.Add(fanBlade); return fanBlade;
        }

        private Mesh RingMesh()
        {
            if (fanRing != null) return fanRing;
            var vertices = new List<Vector3>(); var indices = new List<int>();
            for (var i = 0; i <= 40; i++)
            {
                var a = i * Mathf.PI * 2 / 40;
                foreach (var radius in new[] { .465f, .51f })
                    vertices.Add(new Vector3(Mathf.Sin(a)*radius, 0, Mathf.Cos(a)*radius));
                if (i == 0) continue;
                int k = i*2; indices.AddRange(new[] { k-2,k-1,k,k-1,k+1,k });
            }
            fanRing = new Mesh { name = "Open GPU fan rim" };
            fanRing.SetVertices(vertices); fanRing.SetTriangles(indices, 0); fanRing.RecalculateNormals(); fanRing.RecalculateBounds();
            ownedMeshes.Add(fanRing); return fanRing;
        }

        private static Transform[] Rotors(Transform model)
        {
            var list = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "Fan rotor") list.Add(t);
            return list.ToArray();
        }

        private Transform DisplayProduct(int key, Transform parent)
        {
            var model = new GameObject("Detailed product display").transform;
            model.SetParent(parent, false); CreateProductGeometry(key, model); return model;
        }

        private void DetailPrecisionStation(BusinessNode node, Transform station)
        {
            if (node.Kind != BuildingKind.GPUAssembly && node.Kind != BuildingKind.Tester) return;
            foreach (var x in new[] { -1.12f, 1.12f })
            {
                Case("Precision rail", new Vector3(x,.99f,-.5f), new Vector3(.12f,.12f,1.35f), steel, station);
                Case("Clamping jaw", new Vector3(x*.56f,1.07f,-.5f), new Vector3(.15f,.16f,.62f), dark, station);
            }
            for (var i=0;i<3;i++)
                Cube("Cycle indicator", new Vector3(.84f+i*.12f,1.25f,-1.45f), new Vector3(.065f,.035f,.025f), cyan, station).GetComponent<Renderer>().sharedMaterial=Glow(cyan);
            if (node.Kind == BuildingKind.GPUAssembly)
            {
                var tools = new List<Transform>();
                foreach (Transform t in station) if (t.name == "Robot tool")
                {
                    tools.Add(t);
                    Shape("Tool spindle", new Vector3(0,-.33f,0), new Vector3(.12f,.12f,.12f), steel, t, PrimitiveType.Cylinder);
                }
                precisionTools[node.Id]=tools.ToArray();
                foreach(var x in new[]{-1.05f,1.05f})
                {
                    var pivot=Shape("Servo joint",new Vector3(x*.65f,1.92f,.29f),new Vector3(.3f,.07f,.3f),steel,station,PrimitiveType.Cylinder);
                    pivot.localRotation=Quaternion.Euler(90,0,0);
                }
            }
            else
            {
                var beam=Cube("Inspection scan",new Vector3(0,1.14f,-.7f),new Vector3(1.55f,.012f,.035f),cyan,station);
                beam.GetComponent<Renderer>().sharedMaterial=Glow(cyan); scanBeams[node.Id]=beam;
                Case("Inspection screen bezel",new Vector3(.9f,1.65f,-1.19f),new Vector3(.84f,.55f,.12f),dark,station);
                for(var i=0;i<8;i++)
                    Cube("Diagnostic waveform",new Vector3(.57f+i*.09f,1.6f+Mathf.Sin(i*1.8f)*.09f,-1.26f),new Vector3(.055f,.04f,.015f),cyan,station).GetComponent<Renderer>().sharedMaterial=Glow(cyan);
            }
        }

        private void PresentPrecision(float tick)
        {
            foreach(var pair in precisionTools)
            {
                var n=game.World.Node(pair.Key); if(n==null)continue;
                bool working=n.Batch.Count>0;
                float p=working&&n.WorkTotal>0?Mathf.Clamp01(1-n.WorkRemaining/(float)n.WorkTotal):0;
                float reach=Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.23f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((p-.78f)/.22f)));
                for(var i=0;i<pair.Value.Length;i++)
                {
                    var tool=pair.Value[i]; var sign=i==0?-1:1;
                    tool.localPosition=new Vector3(sign*Mathf.Lerp(.65f,.3f,reach),1.8f-.38f*reach,Mathf.Lerp(.55f,-.6f,reach));
                    tool.localRotation=Quaternion.Euler(0,working&&p>.3f&&p<.7f?p*1440:0,0);
                }
            }
            foreach(var pair in scanBeams)
            {
                var n=game.World.Node(pair.Key);bool working=n!=null&&n.Batch.Count>0;
                if(working&&inputPieces.TryGetValue(pair.Key,out var previous))
                {
                    var good=n.Batch[0];var key=HardwareVisualKey.Of(HardwareCatalog.RawGPU,game.World.Sku(good.SpecId),good.Grade,good.Gen);
                    if(!inspectedKeys.TryGetValue(pair.Key,out var oldKey)||oldKey!=key)
                    {
                        var replacement=DisplayProduct(key,previous.parent);replacement.localPosition=previous.localPosition;replacement.localScale=previous.localScale;
                        previous.gameObject.SetActive(false);Destroy(previous.gameObject);inputPieces[pair.Key]=replacement;testRotors[pair.Key]=Rotors(replacement);inspectedKeys[pair.Key]=key;
                    }
                }
                pair.Value.gameObject.SetActive(working);
                float p=working&&n.WorkTotal>0?Mathf.Clamp01(1-n.WorkRemaining/(float)n.WorkTotal):0;
                pair.Value.localPosition=new Vector3(0,1.14f,Mathf.Lerp(-1.03f,-.35f,Mathf.PingPong(p*2,1)));
                if(testRotors.TryGetValue(pair.Key,out var rotors))foreach(var rotor in rotors)
                    rotor.localRotation=Quaternion.Euler(0,working?tick*8:0,0);
            }
            if(previewCamera!=null&&previewCamera.enabled&&previewModel!=null)
            {
                previewModel.localRotation=Quaternion.Euler(0,Mathf.Sin(Time.unscaledTime*.3f)*18,0);
                foreach(var rotor in previewRotors)if(rotor!=null)rotor.localRotation=Quaternion.Euler(0,Time.unscaledTime*180,0);
            }
        }
    }
}
