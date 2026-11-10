using System.Linq;
using AssemblyZero.Domain.EP04;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareGame
    {
        public GridPoint ExplorationTarget
        {
            get
            {
                if(Truck.Cargo.Count>0)return World.Factory(Factory).Dock;
                var h=Config.Economy.Hardware;
                if(World.State.Hardware.Deliveries.Any(d=>d.Method==2&&d.Factory==Factory&&d.Cargo.Count>0))return new GridPoint(h.SupplierX,h.SupplierZ);
                for(var i=0;i<HardwareWorldMap.Landmarks.Length;i++)
                {var p=HardwareWorldMap.Landmarks[i];if(!World.State.Explored.Contains(new GridPoint(p.X/8,p.Z/8)))return p;}
                return new GridPoint(h.OreX,h.OreZ);
            }
        }
        public string ExplorationHint
        {
            get
            {
                var target=ExplorationTarget;string task;
                if(Truck.Cargo.Count>0)task="返厂卸货 · 停车后按 E";
                else if(World.State.Hardware.Deliveries.Any(d=>d.Method==2&&d.Factory==Factory&&d.Cargo.Count>0))task="采购站自提 · 停车后按 E";
                else
                {
                    var i=System.Array.IndexOf(HardwareWorldMap.Landmarks,target);
                    task=i>=0?"探索："+HardwareWorldMap.LandmarkNames[i]:"矿区采集 · 停车后按 E";
                }
                var distance=System.Math.Sqrt(System.Math.Pow(target.X-Truck.DriveX,2)+System.Math.Pow(target.Z-Truck.DriveZ,2))*1.2;
                return task+"\n"+(Truck.Manual?"距目标 "+distance.ToString("0")+" m · 金色路线指引":"接管小卡车，沿道路自由探索");
            }
        }
    }
}
