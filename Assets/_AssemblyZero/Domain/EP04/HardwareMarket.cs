using System;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        public const int StandardBasePrice=48000, QuietBasePrice=56000, BudgetBasePrice=26000;
        // Deterministic xorshift stored in the save; every random business decision draws from it.
        private int NextRandom(int max)
        {
            var x=unchecked((uint)State.Hardware.Rng);if(x==0)x=0x5EED1234;
            x^=x<<13;x^=x>>17;x^=x<<5;State.Hardware.Rng=unchecked((int)x);
            return max<=1?0:(int)(x%(uint)max);
        }
        private bool Roll(int permille)=>permille>0&&NextRandom(1000)<permille;
        public MarketEventDefinition CurrentEvent=>Settings.IsHardware&&State.Hardware.Market.Current>=0?Settings.Hardware.Events[State.Hardware.Market.Current]:null;
        public MarketEventDefinition ForecastEvent
        {
            get
            {
                var m=State.Hardware?.Market;if(m==null||m.Current>=0||m.Next<0||m.PhaseEnds==0)return null;
                return m.PhaseEnds-State.Tick<=Settings.Hardware.ForecastSeconds*60L?Settings.Hardware.Events[m.Next]:null;
            }
        }
        public long MarketSecondsLeft=>State.Hardware?.Market.PhaseEnds>0?Math.Max(0,(State.Hardware.Market.PhaseEnds-State.Tick)/60):0;
        // The market starts after the first contract so the opening is never disrupted.
        private void TickMarket()
        {
            var m=State.Hardware.Market;var events=Settings.Hardware.Events;
            if(events==null||events.Length==0||State.Hardware.Settlements.Count==0)return;
            if(m.PhaseEnds==0){m.Current=-1;m.Next=NextRandom(events.Length);m.PhaseEnds=State.Tick+Settings.Hardware.CalmSeconds*60L;return;}
            if(State.Tick<m.PhaseEnds)return;
            if(m.Current<0)
            {
                m.Current=m.Next;m.PhaseEnds=State.Tick+Settings.Hardware.EventSeconds*60L;
                State.Hardware.NextOfferRefresh=State.Tick;Log("市场事件：「"+events[m.Current].Name+"」"+events[m.Current].Detail);
            }
            else
            {
                var previous=m.Current;m.Current=-1;m.Next=(previous+1+NextRandom(events.Length-1))%events.Length;
                m.PhaseEnds=State.Tick+Settings.Hardware.CalmSeconds*60L;State.Hardware.NextOfferRefresh=State.Tick;Log("市场回归平稳；留意下一次行情预告");
            }
        }
        public int PurchasePrice(int material)
        {
            var m=Settings.Hardware.Materials.FirstOrDefault(x=>x.Id==material);if(m==null)return 0;var e=CurrentEvent;
            var percent=e==null?100:material==HardwareCatalog.Core?e.CorePercent:material==HardwareCatalog.Memory?e.MemoryPercent:100;
            return m.Price*percent/100;
        }
        public int OrderPricePercent(int material)
        {
            var e=CurrentEvent;if(e==null)return 100;
            return material==HardwareCatalog.Standard?e.StandardPricePercent:material==HardwareCatalog.Quiet?e.QuietPricePercent:100;
        }
        public int SpotPrice(int material)
        {
            if(material==HardwareCatalog.Ore)return 100;
            var basePrice=material==HardwareCatalog.Standard?35000:material==HardwareCatalog.Quiet?40000:material==HardwareCatalog.Budget?22000:material==HardwareCatalog.Defect?9000:0;
            return basePrice*(CurrentEvent?.SpotPercent??100)/100;
        }
    }
}
