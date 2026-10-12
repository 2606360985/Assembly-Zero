using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain
{
    [Serializable] public sealed class RecipeInput { public int Material, Quantity = 1; public RecipeInput() { } public RecipeInput(int material, int quantity = 1) { Material = material; Quantity = quantity; } }
    [Serializable] public sealed class FactoryRecipe
    {
        public string Id, Name;
        public int Output, OutputQuantity = 1, Duration = 120, InputCapacity = 8, OutputCapacity = 4;
        public RecipeInput[] Inputs = Array.Empty<RecipeInput>();
        public void Validate()
        {
            if (string.IsNullOrEmpty(Id) || Output < 2 || Output >= 64 || Duration < 1 || OutputQuantity < 1 || OutputCapacity < OutputQuantity || Inputs.Length == 0 || Inputs.Any(i => i.Material < 2 || i.Material >= 64 || i.Quantity < 1 || i.Quantity > InputCapacity) || Inputs.Select(i => i.Material).Distinct().Count() != Inputs.Length) throw new ArgumentException("Invalid recipe: " + Id);
        }
    }
    [Serializable] public sealed class FactoryModuleDefinition
    {
        public string Id, RecipeId;
        public int Material, Capacity = 32, InitialStock, RefillQuantity, AssemblyLane = -1;
        public bool Warehouse;
    }
    [Serializable] public sealed class FactoryConnection
    {
        public string Id, From, To;
        public int Material, Length = 4096, Speed = 32, Spacing = 512;
    }
    [Serializable] public sealed class FactoryDefinition
    {
        public FactoryRecipe[] Recipes = Array.Empty<FactoryRecipe>();
        public FactoryModuleDefinition[] Modules = Array.Empty<FactoryModuleDefinition>();
        public FactoryConnection[] Connections = Array.Empty<FactoryConnection>();
        public int RefillInterval = 600, FinishedCapacity = 8, DispatchInterval = 240;
        public void Validate()
        {
            foreach (var r in Recipes) r.Validate();
            if (Recipes.Select(r => r.Id).Distinct().Count() != Recipes.Length || Modules.Any(m => string.IsNullOrEmpty(m.Id)) || Modules.Select(m => m.Id).Distinct().Count() != Modules.Length || Connections.Select(c => c.Id).Distinct().Count() != Connections.Length || RefillInterval < 1 || FinishedCapacity < 1 || DispatchInterval < 1) throw new ArgumentException("Duplicate factory ID or invalid timing.");
            var modules = Modules.ToDictionary(m => m.Id); var recipes = Recipes.ToDictionary(r => r.Id);
            foreach (var m in Modules)
            {
                if (m.Capacity < 1 || m.InitialStock < 0 || m.InitialStock > m.Capacity || m.RefillQuantity < 0 || m.RefillQuantity > m.Capacity || m.AssemblyLane < -1 || m.AssemblyLane > 5 || !string.IsNullOrEmpty(m.RecipeId) && !recipes.ContainsKey(m.RecipeId)) throw new ArgumentException("Invalid module: " + m.Id);
                if (!string.IsNullOrEmpty(m.RecipeId) && !Connections.Any(c => c.From == m.Id)) throw new ArgumentException("Unused producer: " + m.Id);
                if (!string.IsNullOrEmpty(m.RecipeId)) foreach (var input in recipes[m.RecipeId].Inputs) if (!Connections.Any(c => c.To == m.Id && c.Material == input.Material)) throw new ArgumentException("Missing input: " + m.Id + " / " + input.Material);
            }
            foreach (var c in Connections)
            {
                if (string.IsNullOrEmpty(c.Id) || !modules.ContainsKey(c.From) || !modules.ContainsKey(c.To) || c.From == c.To || c.Length < c.Spacing * 2 || c.Speed < 1 || c.Spacing < 1) throw new ArgumentException("Invalid connection: " + c.Id);
                var source = modules[c.From]; var target = modules[c.To];
                var output = string.IsNullOrEmpty(source.RecipeId) ? source.Material : recipes[source.RecipeId].Output;
                if (output != c.Material || (string.IsNullOrEmpty(target.RecipeId) ? target.Material != c.Material : !recipes[target.RecipeId].Inputs.Any(i => i.Material == c.Material))) throw new ArgumentException("Material mismatch: " + c.Id);
            }
        }
    }
    public static class FactoryCatalog
    {
        public const int Metal = 8, Plastic = 9, Electronics = 10, Plate = 11, Rod = 12, Shell = 13, Gear = 14, Motor = 15, Circuit = 16;
        public static FactoryRecipe[] Recipes() => new[] {
            Recipe("plate", "板材", Plate,120,new RecipeInput(Metal)), Recipe("rod","杆件",Rod,120,new RecipeInput(Metal)), Recipe("shell","塑料壳",Shell,120,new RecipeInput(Plastic)),
            Recipe("gear","齿轮",Gear,180,new RecipeInput(Plate),new RecipeInput(Rod)), Recipe("motor","电机",Motor,240,new RecipeInput(Rod),new RecipeInput(Electronics)), Recipe("circuit","电路板",Circuit,180,new RecipeInput(Electronics),new RecipeInput(Plastic)),
            Recipe("body","躯干",3,360,new RecipeInput(Plate,2),new RecipeInput(Shell),new RecipeInput(Circuit)),
            Recipe("leg-a","LegA",6,240,new RecipeInput(Rod),new RecipeInput(Gear),new RecipeInput(Motor)), Recipe("leg-b","LegB",7,240,new RecipeInput(Rod),new RecipeInput(Gear),new RecipeInput(Motor)),
            Recipe("arm-a","ArmA",4,240,new RecipeInput(Shell),new RecipeInput(Gear),new RecipeInput(Motor)), Recipe("arm-b","ArmB",5,240,new RecipeInput(Shell),new RecipeInput(Gear),new RecipeInput(Motor)),
            Recipe("head","头部",2,300,new RecipeInput(Shell),new RecipeInput(Circuit,2)) };
        private static FactoryRecipe Recipe(string id,string name,int output,int duration,params RecipeInput[] inputs) => new FactoryRecipe { Id=id,Name=name,Output=output,Duration=duration,Inputs=inputs };
    }
}
