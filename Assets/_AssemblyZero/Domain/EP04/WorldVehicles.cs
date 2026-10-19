using System;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        private CommandResult VehicleCommand(WorldCommand c)
        {
            var v=State.Vehicles.FirstOrDefault(x=>x.Id==c.Vehicle);
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
                var destination=new GridPoint(c.X,c.Z);var path=GridRouting.WorldPath(v.Position,destination);if(path==null)return Fail("目标不可达，请选择道路或空地");
                v.Loop=false;v.TransferTask=false;SetJourney(v,path,0);return Ok("皮卡正在探索");
            }
            if(c.Kind==CommandKind.Return)
            {
                if(Factory(c.TargetFactory)?.Owned!=true)return Fail("目标工厂未购买");
                v.TargetFactory=c.TargetFactory;v.Loop=false;
                if(!Travel(v,Factory(c.TargetFactory).Dock,2))return Fail("返厂路径不可达");return Ok("正在返厂卸货");
            }
            if(c.Kind==CommandKind.Collect)
            {
                var resource=State.Resources.FirstOrDefault(r=>r.Id==c.Resource&&r.Discovered);
                if(resource==null||Factory(c.TargetFactory)?.Owned!=true)return Fail("请选择已发现资源和有效目标工厂");
                if(v.Cargo.Count>0)return Fail("请先将车上货物卸入仓库");
                if(GridRouting.WorldPath(v.Position,resource.Point)==null)return Fail("资源点不可达");
                v.Resource=c.Resource;v.TargetFactory=c.TargetFactory;v.TransferTask=false;v.Loop=v.Role==VehicleRole.Collector;StartTrip(v);return Ok("采集任务已下达");
            }
            if(c.Kind==CommandKind.Transfer)
            {
                if(Factory(c.Factory)?.Owned!=true||Factory(c.TargetFactory)?.Owned!=true||c.Factory==c.TargetFactory||!ValidMaterial(c.Material))return Fail("请选择两个不同的已购工厂及物料");
                if(v.Cargo.Count>0)return Fail("请先卸下当前货物");
                v.SourceFactory=c.Factory;v.TargetFactory=c.TargetFactory;v.Material=c.Material;v.TransferTask=true;v.Loop=v.Role==VehicleRole.Hauler;StartTrip(v);return Ok("调拨任务已下达");
            }
            return Fail("未知车辆命令");
        }
        private static bool ValidMaterial(int material)=>new[]{8,9,10,13,16,17,18,19}.Contains(material);
        private void SetJourney(WorldVehicle v,System.Collections.Generic.List<GridPoint> path,int journey)
        {
            v.Path=path;v.PathIndex=0;v.SegmentTicks=0;v.Journey=journey;v.Phase=VehiclePhase.Travelling;v.Status=journey==0?"探索中":journey==1?"前往资源点":journey==2?"返厂运输":"前往调拨仓库";
        }
        private bool Travel(WorldVehicle v,GridPoint end,int journey)
        {
            var path=GridRouting.WorldPath(v.Position,end);if(path==null){v.Phase=VehiclePhase.Idle;v.Status="道路不可达";return false;}SetJourney(v,path,journey);return true;
        }
        private void StartTrip(WorldVehicle v)
        {
            var end=v.TransferTask?Factory(v.SourceFactory).Dock:State.Resources.First(r=>r.Id==v.Resource).Point;
            var path=GridRouting.WorldPath(v.Position,end);if(path==null){v.Phase=VehiclePhase.Idle;v.Status="道路不可达";return;}
            if(v.Role!=VehicleRole.Pickup&&!Pay(Settings.TripFee)){v.Phase=VehiclePhase.WaitingFunds;v.Status="资金不足，暂停派遣";return;}
            SetJourney(v,path,v.TransferTask?3:1);
        }
        private void Discover(GridPoint position)
        {
            var cell=new GridPoint(position.X/8,position.Z/8);if(!State.Explored.Contains(cell)){State.Explored.Add(cell);Revision++;}
            foreach(var r in State.Resources)if(!r.Discovered&&GridPoint.Distance(position,r.Point)<=9){r.Discovered=true;Revision++;Log("发现 "+EconomySettings.MaterialName(r.Material)+"资源点");}
            foreach(var f in State.Factories)if(!f.Discovered&&GridPoint.Distance(position,f.Dock)<=12){f.Discovered=true;Revision++;Log("发现第二厂址");}
        }
        private void TickVehicle(WorldVehicle v)
        {
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
                foreach(var n in State.Nodes.Where(n=>n.Factory==v.SourceFactory&&n.Kind==BuildingKind.Warehouse).OrderBy(n=>n.Id))
                foreach(var item in n.Output.Where(g=>g.Material==v.Material).Take(Settings.VehicleCapacity-v.Cargo.Count).ToArray()){n.Output.Remove(item);v.Cargo.Add(item);}
                // Manufactured goods may be loaded directly at an output dock.
                foreach(var n in State.Nodes.Where(n=>n.Factory==v.SourceFactory&&n.Kind!=BuildingKind.Warehouse).OrderBy(n=>n.Id))
                foreach(var item in n.Output.Where(g=>g.Material==v.Material).Take(Settings.VehicleCapacity-v.Cargo.Count).ToArray()){n.Output.Remove(item);v.Cargo.Add(item);}
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
