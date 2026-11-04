using System;
using System.Collections.Generic;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    public enum HardwareProductForm { Ore, Billet, Core, Memory, BareBoard, Shell, Carton, Cooler, PopulatedBoard, Card, Retail }
    public readonly struct HardwareProductStyle
    {
        public readonly HardwareProductForm Form;
        public readonly bool Quiet, Qualified;
        public readonly Color Color;
        public readonly string Mark, Stage;
        public HardwareProductStyle(HardwareProductForm form,string mark,string stage,Color color,bool quiet=false,bool qualified=false)
        {Form=form;Mark=mark;Stage=stage;Color=color;Quiet=quiet;Qualified=qualified;}
    }
    // Packs a material and the SKU appearance of a card into one cache key:
    // bits 0-7 material, 8-9 cooler, 10-11 memory tier, 12-14 shell colour, 15-17 generation bucket, 18-19 grade.
    public static class HardwareVisualKey
    {
        public static int Material(int key)=>key&0xFF;
        public static int Cooler(int key)=>(key>>8)&3;
        public static bool Quiet(int key)=>Cooler(key)==1;
        public static int MemoryTier(int key)=>(key>>10)&3;
        public static int Shell(int key)=>(key>>12)&7;
        public static int Gen(int key)=>((key>>15)&7)+1;
        public static int Grade(int key)=>(key>>18)&3;
        public static bool Styled(int key)=>key>0xFF;
        public static int Fans(int key)=>(key>>21)&3;
        public static int Length(int key)=>(key>>23)&3;
        public static bool Backplate(int key)=>(key&(1<<25))!=0;
        public static int Of(int material,SkuDefinition sku,int grade=0,int generation=0)
        {
            if(sku==null||(material!=HardwareCatalog.RawGPU&&material!=HardwareCatalog.Tested&&material!=HardwareCatalog.Standard))return material;
            var cooler=sku.Cooler==HardwareCatalog.QuietHeat?1:sku.Cooler==HardwareCatalog.TowerHeat?2:0;
            var memory=sku.Memory>=4?2:sku.Memory>=2?1:0;
            return material|cooler<<8|memory<<10|(sku.Shell&7)<<12|(Math.Min(8,Math.Max(1,generation==0?sku.Gen:generation))-1)<<15|(grade&3)<<18|1<<20|(sku.Fans&3)<<21|(sku.Length&3)<<23|(sku.Backplate?1<<25:0);
        }
        public static int Of(Goods g,WorldSimulation w)=>Of(g.Material,w.Sku(g.SpecId),g.Grade,g.Gen);
    }
    // Shared by world cargo, station previews and HUD. IDs are never inferred from ranges.
    public static class HardwareVisualCatalog
    {
        public static readonly Color Cyan=new Color(.05f,.72f,.82f),Ivory=new Color(.83f,.9f,.94f),Graphite=new Color(.09f,.14f,.2f),Reject=new Color(.86f,.22f,.18f),Kraft=new Color(.68f,.5f,.3f);
        private static readonly Dictionary<int,HardwareProductStyle> Styles=new Dictionary<int,HardwareProductStyle>
        {
            {HardwareCatalog.Ore,new HardwareProductStyle(HardwareProductForm.Ore,"AL","矿石",new Color(.49f,.57f,.62f))},
            {HardwareCatalog.Aluminum,new HardwareProductStyle(HardwareProductForm.Billet,"AL","铝材",new Color(.67f,.74f,.79f))},
            {HardwareCatalog.Core,new HardwareProductStyle(HardwareProductForm.Core,"GPU","核心",Graphite)},
            {HardwareCatalog.Memory,new HardwareProductStyle(HardwareProductForm.Memory,"VRAM","显存",Graphite)},
            {HardwareCatalog.Board,new HardwareProductStyle(HardwareProductForm.BareBoard,"PCB","裸板",new Color(.06f,.38f,.26f))},
            {HardwareCatalog.Shell,new HardwareProductStyle(HardwareProductForm.Shell,"CASE","外壳",Graphite)},
            {HardwareCatalog.Carton,new HardwareProductStyle(HardwareProductForm.Carton,"PACK","包材",new Color(.64f,.46f,.29f))},
            {HardwareCatalog.Heat,new HardwareProductStyle(HardwareProductForm.Cooler,"S1","标准散热",Cyan)},
            {HardwareCatalog.QuietHeat,new HardwareProductStyle(HardwareProductForm.Cooler,"Q","低噪散热",Ivory,true)},
            {HardwareCatalog.AssembledBoard,new HardwareProductStyle(HardwareProductForm.PopulatedBoard,"PCB+","已贴装",new Color(.06f,.38f,.26f))},
            {HardwareCatalog.TowerHeat,new HardwareProductStyle(HardwareProductForm.Cooler,"T","塔式散热",new Color(.62f,.68f,.72f),true)},
            {HardwareCatalog.RawGPU,new HardwareProductStyle(HardwareProductForm.Card,"S1","待检测",Cyan)},
            {HardwareCatalog.Tested,new HardwareProductStyle(HardwareProductForm.Card,"S1 / QC","检测合格",Cyan,false,true)},
            {HardwareCatalog.Standard,new HardwareProductStyle(HardwareProductForm.Retail,"S1 STANDARD","包装成品",Cyan,false,true)},
            {HardwareCatalog.Defect,new HardwareProductStyle(HardwareProductForm.Card,"NG","检测次品",Reject)},
            {HardwareCatalog.Budget,new HardwareProductStyle(HardwareProductForm.Retail,"S1 LITE","丐版简装",Kraft,false,true)}
        };
        // Low-noise SKUs share the card materials but keep the ivory three-fan identity.
        private static readonly Dictionary<int,HardwareProductStyle> QuietStyles=new Dictionary<int,HardwareProductStyle>
        {
            {HardwareCatalog.RawGPU,new HardwareProductStyle(HardwareProductForm.Card,"Q","待检测",Ivory,true)},
            {HardwareCatalog.Tested,new HardwareProductStyle(HardwareProductForm.Card,"Q / QC","检测合格",Ivory,true,true)},
            {HardwareCatalog.Standard,new HardwareProductStyle(HardwareProductForm.Retail,"S1 QUIET","包装成品",Ivory,true,true)}
        };
        public static HardwareProductStyle Get(int material)=>Styles.TryGetValue(material,out var value)?value:Styles[HardwareCatalog.Carton];
        public static HardwareProductStyle Get(int material,bool quiet)=>quiet&&QuietStyles.TryGetValue(material,out var value)?value:Get(material);
        public static string Caption(int material){var s=Get(material);return "<color=#"+ColorUtility.ToHtmlStringRGB(s.Color)+">[ "+s.Mark+" ]</color> · "+s.Stage;}
    }
}
