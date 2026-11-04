using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        public const int RepairClient=2, PreviewReputation=10;
        public bool OfferBoardOpen=>Settings.IsHardware&&State.Hardware.Settlements.Count>0;
        public IEnumerable<HardwareOrder> Offers=>State.Hardware.Orders.Where(o=>o.Phase==HardwareOrderPhase.Available);
        // Keeps a rotating board of client offers; a small repair job always exists as a cash fallback.
        private void TickOffers()
        {
            var h=State.Hardware;var clients=Settings.Hardware.Clients;
            if(!OfferBoardOpen||clients==null||clients.Length==0||State.Tick<h.NextOfferRefresh)return;
            h.Orders.RemoveAll(o=>o.Phase==HardwareOrderPhase.Available&&o.Template==0&&o.OfferExpires<=State.Tick);
            var board=h.Orders.Where(o=>o.Phase==HardwareOrderPhase.Available&&o.Template==0).ToList();
            if(!board.Any(o=>o.Client==RepairClient)&&RepairClient<clients.Length){var repair=Generate(RepairClient);h.Orders.Add(repair);board.Add(repair);}
            for(var guard=0;board.Count<Settings.Hardware.BoardSize&&guard<20;guard++)
            {
                var client=PickClient();if(client<0)break;var offer=Generate(client);h.Orders.Add(offer);board.Add(offer);
            }
            h.NextOfferRefresh=State.Tick+Settings.Hardware.OfferRefreshSeconds*60L;
        }
        private int PickClient()
        {
            var clients=Settings.Hardware.Clients;var e=CurrentEvent;var weights=new int[clients.Length];var total=0;
            for(var i=0;i<clients.Length;i++)
            {
                // Slightly-out-of-reach offers stay visible as goals but cannot be accepted yet.
                if(clients[i].MinReputation>State.Hardware.Reputation+PreviewReputation||e!=null&&e.BlockClient==i)continue;
                if(clients[i].Material==HardwareCatalog.Standard&&!State.Hardware.Skus.Any(s=>FitsClient(s,clients[i])))continue;
                weights[i]=clients[i].Weight*(e!=null&&e.BoostClient==i?4:1);total+=weights[i];
            }
            if(total==0)return -1;var roll=NextRandom(total);
            for(var i=0;i<weights.Length;i++){if(roll<weights[i])return i;roll-=weights[i];}
            return -1;
        }
        private HardwareOrder Generate(int index)
        {
            var c=Settings.Hardware.Clients[index];var e=CurrentEvent;var spec=PickSku(c)??(string.IsNullOrEmpty(c.Spec)?HardwareCatalog.Spec(c.Material):c.Spec);
            var quantity=c.MinQuantity+NextRandom(Math.Max(1,c.MaxQuantity-c.MinQuantity+1));
            if(c.Material==HardwareCatalog.Standard&&!IsQuiet(spec)&&e!=null)quantity=Math.Max(1,quantity*e.StandardQuantityPercent/100);
            var gen=Math.Max(State.Hardware.CoreGen,State.Hardware.IndustryGen);var minGen=c.Gen==2?gen:c.Gen==1?Math.Max(1,gen-1):0;
            // A quote must be fulfillable by the chosen model; do not silently turn an old SKU into a new one.
            if(c.Material==HardwareCatalog.Standard)minGen=Math.Min(minGen,Sku(spec)?.Gen??1);
            var unit=(int)Math.Min(10000000,(long)BasePrice(spec)*c.PricePercent/100*OrderPricePercent(spec)/100*RequirementPercent(c.MinGrade)/100*SkuPricePercent(spec,Math.Max(1,minGen))/100/100*100);
            return new HardwareOrder{Id=State.Hardware.NextOrder++,Template=0,Client=index,Material=c.Material,Spec=spec,Version=Sku(spec)?.Version??1,Quantity=quantity,UnitPrice=unit,MinGrade=c.MinGrade,MinGen=minGen,
                Seconds=90+quantity*c.SecondsPerUnit,MinReputation=c.MinReputation,OfferExpires=State.Tick+Settings.Hardware.OfferLifetimeSeconds*60L,
                Name=c.Name+" · "+quantity+" 张"+ProductName(spec)+(c.MinGrade>0?"（"+GradeRequirement(c.MinGrade)+"）":"")+(minGen>1?" · 第"+minGen+"代起":"")};
        }
    }
}
