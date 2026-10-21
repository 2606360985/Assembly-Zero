using System;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        private CommandResult DrivingCommand(WorldCommand c)
        {
            var v=State.Vehicles.FirstOrDefault(x=>x.Id==c.Vehicle);if(v==null||v.Role!=VehicleRole.Pickup)return Fail("请选择自用货车");
            if(v.RescueTicks>0)return Fail("正在救援，货物暂不可操作");
            if(c.Kind==CommandKind.TakeControl)
            {
                if(v.Manual||v.Phase!=VehiclePhase.Idle)return Fail("车辆正被占用");
                if(v.Journey!=9&&GridPoint.Distance(v.Position,Factory(1).Dock)>3)return Fail("请先自动返回车库再接管");
                v.Manual=true;v.Loop=false;v.DriveX=v.Position.X;v.DriveZ=v.Position.Z;v.Speed=0;v.Throttle=v.Steer=0;v.Status="手动驾驶";return Ok("WASD 驾驶 · E 装卸 · Tab 停靠并查看工厂");
            }
            if(c.Kind==CommandKind.ReleaseControl){if(!v.Manual)return Fail("车辆未被接管");v.Manual=false;v.Throttle=v.Steer=v.Speed=0;v.Phase=VehiclePhase.Idle;v.Journey=9;v.Status="停靠，等待玩家";return Ok("车辆已停靠");}
            if(c.Kind==CommandKind.RecoverVehicle){v.Manual=false;v.Throttle=v.Steer=v.Speed=0;v.RescueTicks=Settings.Hardware.RescueSeconds*60;v.Status="救援返库中";return Ok("救援耗时 "+Settings.Hardware.RescueSeconds+" 秒，货物保留");}
            if(!v.Manual)return Fail("请先接管货车");
            if(c.Kind==CommandKind.Drive){v.Throttle=Math.Max(-1,Math.Min(1,c.Throttle));v.Steer=Math.Max(-1,Math.Min(1,c.Steer));return Ok("驾驶");}
            if(c.Kind==CommandKind.RightVehicle){v.Speed=v.Throttle=v.Steer=0;v.Heading=0;return Ok("已原地回正，位置与货物不变");}
            if(c.Kind==CommandKind.Interact)
            {
                if(Math.Abs(v.Speed)>.3)return Fail("请停车后装卸");
                foreach(var f in State.Factories.Where(f=>f.Owned))if(GridPoint.Distance(v.Position,f.Dock)<=3)
                {
                    var count=v.Cargo.Count;
                    foreach(var g in v.Cargo.ToArray()){var n=State.Nodes.FirstOrDefault(n=>n.Factory==f.Id&&n.Kind==BuildingKind.Warehouse&&CanReceive(n,g.Material));if(n==null)continue;n.Output.Add(g);v.Cargo.Remove(g);}
                    return Ok(v.Cargo.Count>0?"部分卸货；等待仓库空间":"卸货完成，共 "+(count-v.Cargo.Count)+" 件");
                }
                var supplier=new GridPoint(Settings.Hardware.SupplierX,Settings.Hardware.SupplierZ);
                if(GridPoint.Distance(v.Position,supplier)<=3)
                {
                    var before=v.Cargo.Count;
                    foreach(var d in State.Hardware.Deliveries.Where(d=>d.Method==2).OrderBy(d=>d.Id).ToArray())
                    {
                        foreach(var g in d.Cargo.Take(Settings.VehicleCapacity-v.Cargo.Count).ToArray()){d.Cargo.Remove(g);v.Cargo.Add(g);}
                        if(d.Cargo.Count==0)State.Hardware.Deliveries.Remove(d);
                    }
                    return Ok(v.Cargo.Count==before?"暂无待提货物或货舱已满":"已装载 "+(v.Cargo.Count-before)+" 件；返回车库卸货");
                }
                var r=State.Resources.FirstOrDefault(r=>GridPoint.Distance(v.Position,r.Point)<=3);
                if(r!=null){if(v.Cargo.Count>=Settings.VehicleCapacity||r.Stock==0)return Fail("车满或资源不足");v.Resource=r.Id;v.WorkTicks=1;return Ok("采集进行中，停车等待；再次行驶会停止采集");}
                return Fail("靠近车库、采购站或铝原料点后操作");
            }
            return Fail("未知驾驶命令");
        }
        private void TickDrivenVehicle(WorldVehicle v)
        {
            if(v.RescueTicks>0){if(--v.RescueTicks==0){var p=Factory(1).Dock;v.Position=p;v.DriveX=p.X;v.DriveZ=p.Z;v.Phase=VehiclePhase.Idle;v.Status="救援完成，请卸货";}return;}
            v.Speed+=(v.Throttle*5-v.Speed)*.06;v.Heading+=v.Steer*Math.Min(1,Math.Abs(v.Speed))*1.7/60;
            var x=v.DriveX+Math.Sin(v.Heading)*v.Speed/60;var z=v.DriveZ+Math.Cos(v.Heading)*v.Speed/60;
            var p2=new GridPoint((int)Math.Round(x),(int)Math.Round(z));
            var blocked=GridRouting.WorldBlocked(p2)||State.Nodes.Any(n=>{var f=Factory(n.Factory);return Math.Abs(x-f.X-n.X)<2&&Math.Abs(z-f.Z-n.Z)<2;});
            if(!blocked){v.DriveX=x;v.DriveZ=z;v.Position=p2;}else v.Speed=0;
            if(Math.Abs(v.Throttle)>.01)v.WorkTicks=0;
            if(v.WorkTicks>0)
            {
                var r=State.Resources.FirstOrDefault(r=>r.Id==v.Resource);
                if(r==null||GridPoint.Distance(v.Position,r.Point)>3||v.Cargo.Count>=Settings.VehicleCapacity||r.Stock==0){v.WorkTicks=0;return;}
                if(++v.WorkTicks>=60){v.WorkTicks=1;r.Stock--;v.Cargo.Add(Create(0,r.Material));State.Gathered=true;}
            }
        }
    }
}
