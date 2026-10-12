using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain
{
    public enum FactoryOwner { Buffer, Processing, Transit, Assembly, Consumed }
    public enum FactoryMachineState { Idle, MissingInput, Processing, OutputBlocked }
    public sealed class FactoryMaterial
    {
        public int Id, Type, Batch;
        public FactoryOwner Owner;
        public string Location;
    }
    public sealed class FactoryBatch
    {
        public int Id; public string Module, Recipe; public long Started, Finished = -1;
        public int[] Inputs; public int[] Outputs = Array.Empty<int>();
    }
    public sealed class FactorySupplyReceipt
    {
        public string Warehouse; public int Material; public long Tick; public int[] Items;
    }
    public sealed class FactoryProductReceipt
    {
        public int ProductId; public long AcceptedTick, DispatchedTick=-1;
    }
    public sealed class FactoryModuleState
    {
        public FactoryModuleDefinition Definition { get; internal set; }
        public FactoryRecipe Recipe { get; internal set; }
        public readonly Dictionary<int, Queue<int>> Inputs = new Dictionary<int, Queue<int>>();
        public readonly Queue<int> Output = new Queue<int>();
        public FactoryMachineState State { get; internal set; }
        public int CompletedBatches { get; internal set; }
        public FactoryBatch Batch { get; internal set; }
        internal long NextRefill;
        internal long NextSend;
    }
    public sealed class FactoryRouteState
    {
        public FactoryConnection Definition { get; internal set; }
        public IBeltSimulationBackend Backend { get; internal set; }
        public IIdentityBeltAccess Access => (IIdentityBeltAccess)Backend;
        public BeltSnapshotWriter Snapshot { get; } = new BeltSnapshotWriter();
    }
    /// <summary>One fixed clock. Buffers, conversion receipts and assembly each have a single owner.</summary>
    public sealed class FactorySimulation
    {
        public FactoryDefinition Definition { get; }
        public EP02Simulation Assembly { get; }
        public IReadOnlyList<FactoryModuleState> Modules => modules;
        public IReadOnlyList<FactoryRouteState> Routes => routes;
        public IReadOnlyList<FactoryBatch> Batches => batches;
        public IReadOnlyList<FactorySupplyReceipt> SupplyReceipts => supplyReceipts;
        public IReadOnlyCollection<FactoryProductReceipt> ProductReceipts => productReceipts.Values;
        public IReadOnlyDictionary<int,FactoryMaterial> Ledger => ledger;
        public IReadOnlyCollection<ProductSnapshot> Finished => finished;
        public long TickNumber { get; private set; }
        public int Dispatched { get; private set; }
        public bool SupplyEnabled = true, DispatchEnabled = true;
        private readonly List<FactoryModuleState> modules = new List<FactoryModuleState>();
        private readonly List<FactoryRouteState> routes = new List<FactoryRouteState>();
        private readonly Dictionary<string,FactoryModuleState> byId = new Dictionary<string,FactoryModuleState>();
        private readonly Dictionary<int,FactoryMaterial> ledger = new Dictionary<int,FactoryMaterial>();
        private readonly List<FactoryBatch> batches = new List<FactoryBatch>();
        private readonly List<FactorySupplyReceipt> supplyReceipts = new List<FactorySupplyReceipt>();
        private readonly Dictionary<int,FactoryProductReceipt> productReceipts = new Dictionary<int,FactoryProductReceipt>();
        private readonly Queue<ProductSnapshot> finished = new Queue<ProductSnapshot>();
        private readonly Dictionary<string,int> cursors = new Dictionary<string,int>();
        private readonly List<BeltAccessCandidate> scratch = new List<BeltAccessCandidate>();
        private int nextId = 1;
        private long nextDispatch;
        public FactorySimulation(FactoryDefinition definition, EP02Simulation assembly, BackendKind backend = BackendKind.Gap)
        {
            definition.Validate(); Definition=definition; Assembly=assembly;
            if (assembly.TickNumber != 0) throw new ArgumentException("Assembly must start at tick zero.");
            foreach (var d in definition.Modules.OrderBy(m=>m.Id,StringComparer.Ordinal))
            {
                var m=new FactoryModuleState { Definition=d, Recipe=definition.Recipes.FirstOrDefault(r=>r.Id==d.RecipeId), NextRefill=definition.RefillInterval };
                if(m.Recipe!=null) foreach(var i in m.Recipe.Inputs) m.Inputs.Add(i.Material,new Queue<int>());
                modules.Add(m);byId.Add(d.Id,m);
                if(d.InitialStock>0)Stock(m,d.InitialStock);
            }
            foreach(var d in definition.Connections.OrderBy(c=>c.Id,StringComparer.Ordinal))
            {
                IBeltSimulationBackend b=backend==BackendKind.Gap?(IBeltSimulationBackend)new GapTransportLineSimulation():new NaiveItemBeltSimulation();
                b.Reset(new ScenarioDefinition { ExternalLogistics=true,MinimumSpacing=d.Spacing,SpeedUnitsPerTick=d.Speed },BeltTopology.CreateStraight(1,d.Length));
                routes.Add(new FactoryRouteState { Definition=d,Backend=b });
            }
            Assembly.TryAcceptProduct=p=> { if(finished.Count>=Definition.FinishedCapacity) return false;productReceipts.Add(p.Id,new FactoryProductReceipt{ProductId=p.Id,AcceptedTick=TickNumber});finished.Enqueue(p);return true; };
            AssertConserved();
        }
        private int Create(int type,FactoryOwner owner,string location,int batch=0)
        {
            var id=nextId++;ledger.Add(id,new FactoryMaterial { Id=id,Type=type,Owner=owner,Location=location,Batch=batch });return id;
        }
        private void Stock(FactoryModuleState module,int quantity)
        {
            var receipt=new FactorySupplyReceipt{Warehouse=module.Definition.Id,Material=module.Definition.Material,Tick=TickNumber,Items=new int[quantity]};
            for(var i=0;i<quantity;i++){var id=Create(receipt.Material,FactoryOwner.Buffer,receipt.Warehouse);receipt.Items[i]=id;module.Output.Enqueue(id);}supplyReceipts.Add(receipt);
        }
        public void Tick()
        {
            TickNumber++;
            if(DispatchEnabled && finished.Count>0 && TickNumber>=nextDispatch) { var product=finished.Dequeue();productReceipts[product.Id].DispatchedTick=TickNumber;Dispatched++;nextDispatch=TickNumber+Definition.DispatchInterval; }
            foreach(var m in modules)
            {
                var d=m.Definition;
                if(d.Warehouse && SupplyEnabled && TickNumber>=m.NextRefill && m.Output.Count+d.RefillQuantity<=d.Capacity)
                { if(d.RefillQuantity>0)Stock(m,d.RefillQuantity);m.NextRefill=TickNumber+Definition.RefillInterval; }
                Process(m);
            }
            foreach(var r in routes) r.Access.TransportTick(TickNumber);
            // Rotate receiving priority so a full merge buffer cannot starve later producers.
            for(var i=0;i<routes.Count;i++)Receive(routes[(i+(int)(TickNumber%routes.Count))%routes.Count]);
            foreach(var m in modules) Send(m);
            foreach(var m in modules.Where(m=>m.Definition.AssemblyLane>=0))
            {
                if(m.Output.Count==0)continue;var id=m.Output.Peek();
                if(!Assembly.TrySupply(id,new PartTypeId(ledger[id].Type),m.Definition.AssemblyLane))continue;
                m.Output.Dequeue();ledger[id].Owner=FactoryOwner.Assembly;ledger[id].Location="assembly";
            }
            Assembly.Tick();
            foreach(var r in routes)r.Backend.CreateSnapshot(r.Snapshot);
        }
        private void Process(FactoryModuleState m)
        {
            var r=m.Recipe;if(r==null)return;
            if(m.Batch!=null)
            {
                if(TickNumber-m.Batch.Started<r.Duration) {m.State=FactoryMachineState.Processing;return;}
                if(m.Output.Count+r.OutputQuantity>r.OutputCapacity) {m.State=FactoryMachineState.OutputBlocked;return;}
                var batch=m.Batch;
                foreach(var id in batch.Inputs) {ledger[id].Owner=FactoryOwner.Consumed;ledger[id].Location="batch/"+batch.Id;}
                batch.Outputs=new int[r.OutputQuantity];
                for(var i=0;i<batch.Outputs.Length;i++){var id=Create(r.Output,FactoryOwner.Buffer,m.Definition.Id,batch.Id);batch.Outputs[i]=id;m.Output.Enqueue(id);}
                batch.Finished=TickNumber;m.CompletedBatches++;m.Batch=null;
            }
            if(m.Output.Count+r.OutputQuantity>r.OutputCapacity){m.State=FactoryMachineState.OutputBlocked;return;}
            if(r.Inputs.Any(i=>m.Inputs[i.Material].Count<i.Quantity)){m.State=FactoryMachineState.MissingInput;return;}
            var ids=new List<int>();foreach(var i in r.Inputs)for(var n=0;n<i.Quantity;n++)ids.Add(m.Inputs[i.Material].Dequeue());
            m.Batch=new FactoryBatch{Id=batches.Count+1,Module=m.Definition.Id,Recipe=r.Id,Started=TickNumber,Inputs=ids.ToArray()};batches.Add(m.Batch);
            foreach(var id in ids){ledger[id].Owner=FactoryOwner.Processing;ledger[id].Location=m.Definition.Id;}
            m.State=FactoryMachineState.Processing;
        }
        private void Receive(FactoryRouteState r)
        {
            var d=r.Definition;var target=byId[d.To];
            var queue=target.Recipe==null?target.Output:target.Inputs[d.Material];
            var cap=target.Recipe==null?target.Definition.Capacity:target.Recipe.InputCapacity;
            if(queue.Count>=cap)return;
            var end=d.Length-d.Spacing;
            r.Access.QueryCandidates(new BeltAccessPort(new LineId(0),1,end,end,end,1UL<<d.Material),scratch);
            if(scratch.Count==0)return;var item=scratch[0];
            if(!r.Access.TryTake(item.Handle,out _))return;
            queue.Enqueue(item.Handle.ItemId.Value);var record=ledger[item.Handle.ItemId.Value];record.Owner=FactoryOwner.Buffer;record.Location=d.To;
        }
        private void Send(FactoryModuleState m)
        {
            if(m.Output.Count==0||TickNumber<m.NextSend)return;
            var choices=routes.Where(r=>r.Definition.From==m.Definition.Id).ToArray();if(choices.Length==0)return;
            cursors.TryGetValue(m.Definition.Id,out var cursor);
            for(var n=0;n<choices.Length;n++)
            {
                var index=(cursor+n)%choices.Length;var route=choices[index];var d=route.Definition;var id=m.Output.Peek();
                if(!route.Access.TryInsertExisting(new BeltAccessPort(new LaneId(0),0,0),new ItemId(id),new PartTypeId(ledger[id].Type),out _))continue;
                m.Output.Dequeue();ledger[id].Owner=FactoryOwner.Transit;ledger[id].Location=d.Id;cursors[m.Definition.Id]=(index+1)%choices.Length;m.NextSend=TickNumber+(d.Spacing+d.Speed-1)/d.Speed;break;
            }
        }
        public void AssertConserved()
        {
            var seen=new HashSet<int>();
            void Own(int id,FactoryOwner owner,string location){if(!ledger.TryGetValue(id,out var x)||x.Owner!=owner||x.Location!=location||!seen.Add(id))throw new InvalidOperationException("Factory ownership violation: "+id+" / "+location);}
            foreach(var m in modules)
            {
                foreach(var id in m.Output)Own(id,FactoryOwner.Buffer,m.Definition.Id);
                foreach(var q in m.Inputs.Values)foreach(var id in q)Own(id,FactoryOwner.Buffer,m.Definition.Id);
                if(m.Batch!=null)foreach(var id in m.Batch.Inputs)Own(id,FactoryOwner.Processing,m.Definition.Id);
                if(m.Output.Count>(m.Recipe?.OutputCapacity??m.Definition.Capacity)||m.Inputs.Values.Any(q=>q.Count>m.Recipe.InputCapacity))throw new InvalidOperationException("Buffer overflow: "+m.Definition.Id);
            }
            foreach(var r in routes){r.Backend.CreateSnapshot(r.Snapshot);foreach(var i in r.Snapshot.Items)Own(i.ItemId.Value,FactoryOwner.Transit,r.Definition.Id);}
            foreach(var x in ledger.Values)
            {
                if(x.Owner==FactoryOwner.Assembly){if(!Assembly.TryGetItemRecord(x.Id,out var part)||part.Part.Value!=x.Type)throw new InvalidOperationException("Assembly identity lost: "+x.Id);Own(x.Id,FactoryOwner.Assembly,"assembly");}
                if(x.Owner==FactoryOwner.Consumed)Own(x.Id,FactoryOwner.Consumed,x.Location);
            }
            foreach(var b in batches)
            {
                var r=Definition.Recipes.Single(x=>x.Id==b.Recipe);
                foreach(var i in r.Inputs)if(b.Inputs.Count(id=>ledger[id].Type==i.Material)!=i.Quantity)throw new InvalidOperationException("Batch input mismatch: "+b.Id);
                if(b.Finished>=0&&(b.Outputs.Length!=r.OutputQuantity||b.Outputs.Any(id=>ledger[id].Batch!=b.Id||ledger[id].Type!=r.Output)))throw new InvalidOperationException("Batch output mismatch: "+b.Id);
            }
            if(batches.SelectMany(b=>b.Inputs).Distinct().Count()!=batches.Sum(b=>b.Inputs.Length))throw new InvalidOperationException("A material was consumed by multiple batches.");
            var created=new HashSet<int>();
            foreach(var receipt in supplyReceipts)foreach(var id in receipt.Items)
                if(!created.Add(id)||!ledger.TryGetValue(id,out var material)||material.Batch!=0||material.Type!=receipt.Material||byId[receipt.Warehouse].Definition.Material!=receipt.Material)throw new InvalidOperationException("Invalid supply receipt: "+receipt.Warehouse);
            foreach(var batch in batches)foreach(var id in batch.Outputs)if(!created.Add(id))throw new InvalidOperationException("Duplicate creation receipt: "+id);
            if(created.Count!=ledger.Count)throw new InvalidOperationException("A material has no creation receipt.");
            foreach(var batch in batches.Where(b=>b.Finished>=0))foreach(var id in batch.Inputs)if(ledger[id].Owner!=FactoryOwner.Consumed||ledger[id].Location!="batch/"+batch.Id)throw new InvalidOperationException("Consumed input no longer matches its conversion receipt: "+id);
            if(productReceipts.Count!=Dispatched+finished.Count||productReceipts.Values.Count(p=>p.DispatchedTick>=0)!=Dispatched||finished.Any(p=>!productReceipts.TryGetValue(p.Id,out var receipt)||receipt.DispatchedTick>=0))throw new InvalidOperationException("Product admission/dispatch receipt mismatch.");
            if(seen.Count!=ledger.Count||!Assembly.Statistics.Conserved||!Assembly.Snapshot.ProductsConserved||Assembly.Snapshot.ExitedProducts!=Dispatched+finished.Count)throw new InvalidOperationException("Factory conservation violated.");
        }
        public ulong CalculateStateHash()
        {
            var h=StateHasher.Add(StateHasher.Offset,TickNumber);h=StateHasher.Add(h,(long)Assembly.CalculateStateHash());
            foreach(var x in new long[]{nextId,nextDispatch,Dispatched,SupplyEnabled?1:0,DispatchEnabled?1:0})h=StateHasher.Add(h,x);
            foreach(var m in modules){h=StateHasher.Add(h,m.NextRefill);h=StateHasher.Add(h,m.NextSend);h=StateHasher.Add(h,m.CompletedBatches);h=StateHasher.Add(h,(int)m.State);foreach(var q in m.Inputs.OrderBy(x=>x.Key)){h=StateHasher.Add(h,q.Key);foreach(var id in q.Value)h=StateHasher.Add(h,id);}foreach(var id in m.Output)h=StateHasher.Add(h,id);}
            foreach(var r in routes)h=StateHasher.Add(h,(long)r.Backend.CalculateStateHash());
            foreach(var b in batches){h=StateHasher.Add(h,b.Id);h=StateHasher.Add(h,b.Started);h=StateHasher.Add(h,b.Finished);foreach(var id in b.Inputs)h=StateHasher.Add(h,id);foreach(var id in b.Outputs)h=StateHasher.Add(h,id);}
            foreach(var p in finished)h=StateHasher.Add(h,p.Id);
            foreach(var receipt in supplyReceipts){h=StateHasher.Add(h,receipt.Tick);foreach(var id in receipt.Items)h=StateHasher.Add(h,id);}
            foreach(var receipt in productReceipts.Values.OrderBy(p=>p.ProductId)){h=StateHasher.Add(h,receipt.ProductId);h=StateHasher.Add(h,receipt.AcceptedTick);h=StateHasher.Add(h,receipt.DispatchedTick);}
            foreach(var c in cursors.OrderBy(x=>x.Key,StringComparer.Ordinal)){foreach(var ch in c.Key)h=StateHasher.Add(h,ch);h=StateHasher.Add(h,c.Value);}
            foreach(var x in ledger.Values){h=StateHasher.Add(h,x.Id);h=StateHasher.Add(h,x.Type);h=StateHasher.Add(h,(int)x.Owner);foreach(var ch in x.Location)h=StateHasher.Add(h,ch);}
            return h;
        }
    }
}
