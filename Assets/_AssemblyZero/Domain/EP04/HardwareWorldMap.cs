using System.Collections.Generic;

namespace AssemblyZero.Domain.EP04
{
    // Shared by scenery and minimap. Existing factory footprints and logistics routes stay fixed.
    public static class HardwareWorldMap
    {
        public static readonly GridPoint[] Landmarks={new GridPoint(142,24),new GridPoint(146,100),new GridPoint(32,108)};
        public static readonly string[] LandmarkNames={"东岸观景台","北部货运环线","林间休息站"};
        public static HashSet<GridPoint> Roads(EconomySettings settings,IEnumerable<FactorySite> factories)
        {
            var cells=new HashSet<GridPoint>();var h=settings.Hardware;
            void Connect(GridPoint a,GridPoint b)
            {
                var path=GridRouting.HardwarePath(a,b,factories);
                if(path!=null)foreach(var p in path)cells.Add(p);
            }
            foreach(var f in factories)foreach(var destination in new[]{new GridPoint(h.SupplierX,h.SupplierZ),new GridPoint(h.OreX,h.OreZ)})Connect(f.Dock,destination);
            // Two connected loops around the retained industrial area, plus cross-town spurs.
            var loop=new[]{new GridPoint(1,1),new GridPoint(60,1),new GridPoint(154,1),new GridPoint(154,112),new GridPoint(1,112),new GridPoint(1,1)};
            for(var i=1;i<loop.Length;i++)Connect(loop[i-1],loop[i]);
            Connect(new GridPoint(60,1),new GridPoint(60,112));
            Connect(new GridPoint(1,40),new GridPoint(154,40));
            Connect(new GridPoint(60,90),new GridPoint(154,90));
            foreach(var f in factories)Connect(f.Dock,new GridPoint(1,f.Dock.Z));
            Connect(new GridPoint(h.SupplierX,h.SupplierZ),new GridPoint(h.SupplierX,h.SupplierZ-5));
            Connect(new GridPoint(h.SupplierX,h.SupplierZ-5),new GridPoint(154,h.SupplierZ-5));
            foreach(var p in Landmarks)Connect(p,new GridPoint(p.X==32?60:154,p.Z));
            return cells;
        }
        public static List<GridPoint> Trees(HashSet<GridPoint> roads,IEnumerable<FactorySite> factories,HardwareDefinition settings=null)
        {
            var trees=new List<GridPoint>();
            // Stable grid order keeps placement independent of HashSet enumeration and save state.
            for(var z=4;z<GridRouting.WorldDepth-3;z++)for(var x=4;x<GridRouting.WorldWidth-3;x++)
            {
                if((x+z)%6!=0)continue;
                if(settings!=null&&(System.Math.Abs(x-settings.SupplierX)<10&&System.Math.Abs(z-settings.SupplierZ)<9||System.Math.Abs(x-settings.OreX)<9&&System.Math.Abs(z-settings.OreZ)<8))continue;
                var p=new GridPoint(x,z);if(GridRouting.HardwareBlocked(p,factories))continue;
                var near=false;var clear=true;
                for(var dz=-3;dz<=3;dz++)for(var dx=-3;dx<=3;dx++)if(roads.Contains(new GridPoint(x+dx,z+dz)))
                {near=true;if(System.Math.Abs(dx)<=2&&System.Math.Abs(dz)<=2)clear=false;}
                if(near&&clear)trees.Add(p);
            }
            return trees;
        }
    }
}
