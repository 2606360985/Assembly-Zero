using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareHud
    {
        private TMP_Text computeSummary;
        private void ComputeDrawer(WorldSimulation w)
        {
            var p=w.Automation(game.Factory);
            Label(content,"自产显卡 → 机柜 → 渲染收入 → 扩建",17,50);
            computeSummary=Label(content,"",16,220);RefreshCompute();
            Button(content,p.Purchase?"自动补料：开启":"自动补料：关闭",()=>SetAutomation(!p.Purchase,p.Sell,p.KeepCards,p.CashReserve));
            Button(content,p.Sell?"经销商销售：开启":"经销商销售：关闭",()=>SetAutomation(p.Purchase,!p.Sell,p.KeepCards,p.CashReserve));
            Button(content,"保留成品 "+p.KeepCards+" 张 · 点击 +4，最多16",()=>SetAutomation(p.Purchase,p.Sell,p.KeepCards>=16?0:p.KeepCards+4,p.CashReserve));
            Button(content,"现金下限 "+HardwareGame.Money(p.CashReserve)+" · 点击切换",()=>SetAutomation(p.Purchase,p.Sell,p.KeepCards,p.CashReserve==100000?200000:p.CashReserve==200000?0:100000));
            Label(content,"补料跟随本厂首台总装型号，保持12套原料，计入在途；采用常规配送。经销商每10秒尝试买1张，订单预留优先，沿用现货额度和报价。",14,115);
            foreach(var kind in new[]{BuildingKind.ComputeRack,BuildingKind.PowerUnit,BuildingKind.CoolingUnit})
            {
                var k=kind;var b=w.Settings.Hardware.Buildings.First(x=>x.Kind==k);
                Button(content,"搭建"+b.Name+" · "+HardwareGame.Money(b.Cost),()=>game.BeginBuild(k));
            }
            foreach(var n in w.State.Nodes.Where(n=>n.Factory==game.Factory&&n.Kind==BuildingKind.ComputeRack))
            {
                var id=n.Id;Label(content,"机柜 #"+id+" · "+n.Input.Count+" / "+(4*n.Level)+" 张",16,30);
                Button(content,"上架1张自产显卡",()=>{game.Submit(new WorldCommand{Kind=CommandKind.InstallCompute,Factory=game.Factory,Node=id});RebuildDrawer();});
                Button(content,"退回1张到仓库",()=>{game.Submit(new WorldCommand{Kind=CommandKind.UninstallCompute,Factory=game.Factory,Node=id});RebuildDrawer();});
            }
            Label(content,"基础供电 / 冷却各支持4张卡；每模块增加8张容量。渲染业务最多利用16张，每有效卡每10秒收入¥3、成本¥0.5。机柜中的显卡可完整退回。",14,110);
        }
        private void SetAutomation(bool purchase,bool sell,int keep,int reserve)
        {
            game.Submit(new WorldCommand{Kind=CommandKind.AutomationPolicy,Factory=game.Factory,Flag=purchase,Priority=sell?1:0,Reserve=keep,Price=reserve});RebuildDrawer();
        }
        private void RefreshCompute()
        {
            if(game.Drawer!="算力"||computeSummary==null)return;
            var w=game.World;var p=w.Automation(game.Factory);var count=w.InstalledCards(game.Factory);var active=w.EffectiveCards(game.Factory);
            computeSummary.text="已装显卡 "+count+" · 有效算力 "+active+" / 16\n供电容量 "+w.ParkPower(game.Factory)+" · 冷却容量 "+w.ParkCooling(game.Factory)+"\n预计净收入 "+HardwareGame.Money(active*1500)+" / 分钟\n累计服务收入 "+HardwareGame.Money(p.Income)+"\n累计运营成本 "+HardwareGame.Money(p.Fees)+"\n"+p.Status+"\n"+(count==0?"下一步：建机柜，留4张成品上架":active<count?"下一步：补足供电、冷却，释放闲置算力":count<8?"下一步：扩建第二台机柜，挑战8卡园区":"园区运转中：继续扩建，或保留产能销售");
        }
    }
}
