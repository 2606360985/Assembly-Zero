using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public sealed partial class WorldSimulation
    {
        private void MigrateHardwareProducts()
        {
            var h=State.Hardware;
            if(h==null||h.ContentId!="az-gpu")return;
            h.GenerationHistory=h.GenerationHistory??new List<GenerationLaunch>();
            var legacy=State.Version==3&&h.ContentVersion==2;
            if(!legacy&&!(State.Version==4&&h.ContentVersion==3))return;
            if(legacy)
            {
                h.Skus=(Settings.Hardware.Skus??Array.Empty<SkuDefinition>()).Select(s=>s.Clone()).ToList();
                foreach(var s in h.Skus)s.Score=HardwareSku.Score(s);
                var all=State.Nodes.SelectMany(n=>n.Input.Concat(n.Output).Concat(n.Batch)).Concat(State.Routes.SelectMany(r=>r.Cargo))
                    .Concat(State.Vehicles.SelectMany(v=>v.Cargo)).Concat(State.Factories.SelectMany(f=>f.Starter)).Concat(h.Deliveries.SelectMany(d=>d.Cargo));
                foreach(var g in all)
                {
                    var quiet=g.Material==43||g.Material==45||g.Material==47;
                    g.Material=g.Material==43?HardwareCatalog.RawGPU:g.Material==45?HardwareCatalog.Tested:g.Material==47?HardwareCatalog.Standard:g.Material;
                    if(HardwareCatalog.IsCard(g.Material)&&string.IsNullOrEmpty(g.SpecId))g.SpecId=quiet?HardwareCatalog.QuietSpec:HardwareCatalog.Spec(g.Material);
                    g.Gen=Math.Max(1,g.Gen);if(g.Material==HardwareCatalog.Standard||g.Material==HardwareCatalog.Tested)g.Grade=GradeB;
                }
                string RecipeId(string id)=>id=="gpu"?HardwareSku.RecipeId(HardwareCatalog.StandardSpec):id=="quiet-gpu"?HardwareSku.RecipeId(HardwareCatalog.QuietSpec):id=="quiet-test"?"test":id=="quiet-pack"?"pack":id;
                foreach(var n in State.Nodes){n.RecipeId=RecipeId(n.RecipeId);n.PendingRecipe=RecipeId(n.PendingRecipe);n.ProcessGen=1;}
                foreach(var o in h.Orders)if(o.Material==47){o.Material=HardwareCatalog.Standard;o.Spec=HardwareCatalog.QuietSpec;}
                // Route payloads are unchanged; the belt's part type and network are rebuilt below on demand.
                foreach(var r in State.Routes)
                {
                    r.Material=r.Material==43?HardwareCatalog.RawGPU:r.Material==45?HardwareCatalog.Tested:r.Material==47?HardwareCatalog.Standard:r.Material;
                    if(r.Saved!=null)foreach(var g in r.Saved.Items)g.Material=r.Material;
                }
                h.ResearchDone=h.ResearchDone??new List<string>();h.ResearchActive=h.ResearchActive??"";h.CoreGen=Math.Max(1,h.CoreGen);h.NextSku=Math.Max(1,h.NextSku);
            }
            h.IndustryGen=Math.Max(1,h.IndustryGen);h.ContentVersion=Settings.Hardware.Version;State.Version=HardwareSaveVersion;
        }
    }
}
