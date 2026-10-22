using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    [Serializable] public sealed class MaterialDefinition { public int Id, Price, Stock=80; public string Name; public bool Finished; }
    [Serializable] public sealed class HardwareBuilding { public BuildingKind Kind; public string Name, Recipe; public int Cost; }
    [Serializable] public sealed class OrderTemplate { public int Id, Material, Quantity, UnitPrice, Seconds; public string Name, Spec; public bool Repeatable; }
    [Serializable] public sealed class HardwareDefinition
    {
        public string Id="az-gpu"; public int Version=1, RestockSeconds=120, DeliverySeconds=90, ExpressSeconds=25, DeliveryFee=1200, ExpressFee=4800, PickupFee=300;
        public int SupplierX=1,SupplierZ=37, SpotCapacity=20, RescueSeconds=60;
        public MaterialDefinition[] Materials; public HardwareBuilding[] Buildings; public OrderTemplate[] Orders;
    }
    public enum HardwareOrderPhase { Available, Active, Ready, Delivered, Expired, Cancelled }
    [Serializable] public sealed class HardwareOrder { public int Id,Template,Factory=1,Material,Quantity,UnitPrice,Version=1; public string Name,Spec; public long Deadline; public HardwareOrderPhase Phase; }
    [Serializable] public sealed class SupplierStock { public int Material,Quantity; }
    [Serializable] public sealed class PurchaseDelivery { public int Id,Factory,Method; public long DueTick; public List<Goods> Cargo=new List<Goods>(); }
    [Serializable] public sealed class CashEntry { public long Tick,Amount; public string Category; }
    [Serializable] public sealed class Settlement { public int Order,Quantity; public long Tick,Income,MaterialCost,ProcessingCost,FreightCost; public long Margin=>Income-MaterialCost-ProcessingCost-FreightCost; }
    [Serializable] public sealed class HardwareState
    {
        public string ContentId="az-gpu"; public int ContentVersion=1,NextOrder=4,NextPurchase=1,SpotRemaining=20;
        public List<HardwareOrder> Orders=new List<HardwareOrder>(); public List<SupplierStock> Stock=new List<SupplierStock>();
        public List<PurchaseDelivery> Deliveries=new List<PurchaseDelivery>(); public List<Settlement> Settlements=new List<Settlement>();
        public List<CashEntry> Cash=new List<CashEntry>();
        public long FirstProductTick;
    }
    public static class HardwareCatalog
    {
        // The shared belt protocol uses a 64-bit part mask. Keep legacy 0..19 intact.
        public const int Ore=32,Aluminum=33,Core=34,Memory=35,Board=36,Shell=37,Carton=38,Heat=39,QuietHeat=40,AssembledBoard=41,RawGPU=42,QuietRaw=43,Tested=44,QuietTested=45,Standard=46,Quiet=47;
        public static string Name(int id) { var i=id-32; return i>=0&&i<Names.Length?Names[i]:"未知物料"; }
        private static readonly string[] Names={"铝原料","铝材","GPU 核心","显存包","板材组件","显卡外壳","包装材料","标准散热器","低噪散热器","已装配板卡","待检标准显卡","待检静音显卡","合格标准显卡","合格静音显卡","星铸 S1 标准版","星铸 S1 静音版"};
        public static EconomySettings Create()
        {
            var h=new HardwareDefinition {
                Materials=Names.Select((n,i)=>new MaterialDefinition{Id=32+i,Name=n,Price=new[]{0,1800,18000,6000,3000,1000,200,6000,10000,0,0,0,0,0,0,0}[i],Finished=i>=14}).ToArray(),
                Buildings=new[]{B(BuildingKind.Warehouse,"仓储装卸", "",14000),B(BuildingKind.Processor,"铝材加工","aluminum",14000),B(BuildingKind.Manufacturer,"散热器加工","heat",18000),B(BuildingKind.BoardAssembly,"板卡装配","board",20000),B(BuildingKind.GPUAssembly,"显卡总装","gpu",22000),B(BuildingKind.Tester,"检测","test",18000),B(BuildingKind.Packaging,"包装","pack",12000),B(BuildingKind.Sales,"交付与回收","",10000)},
                Orders=new[]{new OrderTemplate{Id=1,Name="晨湾工作室 · 首批标准显卡",Material=Standard,Spec="s1-standard",Quantity=8,UnitPrice=48000,Seconds=1080},new OrderTemplate{Id=2,Name="静庭设计 · 低噪工作站",Material=Quiet,Spec="s1-quiet",Quantity=6,UnitPrice=56000,Seconds=900},new OrderTemplate{Id=3,Name="本地维修社 · 小额补货",Material=Standard,Spec="s1-standard",Quantity=1,UnitPrice=44000,Seconds=900,Repeatable=true}}
            };
            return new EconomySettings{Hardware=h,StartingMoney=300000,Level2Cost=30000,Level3Cost=65000,CollectorCost=45000,HaulerCost=60000,StationCost=25000,SecondFactoryCost=2200000,TripFee=800,VehicleCapacity=24,
                Recipes=new[]{
                    R("aluminum",Aluminum,20,BuildingKind.Processor,100,"",new RecipeInput(Ore,2)),
                    R("heat",Heat,35,BuildingKind.Manufacturer,200,"",new RecipeInput(Aluminum,2)),
                    R("quiet-heat",QuietHeat,55,BuildingKind.Manufacturer,300,"",new RecipeInput(Aluminum,3)),
                    R("board",AssembledBoard,75,BuildingKind.BoardAssembly,300,"",new RecipeInput(Board),new RecipeInput(Core),new RecipeInput(Memory)),
                    R("gpu",RawGPU,60,BuildingKind.GPUAssembly,200,"s1-standard",new RecipeInput(AssembledBoard),new RecipeInput(Heat),new RecipeInput(Shell)),
                    R("quiet-gpu",QuietRaw,70,BuildingKind.GPUAssembly,250,"s1-quiet",new RecipeInput(AssembledBoard),new RecipeInput(QuietHeat),new RecipeInput(Shell)),
                    R("test",Tested,55,BuildingKind.Tester,100,"s1-standard",new RecipeInput(RawGPU)),
                    R("quiet-test",QuietTested,65,BuildingKind.Tester,150,"s1-quiet",new RecipeInput(QuietRaw)),
                    R("pack",Standard,30,BuildingKind.Packaging,50,"s1-standard",new RecipeInput(Tested),new RecipeInput(Carton)),
                    R("quiet-pack",Quiet,30,BuildingKind.Packaging,50,"s1-quiet",new RecipeInput(QuietTested),new RecipeInput(Carton))}};
        }
        private static HardwareBuilding B(BuildingKind k,string name,string recipe,int cost)=>new HardwareBuilding{Kind=k,Name=name,Recipe=recipe,Cost=cost};
        private static ProductRule R(string id,int output,int seconds,BuildingKind kind,int fee,string spec,params RecipeInput[] input)=>new ProductRule{Id=id,Name=Name(output),Output=output,Seconds=seconds,Machine=kind,Inputs=input,ProcessingFee=fee,SpecId=spec};
    }
}
