using AssemblyZero.Domain;
using UnityEngine;

namespace AssemblyZero.Unity
{
    [CreateAssetMenu(menuName = "Assembly Zero/Scenario", fileName = "EP01Scenario")]
    public sealed class ScenarioConfig : ScriptableObject
    {
        [HideInInspector] public int ConfigurationVersion;
        [Header("Simulation")] public int SpeedUnitsPerTick = 8;
        public int SpawnIntervalTicks = 2;
        public int OrderTarget = 100;
        public int MaxCatchUpTicks = 8;
        [Header("Physical Cargo Demo")]
        [Min(1)] public int PhysicalMinimumSpacing = 400;
        [Min(1)] public int SmallLoadItems = 96;
        [Min(1)] public int GateDemoItems = 112;
        [Min(1)] public int PhysicalVisibleItems = 128;
        [Header("20,000 Item Stress Test")]
        [Min(1)] public int StressLogicalItems = 20000;
        [Min(1)] public int StressMinimumSpacing = 2;
        [Min(1)] public int StressVisibleItems = 128;
        [Header("Stage 6")] public int GateCloseTick = 180;
        public int GateOpenTick = 480;

        public ScenarioDefinition CreatePhysical(int maxItems, int initialItems = 0) => Create(maxItems, initialItems, PhysicalMinimumSpacing, PhysicalVisibleItems);
        public ScenarioDefinition CreateStress() => Create(StressLogicalItems, StressLogicalItems, StressMinimumSpacing, StressVisibleItems);

        private ScenarioDefinition Create(int maxItems, int initialItems, int minimumSpacing, int visibleItems) => new ScenarioDefinition
        {
            SpeedUnitsPerTick = SpeedUnitsPerTick,
            MinimumSpacing = minimumSpacing,
            SpawnIntervalTicks = SpawnIntervalTicks,
            MaxItems = maxItems,
            InitialItems = initialItems,
            InitialFrontDistance = initialItems > 0 ? initialItems * minimumSpacing : -1,
            AlternateItemTypes = true,
            OrderTarget = OrderTarget,
            VisibleItems = visibleItems,
            GateCloseTick = -1,
            GateOpenTick = -1
        };

#if UNITY_EDITOR
        public void ApplyCargoDemoDefaultsIfNeeded()
        {
            if (ConfigurationVersion >= 4) return;
            PhysicalMinimumSpacing = 400;
            SmallLoadItems = 96;
            GateDemoItems = 112;
            PhysicalVisibleItems = 128;
            StressLogicalItems = 20000;
            StressMinimumSpacing = 2;
            StressVisibleItems = 128;
            ConfigurationVersion = 4;
        }
#endif
    }
}
