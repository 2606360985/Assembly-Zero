using System.Collections.Generic;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareFactoryView
    {
        private void BuildExplorationRoads(HashSet<GridPoint> roads)
        {
            // Merge east-west tiles into strips, avoiding a GameObject per road cell.
            for(var z=0;z<GridRouting.WorldDepth;z++)for(var x=0;x<GridRouting.WorldWidth;x++)
            {
                if(!roads.Contains(new GridPoint(x,z)))continue;var start=x;
                while(x+1<GridRouting.WorldWidth&&roads.Contains(new GridPoint(x+1,z)))x++;
                Cube("Exterior road",new Vector3((start+x)*.6f,-.02f,z*1.2f),new Vector3((x-start)*1.2f+2.7f,.14f,2.7f),new Color(.17f,.23f,.27f),environment);
                for(var mark=start;mark<=x;mark++)if((mark+z)%5==0)
                    Cube("Road center marking",new Vector3(mark*1.2f,.065f,z*1.2f),new Vector3(.4f,.025f,.15f),ivory,environment);
            }
            var h=game.Config.Economy.Hardware;
            foreach(var p in HardwareWorldMap.Trees(roads,game.World.State.Factories,h))
            {
                var tree=new GameObject("Roadside tree").transform;tree.SetParent(environment,false);tree.localPosition=new Vector3(p.X*1.2f,0,p.Z*1.2f);
                var height=2.5f+(p.X*13+p.Z*7)%5*.18f;
                Shape("Tree trunk",Vector3.up*height*.45f,new Vector3(.27f,height*.45f,.27f),new Color(.35f,.26f,.18f),tree,PrimitiveType.Cylinder);
                Shape("Tree crown",Vector3.up*height,new Vector3(2.6f,3.4f,2.6f),new Color(.18f+(p.X%3)*.025f,.38f+(p.Z%3)*.025f,.25f),tree,PrimitiveType.Sphere);
                Cube("Tree planter",new Vector3(0,-.05f,0),new Vector3(1.1f,.16f,1.1f),new Color(.43f,.49f,.45f),tree);
            }
            for(var i=0;i<HardwareWorldMap.Landmarks.Length;i++)
            {
                var p=HardwareWorldMap.Landmarks[i];var at=new Vector3(p.X*1.2f,0,p.Z*1.2f);
                Case("Exploration stop",at-Vector3.up*.03f,new Vector3(6,.18f,6),steel,environment);
                Cube("Wayfinding post",at+new Vector3(2,1.1f,2),new Vector3(.15f,2.2f,.15f),ivory,environment);
                Cube("Wayfinding sign",at+new Vector3(2,2.1f,2),new Vector3(2.6f,.85f,.18f),cyan,environment);
                Label(HardwareWorldMap.LandmarkNames[i],at+Vector3.up*3.3f,environment,.4f);
            }
        }
    }
}
