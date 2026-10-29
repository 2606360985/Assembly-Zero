using System;
using System.Linq;
using AssemblyZero.Domain.EP04;
using AssemblyZero.Unity.EP04;
using NUnit.Framework;

namespace AssemblyZero.Tests
{
    public sealed class HardwareBuildShortcutTests
    {
        private static HardwareBuilding[] Catalog()=>HardwareCatalog.Create().Hardware.Buildings;

        [Test] public void DefaultsHaveSevenDistinctFrequentlyUsedDevices()
        {
            var layout=new HardwareBuildShortcuts(Catalog());
            CollectionAssert.AreEqual(new[]{BuildingKind.Warehouse,BuildingKind.Processor,BuildingKind.Manufacturer,
                BuildingKind.BoardAssembly,BuildingKind.GPUAssembly,BuildingKind.Tester,BuildingKind.Packaging},layout.Export().Select(i=>(BuildingKind)i));
            Assert.IsNull(layout.At(-1));Assert.IsNull(layout.At(7));
        }

        [Test] public void ReplacingAndSwappingKeepsEveryOtherSlotStable()
        {
            var layout=new HardwareBuildShortcuts(Catalog());var before=layout.Export();
            layout.Assign(6,BuildingKind.Rework);Assert.AreEqual(BuildingKind.Rework,layout.At(6));Assert.AreEqual(-1,layout.IndexOf(BuildingKind.Packaging));
            CollectionAssert.AreEqual(before.Take(6),layout.Export().Take(6));
            layout.Assign(6,BuildingKind.Warehouse);Assert.AreEqual(BuildingKind.Rework,layout.At(0));Assert.AreEqual(BuildingKind.Warehouse,layout.At(6));
            Assert.AreEqual(7,layout.Export().Distinct().Count());
            var export=layout.Export();export[1]=12345;Assert.AreEqual(BuildingKind.Processor,layout.At(1));
        }

        [Test] public void CatalogGrowthAndReorderingNeverReorderSavedSlots()
        {
            var layout=new HardwareBuildShortcuts(Catalog());layout.Assign(2,BuildingKind.Sales);layout.Assign(5,BuildingKind.Rework);
            var saved=layout.Export();var extended=Catalog().Reverse().Concat(new[]{new HardwareBuilding{Kind=(BuildingKind)100,Name="新设备"}});
            CollectionAssert.AreEqual(saved,new HardwareBuildShortcuts(extended,saved).Export());
        }

        [Test] public void InvalidAndDuplicateSavedEntriesAreRepairedInPlace()
        {
            var layout=new HardwareBuildShortcuts(Catalog(),new[]{-1,999,(int)BuildingKind.Warehouse,(int)BuildingKind.Warehouse,(int)BuildingKind.Rework});
            Assert.AreEqual(BuildingKind.Warehouse,layout.At(2));Assert.AreEqual(BuildingKind.Rework,layout.At(4));
            Assert.AreEqual(7,layout.Export().Distinct().Count());Assert.IsTrue(layout.Export().All(i=>Catalog().Any(b=>(int)b.Kind==i)));
        }

        [Test] public void RemovingACatalogItemOnlyRepairsItsOwnSlot()
        {
            var saved=new HardwareBuildShortcuts(Catalog()).Export();
            var layout=new HardwareBuildShortcuts(Catalog().Where(b=>b.Kind!=BuildingKind.Processor),saved);
            for(var i=0;i<saved.Length;i++)if(i!=1)Assert.AreEqual(saved[i],layout.Export()[i]);
            Assert.AreNotEqual(BuildingKind.Processor,layout.At(1));
        }

        [Test] public void SmallCatalogLeavesReachableEmptySlotsAndRejectsInvalidAssignments()
        {
            var layout=new HardwareBuildShortcuts(Catalog().Take(2));
            Assert.IsNull(layout.At(2));Assert.AreEqual(2,layout.Export().Count(i=>i>=0));
            Assert.Throws<ArgumentOutOfRangeException>(()=>layout.Assign(7,BuildingKind.Warehouse));
            Assert.Throws<ArgumentException>(()=>layout.Assign(0,BuildingKind.Rework));
        }
    }
}
