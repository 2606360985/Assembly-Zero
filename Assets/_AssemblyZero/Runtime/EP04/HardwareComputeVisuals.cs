using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareFactoryView
    {
        private bool BuildComputeModel(BusinessNode n,Transform parent)
        {
            if(n.Kind!=BuildingKind.ComputeRack&&n.Kind!=BuildingKind.PowerUnit&&n.Kind!=BuildingKind.CoolingUnit)return false;
            Case("Park equipment",new Vector3(0,1.35f,0),new Vector3(2.5f,2.1f,2.1f),ivory,parent);
            if(n.Kind==BuildingKind.ComputeRack)
            {
                Case("Server face",new Vector3(0,1.4f,-1.08f),new Vector3(2.18f,1.8f,.1f),dark,parent);
                for(var i=0;i<4;i++)
                {
                    Case("GPU slot "+i,new Vector3(0,.8f+i*.4f,-1.17f),new Vector3(1.9f,.3f,.15f),steel,parent);
                    Cube("Compute indicator "+i,new Vector3(.78f,.8f+i*.4f,-1.27f),new Vector3(.12f,.055f,.035f),cyan,parent).GetComponent<Renderer>().sharedMaterial=Glow(cyan);
                    for(var k=0;k<5;k++)Cube("Server ventilation",new Vector3(-.7f+k*.25f,.8f+i*.4f,-1.26f),new Vector3(.13f,.035f,.02f),dark,parent);
                }
            }
            else if(n.Kind==BuildingKind.PowerUnit)
            {
                for(var i=0;i<3;i++)Case("Power cell",new Vector3((i-1)*.65f,1.4f,-1.08f),new Vector3(.45f,1.55f,.2f),dark,parent);
                Cube("Power bus",new Vector3(0,2.37f,0),new Vector3(2.05f,.1f,.2f),amber,parent);
            }
            else
            {
                for(var i=0;i<12;i++)Cube("Cooling grille",new Vector3(-1+i*.18f,1.4f,-1.08f),new Vector3(.06f,1.55f,.05f),steel,parent);
                foreach(var x in new[]{-.7f,.7f})Shape("Cooling riser",new Vector3(x,1.4f,1.15f),new Vector3(.16f,.95f,.16f),cyan,parent,PrimitiveType.Cylinder);
            }
            return true;
        }
        private void PresentComputeModels()
        {
            foreach(var pair in stations)
            {
                var n=game.World.Node(pair.Key);if(n==null||n.Kind!=BuildingKind.ComputeRack)continue;
                var model=pair.Value.Find("Model");if(model==null)continue;
                for(var i=0;i<4;i++)
                {
                    var light=model.Find("Compute indicator "+i);if(light!=null)light.GetComponent<Renderer>().sharedMaterial=i<n.Input.Count?Glow(cyan):Paint(dark);
                }
            }
        }
    }
}
