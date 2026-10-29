using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    [Serializable] public sealed class MaterialDefinition { public int Id, Price, Stock=80; public string Name; public bool Finished; }
    [Serializable] public sealed class HardwareBuilding { public BuildingKind Kind; public string Name, Recipe; public int Cost; }
    [Serializable] public sealed class OrderTemplate { public int Id, Material, Quantity, UnitPrice, Seconds; public string Name, Spec; public bool Repeatable; }
    [Serializable] public sealed class SuggestedSlot { public BuildingKind Kind; public int X, Z, Rotation; }
    // Generated customers; percentages are relative to the product base price.
    [Serializable] public sealed class ClientType { public string Name; public int Material, MinQuantity, MaxQuantity, PricePercent=100, SecondsPerUnit=24, MinReputation, Weight=1; }
    // Percent fields default to 100 (no change). Client indices refer to HardwareDefinition.Clients.
    [Serializable] public sealed class MarketEventDefinition
    {
        public string Id, Name, Detail;
        public int CorePercent=100, MemoryPercent=100, StandardPricePercent=100, QuietPricePercent=100, StandardQuantityPercent=100, SpotPercent=100, CoreSupplyPercent=100, FlawPermille, BoostClient=-1, BlockClient=-1;
    }
    [Serializable] public sealed class HardwareDefinition
    {
        public string Id="az-gpu"; public int Version=HardwareCatalog.ContentVersion, RestockSeconds=60, DeliverySeconds=30, ExpressSeconds=8, DeliveryFee=1200, ExpressFee=4800, PickupFee=300;
        public int SupplierX=36,SupplierZ=64, OreX=12,OreZ=72,LayoutVersion=1, SpotCapacity=20, RescueSeconds=60;
        public int StartingReputation=10, CalmSeconds=240, EventSeconds=180, ForecastSeconds=60, OfferRefreshSeconds=90, OfferLifetimeSeconds=240, BoardSize=4, MaxActiveOrders=3;
        public MaterialDefinition[] Materials; public HardwareBuilding[] Buildings; public OrderTemplate[] Orders;
        public SuggestedSlot[] Slots; public ClientType[] Clients; public MarketEventDefinition[] Events;
    }
    public enum HardwareOrderPhase { Available, Active, Ready, Delivered, Expired, Cancelled }
    public enum DeliveryGrade { Early, OnTime, Late }
    [Serializable] public sealed class HardwareOrder
    {
        public int Id,Template,Factory=1,Material,Quantity,UnitPrice,Version=1; public string Name,Spec; public long Deadline; public HardwareOrderPhase Phase;
        // Client is -1 for scripted contracts. Seconds is the promised lead time; the three deadlines are set on acceptance.
        public int Client=-1, Seconds, MinReputation; public long BonusDeadline, HardDeadline, OfferExpires;
    }
    [Serializable] public sealed class SupplierStock { public int Material,Quantity; }
    [Serializable] public sealed class PurchaseDelivery { public int Id,Factory,Method; public long DueTick; public List<Goods> Cargo=new List<Goods>(); }
    [Serializable] public sealed class CashEntry { public long Tick,Amount; public string Category; }
    [Serializable] public sealed class Settlement
    {
        public int Order,Quantity; public long Tick,Income,MaterialCost,ProcessingCost,FreightCost; public long Margin=>Income-MaterialCost-ProcessingCost-FreightCost;
        public DeliveryGrade Grade; public int ReputationDelta; public long Adjustment; public string Name;
    }
    // Cumulative snapshot taken once per simulated minute; charts plot differences between buckets.
    [Serializable] public sealed class StatsBucket { public long Tick,Revenue,Expenses; public int Penalties,Cards,CorePricePercent,Reputation; }
    [Serializable] public sealed class MarketState { public int Current=-1, Next=-1; public long PhaseEnds; }
    [Serializable] public sealed class HardwareState
    {
        public string ContentId="az-gpu"; public int ContentVersion=HardwareCatalog.ContentVersion,LayoutVersion,NextOrder=4,NextPurchase=1,SpotRemaining=20;
        public List<HardwareOrder> Orders=new List<HardwareOrder>(); public List<SupplierStock> Stock=new List<SupplierStock>();
        public List<PurchaseDelivery> Deliveries=new List<PurchaseDelivery>(); public List<Settlement> Settlements=new List<Settlement>();
        public List<CashEntry> Cash=new List<CashEntry>();
        public List<StatsBucket> Stats=new List<StatsBucket>();
        public long FirstProductTick, NextOfferRefresh;
        public int Rng=0x5EED1234, Reputation, Penalties, ScriptedDone;
        public MarketState Market=new MarketState();
    }
    public static class HardwareCatalog
    {
        public const int ContentVersion=2;
        // The shared belt protocol uses a 64-bit part mask. Keep legacy 0..19 intact.
        public const int Ore=32,Aluminum=33,Core=34,Memory=35,Board=36,Shell=37,Carton=38,Heat=39,QuietHeat=40,AssembledBoard=41,RawGPU=42,QuietRaw=43,Tested=44,QuietTested=45,Standard=46,Quiet=47,Defect=48,Budget=49;
        public const string StandardSpec="s1-standard",QuietSpec="s1-quiet",BudgetSpec="s1-budget";
        public static string Name(int id) { var i=id-32; return i>=0&&i<Names.Length?Names[i]:"未知物料"; }
        private static readonly string[] Names={"铝原料","铝材","GPU 核心","显存包","板材组件","显卡外壳","包装材料","标准散热器","低噪散热器","已装配板卡","待检标准显卡","待检静音显卡","合格标准显卡","合格静音显卡","星铸 S1 标准版","星铸 S1 静音版","次品显卡","星铸 S1 丐版"};
        public static string Spec(int material)=>material==Quiet?QuietSpec:material==Budget?BudgetSpec:StandardSpec;
        public static string ReputationTier(int reputation)=>reputation>=70?"一线大厂":reputation>=45?"知名品牌":reputation>=20?"可靠供应商":"新厂";
        public static EconomySettings Create()
        {
            var h=new HardwareDefinition {
                Materials=Names.Select((n,i)=>new MaterialDefinition{Id=32+i,Name=n,Price=new[]{0,1800,18000,6000,3000,1000,200,6000,10000,0,0,0,0,0,0,0,0,0}[i],Finished=i>=14}).ToArray(),
                Buildings=new[]{B(BuildingKind.Warehouse,"仓储装卸", "",14000),B(BuildingKind.Processor,"铝材加工","aluminum",14000),B(BuildingKind.Manufacturer,"散热器加工","heat",18000),B(BuildingKind.BoardAssembly,"板卡装配","board",20000),B(BuildingKind.GPUAssembly,"显卡总装","gpu",22000),B(BuildingKind.Tester,"检测","test",18000),B(BuildingKind.Packaging,"包装","pack",12000),B(BuildingKind.Sales,"交付与回收","",10000),B(BuildingKind.Rework,"返修台","rework",16000)},
                Orders=new[]{
                    new OrderTemplate{Id=1,Name="晨湾工作室 · 首批 3 张标准版",Material=Standard,Spec=StandardSpec,Quantity=3,UnitPrice=48000,Seconds=300},
                    new OrderTemplate{Id=2,Name="星河网吧 · 8 张标准版装机",Material=Standard,Spec=StandardSpec,Quantity=8,UnitPrice=48000,Seconds=480}},
                Slots=new[]{new SuggestedSlot{Kind=BuildingKind.GPUAssembly,X=20,Z=8},new SuggestedSlot{Kind=BuildingKind.Tester,X=28,Z=8},new SuggestedSlot{Kind=BuildingKind.Packaging,X=34,Z=16}},
                Clients=new[]{
                    new ClientType{Name="星河网吧",Material=Standard,MinQuantity=6,MaxQuantity=12,PricePercent=100,SecondsPerUnit=26,Weight=3},
                    new ClientType{Name="独立游戏工作室",Material=Standard,MinQuantity=3,MaxQuantity=6,PricePercent=106,SecondsPerUnit=30,Weight=3},
                    new ClientType{Name="街角维修社",Material=Standard,MinQuantity=1,MaxQuantity=2,PricePercent=92,SecondsPerUnit=60,Weight=2},
                    new ClientType{Name="静庭设计",Material=Quiet,MinQuantity=4,MaxQuantity=8,PricePercent=100,SecondsPerUnit=30,MinReputation=20,Weight=2},
                    new ClientType{Name="北岭矿场",Material=Standard,MinQuantity=12,MaxQuantity=24,PricePercent=94,SecondsPerUnit=20,MinReputation=30,Weight=1},
                    new ClientType{Name="深脉 AI 实验室",Material=Quiet,MinQuantity=8,MaxQuantity=16,PricePercent=115,SecondsPerUnit=24,MinReputation=45,Weight=1},
                    new ClientType{Name="拼拼电商",Material=Budget,MinQuantity=3,MaxQuantity=8,PricePercent=100,SecondsPerUnit=40,MinReputation=10,Weight=1}},
                Events=new[]{
                    new MarketEventDefinition{Id="mining",Name="挖矿潮",Detail="标准版订单量 ×2.5、单价 +30%；GPU 核心采购价 +50%",CorePercent=150,StandardPricePercent=130,StandardQuantityPercent=250,BoostClient=4},
                    new MarketEventDefinition{Id="ai",Name="AI 热潮",Detail="显存包 +40%；静音/大显存订单溢价 +40%",MemoryPercent=140,QuietPricePercent=140,BoostClient=5},
                    new MarketEventDefinition{Id="crash",Name="矿难",Detail="二手卡冲击：现货/翻新价 -40%，标准版单价 -15%，矿场停单",StandardPricePercent=85,SpotPercent=60,BlockClient=4},
                    new MarketEventDefinition{Id="shortage",Name="芯片紧缺",Detail="核心供应减半、采购价 +25%；次品率 +4%",CorePercent=125,CoreSupplyPercent=50,FlawPermille=40}}
            };
            return new EconomySettings{Hardware=h,StartingMoney=300000,Level2Cost=30000,Level3Cost=65000,CollectorCost=45000,HaulerCost=60000,StationCost=25000,SecondFactoryCost=2200000,TripFee=800,VehicleCapacity=24,
                Recipes=new[]{
                    R("aluminum",Aluminum,5,BuildingKind.Processor,100,"",new RecipeInput(Ore,2)),
                    R("heat",Heat,8,BuildingKind.Manufacturer,200,"",new RecipeInput(Aluminum,2)),
                    R("quiet-heat",QuietHeat,12,BuildingKind.Manufacturer,300,"",new RecipeInput(Aluminum,3)),
                    R("board",AssembledBoard,16,BuildingKind.BoardAssembly,300,"",new RecipeInput(Board),new RecipeInput(Core),new RecipeInput(Memory)),
                    R("gpu",RawGPU,12,BuildingKind.GPUAssembly,200,StandardSpec,new RecipeInput(AssembledBoard),new RecipeInput(Heat),new RecipeInput(Shell)).Flaw(100),
                    R("quiet-gpu",QuietRaw,14,BuildingKind.GPUAssembly,250,QuietSpec,new RecipeInput(AssembledBoard),new RecipeInput(QuietHeat),new RecipeInput(Shell)).Flaw(100),
                    R("test",Tested,10,BuildingKind.Tester,100,StandardSpec,new RecipeInput(RawGPU)).With(Defect),
                    R("quiet-test",QuietTested,12,BuildingKind.Tester,150,QuietSpec,new RecipeInput(QuietRaw)).With(Defect),
                    R("pack",Standard,6,BuildingKind.Packaging,50,StandardSpec,new RecipeInput(Tested),new RecipeInput(Carton)),
                    R("quiet-pack",Quiet,6,BuildingKind.Packaging,50,QuietSpec,new RecipeInput(QuietTested),new RecipeInput(Carton)),
                    R("budget-pack",Budget,6,BuildingKind.Packaging,50,BudgetSpec,new RecipeInput(Defect),new RecipeInput(Carton)),
                    R("rework",RawGPU,14,BuildingKind.Rework,250,"",new RecipeInput(Defect),new RecipeInput(Memory)).With(QuietRaw)}};
        }
        private static HardwareBuilding B(BuildingKind k,string name,string recipe,int cost)=>new HardwareBuilding{Kind=k,Name=name,Recipe=recipe,Cost=cost};
        private static ProductRule R(string id,int output,int seconds,BuildingKind kind,int fee,string spec,params RecipeInput[] input)=>new ProductRule{Id=id,Name=Name(output),Output=output,Seconds=seconds,Machine=kind,Inputs=input,ProcessingFee=fee,SpecId=spec};
        private static ProductRule With(this ProductRule r,int byproduct){r.Byproduct=byproduct;return r;}
        private static ProductRule Flaw(this ProductRule r,int permille){r.FlawPermille=permille;return r;}
    }
}
