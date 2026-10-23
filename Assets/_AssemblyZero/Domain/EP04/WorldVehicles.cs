using System;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        private System.Collections.Generic.List<GridPoint> VehiclePath(GridPoint start,GridPoint end)
            =>Settings.IsHardware?GridRouting.HardwarePath(start,end,State.Factories):GridRouting.WorldPath(start,end);

        private void MigrateHardwareLayout()
        {
            if(State.Hardware.LayoutVersion>=Settings.Hardware.LayoutVersion)return;
            var resource=State.Resources.First(r=>r.Id==1);
            resource.X=Settings.Hardware.OreX;resource.Z=Settings.Hardware.OreZ;
            foreach(var v in State.Vehicles)
            {
                // Loading an old save may place the truck inside a newly enclosed factory.
                if(GridRouting.HardwareBlocked(v.Position,State.Factories))
                {
                    var closest=v.Position;var distance=int.MaxValue;
                    for(var x=0;x<GridRouting.WorldWidth;x++)for(var z=0;z<GridRouting.WorldDepth;z++)
                    {
                        var p=new GridPoint(x,z);var d=GridPoint.Distance(v.Position,p);
                        if(d<distance&&!GridRouting.HardwareBlocked(p,State.Factories)){closest=p;distance=d;}
                    }
                    v.Position=closest;v.DriveX=closest.X;v.DriveZ=closest.Z;
                }
                if(v.Manual||v.RescueTicks>0)continue;
                if(v.Phase==VehiclePhase.Travelling)
                {
                    var end=v.Journey==1?resource.Point:v.Journey==4?new GridPoint(Settings.Hardware.SupplierX,Settings.Hardware.SupplierZ):
                        v.Journey==2?Factory(v.TargetFactory).Dock:v.Journey==3?Factory(v.SourceFactory).Dock:
                        v.Path.Count>0?v.Path[v.Path.Count-1]:v.Position;
                    Travel(v,end,v.Journey);
                }
                else if(v.Phase==VehiclePhase.Gathering)Travel(v,resource.Point,1);
                else if(v.Phase==VehiclePhase.WaitingStock&&v.PickupTask)Travel(v,new GridPoint(Settings.Hardware.SupplierX,Settings.Hardware.SupplierZ),4);
            }
            State.Hardware.LayoutVersion=Settings.Hardware.LayoutVersion;
        }
        private CommandResult VehicleCommand(WorldCommand c)
        {
            var v=State.Vehicles.FirstOrDefault(x=>x.Id==c.Vehicle);
            if(v!=null&&(v.Manual||v.RescueTicks>0))return Fail("车辆由玩家控制或正在救援");
            if(c.Kind==CommandKind.Station)
            {
                var resource=State.Resources.FirstOrDefault(r=>r.Id==c.Resource);
                if(resource==null||!resource.Discovered||resource.Station)return Fail("请选择已发现且未建站的资源点");
                if(State.Unlocked<2)return Fail("首次销售 T1 后开放采集站");
                if(!Pay(Settings.StationCost))return Fail("采集站资金不足");resource.Station=true;Revision++;return Ok("采集站已建设，采集效率翻倍");
            }
            if(c.Kind==CommandKind.HireCollector||c.Kind==CommandKind.HireHauler)
            {
                var collector=c.Kind==CommandKind.HireCollector;
                if(State.Unlocked<(collector?2:3))return Fail("尚未解锁该团队");
                if(Factory(c.TargetFactory)?.Owned!=true||Factory(c.Factory)?.Owned!=true)return Fail("请指定已购买的厂址");
                if(collector&&!State.Resources.Any(r=>r.Id==c.Resource&&r.Discovered))return Fail("请选择已发现的资源点");
                if(!collector&&(c.Factory==c.TargetFactory||!ValidMaterial(c.Material)))return Fail("调拨需要不同工厂与有效物料");
                var price=collector?Settings.CollectorCost:Settings.HaulerCost;if(!Pay(price))return Fail("雇佣资金不足");
                v=new WorldVehicle{Id=State.NextVehicle++,Role=collector?VehicleRole.Collector:VehicleRole.Hauler,Position=Factory(c.Factory).Dock,TargetFactory=c.TargetFactory,SourceFactory=c.Factory,Resource=c.Resource,Material=c.Material,Loop=true,TransferTask=!collector};
                State.Vehicles.Add(v);State.Hired=true;StartTrip(v);Revision++;return Ok("团队已雇佣，可在车辆面板查看任务",v.Id);
            }
            if(v==null)return Fail("未选择车辆");
            if(c.Kind==CommandKind.Boost){v.Boost=!v.Boost;return Ok(v.Boost?"车辆行驶加速已开启":"车辆恢复普通速度");}
            if(c.Kind==CommandKind.Explore)
            {
                if(v.Role!=VehicleRole.Pickup)return Fail("请使用皮卡探索");
                var destination=new GridPoint(c.X,c.Z);var path=VehiclePath(v.Position,destination);if(path==null)return Fail("目标不可达，请选择道路或空地");
                v.Loop=false;v.TransferTask=false;v.PickupTask=false;SetJourney(v,path,0);return Ok("皮卡正在探索");
            }
            if(c.Kind==CommandKind.Return)
            {
                if(Factory(c.TargetFactory)?.Owned!=true)return Fail("目标工厂未购买");
                var path=VehiclePath(v.Position,Factory(c.TargetFactory).Dock);if(path==null)return Fail("返厂路径不可达");
                v.TargetFactory=c.TargetFactory;v.Loop=false;v.PickupTask=false;v.WorkTicks=0;
                SetJourney(v,path,2);return Ok("已停止循环；正在返厂自动卸货");
            }
            if(c.Kind==CommandKind.PickupPurchase)
            {
                if(!Settings.IsHardware||v.Role!=VehicleRole.Pickup||Factory(c.TargetFactory)?.Owned!=true)return Fail("请选择皮卡和已购买的目标工厂");
                if(v.Cargo.Count>0)return Fail("请先返厂卸下当前货物");
                if(!State.Hardware.Deliveries.Any(d=>d.Method==2&&d.Factory==c.TargetFactory&&d.Cargo.Count>0))return Fail("当前工厂没有待自提采购");
                var path=VehiclePath(v.Position,new GridPoint(Settings.Hardware.SupplierX,Settings.Hardware.SupplierZ));if(path==null)return Fail("采购站不可达");
                v.TargetFactory=c.TargetFactory;v.TransferTask=false;v.PickupTask=true;v.Loop=true;v.WorkTicks=0;SetJourney(v,path,4);
                return Ok("自动自提已规划：前往采购站、分批装运、返厂卸货，全部取回后待命");
            }
            if(c.Kind==CommandKind.Collect)
            {
                var resource=State.Resources.FirstOrDefault(r=>r.Id==c.Resource&&r.Discovered);
                if(resource==null||Factory(c.TargetFactory)?.Owned!=true)return Fail("请选择已发现资源和有效目标工厂");
                if(v.Cargo.Count>0)return Fail("请先将车上货物卸入仓库");
                if(VehiclePath(v.Position,resource.Point)==null)return Fail("资源点不可达");
                v.Resource=c.Resource;v.TargetFactory=c.TargetFactory;v.TransferTask=false;v.PickupTask=false;v.WorkTicks=0;v.Loop=Settings.IsHardware||v.Role==VehicleRole.Collector;StartTrip(v);return Ok(v.Loop?"自动采集已规划：采满返厂、自动卸货后继续；可随时返厂停止":"采集任务已下达");
            }
            if(c.Kind==CommandKind.Transfer)
            {
                if(Factory(c.Factory)?.Owned!=true||Factory(c.TargetFactory)?.Owned!=true||c.Factory==c.TargetFactory||!ValidMaterial(c.Material))return Fail("请选择两个不同的已购工厂及物料");
                if(v.Cargo.Count>0)return Fail("请先卸下当前货物");
                v.SourceFactory=c.Factory;v.TargetFactory=c.TargetFactory;v.Material=c.Material;v.TransferTask=true;v.PickupTask=false;v.Loop=v.Role==VehicleRole.Hauler;StartTrip(v);return Ok("调拨任务已下达");
            }
            return Fail("未知车辆命令");
        }
        private bool ValidMaterial(int material)=>Settings.IsHardware?Settings.Hardware.Materials.Any(m=>m.Id==material):new[]{8,9,10,13,16,17,18,19}.Contains(material);
        private void SetJourney(WorldVehicle v,System.Collections.Generic.List<GridPoint> path,int journey)
        {
            v.Path=path;v.PathIndex=0;v.SegmentTicks=0;v.Journey=journey;v.Phase=VehiclePhase.Travelling;v.Status=journey==0?"探索中":journey==1?"前往资源点":journey==2?"返厂运输":journey==4?"前往采购站自提":"前往调拨仓库";
        }
        private bool Travel(WorldVehicle v,GridPoint end,int journey)
        {
            var path=VehiclePath(v.Position,end);if(path==null){v.Phase=VehiclePhase.Idle;v.Status="道路不可达";return false;}SetJourney(v,path,journey);return true;
        }
        private void StartTrip(WorldVehicle v)
        {
            if(v.PickupTask&&!State.Hardware.Deliveries.Any(d=>d.Method==2&&d.Factory==v.TargetFactory&&d.Cargo.Count>0))
            {v.Loop=false;v.PickupTask=false;v.Phase=VehiclePhase.Idle;v.Status="自提完成，等待规划";return;}
            var end=v.PickupTask?new GridPoint(Settings.Hardware.SupplierX,Settings.Hardware.SupplierZ):v.TransferTask?Factory(v.SourceFactory).Dock:State.Resources.First(r=>r.Id==v.Resource).Point;
            var path=VehiclePath(v.Position,end);if(path==null){v.Phase=VehiclePhase.Idle;v.Status="道路不可达";return;}
            if(v.Role!=VehicleRole.Pickup&&!Pay(Settings.TripFee)){v.Phase=VehiclePhase.WaitingFunds;v.Status="资金不足，暂停派遣";return;}
            SetJourney(v,path,v.PickupTask?4:v.TransferTask?3:1);
        }
        private void Discover(GridPoint position)
        {
            var cell=new GridPoint(position.X/8,position.Z/8);if(!State.Explored.Contains(cell)){State.Explored.Add(cell);Revision++;}
            foreach(var r in State.Resources)if(!r.Discovered&&GridPoint.Distance(position,r.Point)<=9){r.Discovered=true;Revision++;Log("发现 "+EconomySettings.MaterialName(r.Material)+"资源点");}
            foreach(var f in State.Factories)if(!f.Discovered&&GridPoint.Distance(position,f.Dock)<=12){f.Discovered=true;Revision++;Log("发现第二厂址");}
        }
        private void TickVehicle(WorldVehicle v)
        {
            if(Settings.IsHardware&&(v.Manual||v.RescueTicks>0)){TickDrivenVehicle(v);return;}
            Discover(v.Position);
            if(v.Phase==VehiclePhase.WaitingFunds){if(State.Tick%60==0&&State.Money>=Settings.TripFee)StartTrip(v);return;}
            if(v.Phase==VehiclePhase.Idle)return;
            if(v.Phase==VehiclePhase.Travelling)
            {
                if(++v.SegmentTicks<(v.Boost?5:10))return;v.SegmentTicks=0;
                if(v.PathIndex+1<v.Path.Count){v.Position=v.Path[++v.PathIndex];return;}
                v.Path.Clear();v.PathIndex=0;
                if(v.Journey==0){v.Phase=VehiclePhase.Idle;v.Status="探索完成";return;}
                if(v.Journey==1){v.Phase=VehiclePhase.Gathering;v.Status="采集中";}
                if(v.Journey==2){v.Phase=VehiclePhase.Unloading;v.Status="正在卸货";}
                if(v.Journey==3){v.Phase=VehiclePhase.WaitingStock;v.Status="装载调拨物资";}
                if(v.Journey==4){v.Phase=VehiclePhase.WaitingStock;v.Status="自动装载采购物资";}
            }
            if(v.Phase==VehiclePhase.Gathering)
            {
                var r=State.Resources.First(x=>x.Id==v.Resource);
                if(++v.WorkTicks>=Math.Max(1,Settings.GatheringTicks/(r.Station?2:1)))
                {
                    v.WorkTicks=0;if(r.Stock>0&&v.Cargo.Count<Settings.VehicleCapacity){r.Stock--;v.Cargo.Add(Create(0,r.Material));State.Gathered=true;}
                }
                if(v.Cargo.Count>=Settings.VehicleCapacity||r.Stock==0&&v.Cargo.Count>0)Travel(v,Factory(v.TargetFactory).Dock,2);
                else if(r.Stock==0)v.Status="等待资源再生";
            }
            if(v.Phase==VehiclePhase.WaitingStock&&State.Tick%30==0)
            {
                if(v.PickupTask)
                {
                    foreach(var d in State.Hardware.Deliveries.Where(d=>d.Method==2&&d.Factory==v.TargetFactory).OrderBy(d=>d.Id).ToArray())
                    {
                        foreach(var g in d.Cargo.Take(Settings.VehicleCapacity-v.Cargo.Count).ToArray()){d.Cargo.Remove(g);v.Cargo.Add(g);}
                        if(d.Cargo.Count==0)State.Hardware.Deliveries.Remove(d);
                    }
                    if(v.Cargo.Count>0)Travel(v,Factory(v.TargetFactory).Dock,2);else StartTrip(v);
                    return;
                }
                foreach(var n in State.Nodes.Where(n=>n.Factory==v.SourceFactory&&n.Kind==BuildingKind.Warehouse).OrderBy(n=>n.Id))
                foreach(var item in n.Output.Where(g=>g.Material==v.Material&&g.ReservedOrder==0).Take(Settings.VehicleCapacity-v.Cargo.Count).ToArray()){n.Output.Remove(item);v.Cargo.Add(item);}
                // Manufactured goods may be loaded directly at an output dock.
                foreach(var n in State.Nodes.Where(n=>n.Factory==v.SourceFactory&&n.Kind!=BuildingKind.Warehouse).OrderBy(n=>n.Id))
                foreach(var item in n.Output.Where(g=>g.Material==v.Material&&g.ReservedOrder==0).Take(Settings.VehicleCapacity-v.Cargo.Count).ToArray()){n.Output.Remove(item);v.Cargo.Add(item);}
                if(v.Cargo.Count>0)Travel(v,Factory(v.TargetFactory).Dock,2);else v.Status="等待调拨库存";
            }
            if(v.Phase==VehiclePhase.Unloading)
            {
                foreach(var n in State.Nodes.Where(n=>n.Factory==v.TargetFactory&&n.Kind==BuildingKind.Warehouse).OrderBy(n=>n.Id))
                    while(v.Cargo.Count>0&&CanReceive(n,v.Cargo[0].Material)){if(v.Cargo[0].Material>10&&!n.Output.Any(i=>i.Material==v.Cargo[0].Material))logisticsDirty=true;n.Output.Add(v.Cargo[0]);v.Cargo.RemoveAt(0);}
                if(v.Cargo.Count>0){v.Status="等待仓库空间；可改送另一厂";return;}
                if(v.Loop)StartTrip(v);else{v.Phase=VehiclePhase.Idle;v.Status="卸货完成";}
            }
        }
    }
}
