using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain
{
    [Serializable] public sealed class PersistedBeltItem { public int Lane, Id, Material, Distance; }
    [Serializable] public sealed class BeltRuntimeState
    {
        public long Tick; public int NextId, Spawned, Sunk; public bool GateOpen;
        public List<PersistedBeltItem> Items = new List<PersistedBeltItem>();
    }
}

namespace AssemblyZero.Domain.EP04
{
    [Serializable] public struct GridPoint : IEquatable<GridPoint>
    {
        public int X, Z, H;
        public GridPoint(int x, int z, int h = 0) { X=x; Z=z; H=h; }
        public bool Equals(GridPoint p) => X==p.X && Z==p.Z && H==p.H;
        public override bool Equals(object o) => o is GridPoint p && Equals(p);
        public override int GetHashCode() => (X*397 ^ Z)*31 ^ H;
        public static int Distance(GridPoint a, GridPoint b) => Math.Abs(a.X-b.X)+Math.Abs(a.Z-b.Z)+Math.Abs(a.H-b.H);
    }
    public enum BuildingKind { Warehouse, Processor, Manufacturer, Sales }
    public enum VehicleRole { Pickup, Collector, Hauler }
    public enum VehiclePhase { Idle, Travelling, Gathering, Unloading, WaitingFunds, WaitingStock }
    public enum CommandKind { Build, Move, Remove, Upgrade, Recipe, Policy, CancelChange, Explore, Collect, Return, Transfer, HireCollector, HireHauler, Station, BuyFactory, Boost }
    [Serializable] public sealed class Goods
    {
        public int Id, Origin, Material;
        public Goods() { }
        public Goods(int id,int origin,int material) { Id=id;Origin=origin;Material=material; }
    }
    [Serializable] public sealed class ProductRule
    {
        public string Id, Name; public int Output, Seconds, Level=1, Price;
        public BuildingKind Machine;
        public RecipeInput[] Inputs;
    }
    [Serializable] public sealed class EconomySettings
    {
        public int StartingMoney=800, MachineCost=180, ProcessorCost=140, WarehouseCost=140, SalesCost=100;
        public int Level2Cost=300, Level3Cost=650, CollectorCost=450, HaulerCost=600, StationCost=250, SecondFactoryCost=22000;
        public int TripFee=8, WarehouseCapacity=300, VehicleCapacity=60, GatheringTicks=30, RegenerationTicks=30;
        public int[] UpgradePercent={100,150,200};
        public ProductRule[] Recipes = {
            new ProductRule { Id="shell", Name="塑料壳",Output=13,Seconds=2,Machine=BuildingKind.Processor,Inputs=new[]{new RecipeInput(9)} },
            new ProductRule { Id="circuit",Name="电路板",Output=16,Seconds=3,Machine=BuildingKind.Processor,Inputs=new[]{new RecipeInput(10),new RecipeInput(9)} },
            new ProductRule { Id="kit",Name="工业零件包",Output=17,Seconds=6,Price=25,Machine=BuildingKind.Manufacturer,Inputs=new[]{new RecipeInput(8,2)} },
            new ProductRule { Id="drive",Name="驱动组件",Output=18,Seconds=12,Level=2,Price=90,Machine=BuildingKind.Manufacturer,Inputs=new[]{new RecipeInput(17,2),new RecipeInput(10)} },
            new ProductRule { Id="robot",Name="小型作业机器人",Output=19,Seconds=20,Level=3,Price=220,Machine=BuildingKind.Manufacturer,Inputs=new[]{new RecipeInput(18),new RecipeInput(16),new RecipeInput(13)} }
        };
        public ProductRule Recipe(string id) => Recipes.FirstOrDefault(r=>r.Id==id);
        public int Cost(BuildingKind kind) => kind==BuildingKind.Processor?ProcessorCost:kind==BuildingKind.Manufacturer?MachineCost:kind==BuildingKind.Warehouse?WarehouseCost:SalesCost;
        public static string MaterialName(int m) => m==8?"金属":m==9?"塑料":m==10?"电子原料":m==13?"塑料壳":m==16?"电路板":m==17?"工业零件包":m==18?"驱动组件":m==19?"作业机器人":"物料";
    }
    [Serializable] public sealed class SupplyPolicy
    {
        public int Material, Supplier, Priority, Reserve;
    }
    [Serializable] public sealed class BusinessNode
    {
        public int Id, Factory, X, Z, Rotation, Level=1, Investment, Cursor;
        public BuildingKind Kind; public string RecipeId="", Status="等待", PendingRecipe="";
        public bool PendingRemove;
        public long NextSend; public int WorkRemaining, WorkTotal;
        public List<Goods> Input=new List<Goods>(), Output=new List<Goods>(), Batch=new List<Goods>();
        public List<SupplyPolicy> Policies=new List<SupplyPolicy>();
        public GridPoint Center => new GridPoint(X,Z);
        public GridPoint Port(bool output) { var d=Rotation%4;var s=output?2:-2;return new GridPoint(X+(d==0?s:d==2?-s:0),Z+(d==1?s:d==3?-s:0)); }
        public SupplyPolicy Policy(int material) => Policies.FirstOrDefault(p=>p.Material==material);
    }
    [Serializable] public sealed class BusinessRoute
    {
        public int Id, Factory, From, To, Material, Length, Speed=50;
        public List<GridPoint> Points=new List<GridPoint>();
        public List<Goods> Cargo=new List<Goods>();
        public BeltRuntimeState Saved;
        [NonSerialized] public GapTransportLineSimulation Backend;
        [NonSerialized] public BeltSnapshotWriter Snapshot=new BeltSnapshotWriter();
        public ScenarioDefinition Scenario() => new ScenarioDefinition { ExternalLogistics=true,MinimumSpacing=512,SpeedUnitsPerTick=Speed };
        public void Initialize()
        {
            Backend=new GapTransportLineSimulation();Snapshot=new BeltSnapshotWriter();
            if(Saved==null)Backend.Reset(Scenario(),BeltTopology.CreateStraight(1,Length));
            else Backend.ImportRuntimeState(Scenario(),BeltTopology.CreateStraight(1,Length),Saved);
            Backend.CreateSnapshot(Snapshot);
        }
    }
    [Serializable] public sealed class FactorySite
    {
        public int Id, X, Z; public bool Owned, Discovered, FreeWarehouse=true, FreeSales=true;
        public int SalesCount; public List<Goods> Starter=new List<Goods>();
        public GridPoint Dock => new GridPoint(X-3,Z+12);
    }
    [Serializable] public sealed class ResourceSite
    {
        public int Id, Material, X, Z, Stock=240, Capacity=240;public bool Discovered, Station;
        public GridPoint Point=>new GridPoint(X,Z);
    }
    [Serializable] public sealed class WorldVehicle
    {
        public int Id, TargetFactory=1, SourceFactory=1, Resource, Material=8, PathIndex, SegmentTicks, WorkTicks, Journey;
        public GridPoint Position; public VehicleRole Role;public VehiclePhase Phase;
        public bool Boost, Loop, TransferTask; public string Status="待命";
        public List<GridPoint> Path=new List<GridPoint>();public List<Goods> Cargo=new List<Goods>();
    }
    [Serializable] public sealed class WorldState
    {
        public int Version=1, Money, NextNode=1,NextItem=1,NextRoute=1,NextVehicle=2,Unlocked=1;
        public long Tick, Revenue, Expenses, Created, Consumed, Sold, Transactions;
        public bool Gathered, Built, Upgraded, Hired, Complete;
        public List<BusinessNode> Nodes=new List<BusinessNode>();public List<BusinessRoute> Routes=new List<BusinessRoute>();
        public List<FactorySite> Factories=new List<FactorySite>();public List<ResourceSite> Resources=new List<ResourceSite>();
        public List<WorldVehicle> Vehicles=new List<WorldVehicle>();public List<GridPoint> Explored=new List<GridPoint>();
        public List<string> Events=new List<string>();
    }
    public sealed class WorldCommand
    {
        public CommandKind Kind;public int Factory=1,Node,X,Z,Rotation,Vehicle=1,Resource,TargetFactory=1,Material=8,Supplier,Priority,Reserve;
        public BuildingKind Building;public string Recipe="";public bool Flag;
    }
    public readonly struct CommandResult
    {
        public readonly bool Success;public readonly string Message;public readonly int Entity;
        public CommandResult(bool success,string message,int entity=0){Success=success;Message=message;Entity=entity;}
    }
}
