using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain.EP04;

namespace AssemblyZero.Unity.EP04
{
    // Stable user-owned slots. Catalog growth never reorders a valid saved shortcut.
    public sealed class HardwareBuildShortcuts
    {
        public const int SlotCount=7;
        private readonly HashSet<BuildingKind> available;
        private readonly int[] slots=Enumerable.Repeat(-1,SlotCount).ToArray();
        private static readonly BuildingKind[] Defaults={BuildingKind.Warehouse,BuildingKind.Processor,BuildingKind.Manufacturer,
            BuildingKind.BoardAssembly,BuildingKind.GPUAssembly,BuildingKind.Tester,BuildingKind.Packaging};

        public HardwareBuildShortcuts(IEnumerable<HardwareBuilding> buildings,IEnumerable<int> saved=null)
        {
            var catalog=buildings.Select(b=>b.Kind).Distinct().ToArray();available=new HashSet<BuildingKind>(catalog);
            var used=new HashSet<int>();var values=saved?.Take(SlotCount).ToArray()??Array.Empty<int>();
            for(var i=0;i<values.Length;i++)
                if(available.Contains((BuildingKind)values[i])&&used.Add(values[i]))slots[i]=values[i];
            var defaults=Defaults.Concat(catalog).Distinct().Where(k=>available.Contains(k)&&!used.Contains((int)k)).ToArray();
            var cursor=0;
            for(var i=0;i<slots.Length&&cursor<defaults.Length;i++)if(slots[i]<0)slots[i]=(int)defaults[cursor++];
        }

        public BuildingKind? At(int index)=>index>=0&&index<SlotCount&&slots[index]>=0?(BuildingKind?)slots[index]:null;
        public int IndexOf(BuildingKind kind)=>Array.IndexOf(slots,(int)kind);
        public int[] Export()=>(int[])slots.Clone();

        public void Assign(int index,BuildingKind kind)
        {
            if(index<0||index>=SlotCount)throw new ArgumentOutOfRangeException(nameof(index));
            if(!available.Contains(kind))throw new ArgumentException("Device is not in the current catalog",nameof(kind));
            var old=IndexOf(kind);if(old==index)return;
            if(old>=0)slots[old]=slots[index];
            slots[index]=(int)kind;
        }
    }
}
