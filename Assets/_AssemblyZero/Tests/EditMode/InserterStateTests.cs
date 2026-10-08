using AssemblyZero.Domain;
using NUnit.Framework;
namespace AssemblyZero.Tests
{
    public sealed class InserterStateTests
    {
        [Test]public void StateChangesOnlyAtConfiguredTicks()
        {
            var c=EP02TestFactory.Config();c.InitialParts=EP02TestFactory.Stock(c);var w=EP02TestFactory.World(c);EP02TestFactory.Until(w,()=>w.Snapshot.Inserters[0].State==InserterState.Reserving);
            var expected=new[]{InserterState.Picking,InserterState.Picking,InserterState.Carrying,InserterState.Carrying,InserterState.Placing,InserterState.Placing,InserterState.Returning,InserterState.Returning,InserterState.WaitingForItem};
            foreach(var state in expected){w.Tick();Assert.AreEqual(state,w.Snapshot.Inserters[0].State,"tick "+w.TickNumber);}
        }
        [Test]public void ProductDoesNotDepartBeforeArmReturns()
        {
            var c=EP02TestFactory.Config(true);c.InitialParts=EP02TestFactory.Stock(c);var w=EP02TestFactory.World(c);EP02TestFactory.Until(w,()=>w.Snapshot.Inserters[0].State==InserterState.Returning);
            var distance=w.Snapshot.Products[0].Distance;w.Tick();Assert.AreEqual(distance,w.Snapshot.Products[0].Distance);Assert.AreEqual(0,w.Snapshot.Products[0].NextStation);w.Tick();Assert.AreEqual(1,w.Snapshot.Products[0].NextStation);w.Tick();Assert.Greater(w.Snapshot.Products[0].Distance,distance);
        }
    }
}
