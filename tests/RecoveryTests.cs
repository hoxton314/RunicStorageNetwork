using System;
using System.Linq;
using RunicStorageNetwork.Logic;

static class RecoveryTests {
 static int passed;
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS recovery "+name);}
 static NetworkNode Node(string id,double x,bool root=false,string saved="",bool confirmed=true)=>new NetworkNode{Id=id,Position=new Point(x,0,0),Root=root,Network=saved,Confirmed=confirmed,Storage=20,Supply=15};
 internal static int Run(){
  Test("saved network survives reassigned runtime core ID",()=>{
   string saved=NetworkGraph.RootIdentity("OLD:00000123"),current="0000000000000001:0000A7BB";
   Assert(saved!=NetworkGraph.RootIdentity(current),"regression fixture must reproduce old rejection");
   Assert(NetworkGraph.PersistentIdentity(saved)==saved&&NetworkGraph.MatchesRoot(saved,saved),"saved binding changed");
   Assert(!NetworkGraph.MatchesRoot(saved,"other")&&!NetworkGraph.MatchesRoot("",""),"wrong root accepted");
  });
  Test("new roots cannot inherit a reused runtime object's identity",()=>Assert(NetworkGraph.PersistentIdentity("")!=NetworkGraph.PersistentIdentity(""),"new identity reused"));
  Test("unbound and formerly manual relays connect without selection",()=>{
   var g=NetworkGraph.Automatic(new[]{Node("a",0,true,"root-a"),Node("b",40),Node("c",80,false,"obsolete-choice")},50);
   Assert(g.Hops["c"]==2&&g.Supplies(g.Nodes["a"].Network,new Point(90,0,0),n=>true),"automatic path missing");
  });
  Test("connected roots merge resources and use nearest root paths",()=>{
   var g=NetworkGraph.Automatic(new[]{Node("a",0,true,"root-a"),Node("b",40),Node("c",80,true,"root-c"),Node("d",120)},50);
   Assert(g.Nodes["a"].Network==g.Nodes["c"].Network&&g.Roots.Count==1&&g.Hops["d"]==1,"roots not merged");
   Assert(g.Covers(g.Nodes["a"].Network,new Point(130,0,0),n=>true),"other root stock unavailable");
  });
  Test("destroying a bridge splits components without stale binding",()=>{
   var all=new[]{Node("a",0,true,"a"),Node("b",40),Node("c",80,true,"c")};
   var before=NetworkGraph.Automatic(all,50);var after=NetworkGraph.Automatic(all.Where(n=>n.Id!="b"),50);
   Assert(before.Roots.Count==1&&after.Roots.Count==2&&!after.Covers(after.Nodes["a"].Network,new Point(80,0,0),n=>true),"split retained remote resources");
  });
  Test("ward denied bridge cannot contribute to preview or payment",()=>{
   var g=NetworkGraph.Automatic(new[]{Node("a",0,true,"a"),Node("b",40,false,"",false),Node("c",80)},50);
   Assert(!g.Hops.ContainsKey("c")&&!g.Covers(g.Nodes["a"].Network,new Point(80,0,0),n=>true),"denied transit used");
  });
  Test("isolated relays and copied identifiers do not join remote storage",()=>{
   var g=NetworkGraph.Automatic(new[]{Node("a",0,true,"copied"),Node("b",200,true,"copied"),Node("c",400)},50);
   Assert(g.Nodes["a"].Network!=g.Nodes["b"].Network&&!g.Hops.ContainsKey("c"),"disconnected identity collision");
  });
  Test("automatic topology independent of enumeration order",()=>{
   var nodes=new[]{Node("c",80,true,"c"),Node("b",40),Node("a",0,true,"a")};var a=NetworkGraph.Automatic(nodes,50);var b=NetworkGraph.Automatic(nodes.Reverse(),50);
   Assert(a.Nodes.All(n=>n.Value.Network==b.Nodes[n.Key].Network&&a.Hops[n.Key]==b.Hops[n.Key]),"nondeterministic component");
  });
  Test("overlapping coverage does not revoke a valid pending network",()=>{
   var g=NetworkGraph.Automatic(new[]{Node("a",0,true,"a"),Node("b",20,true,"b")},5);
   string pinned=g.Nodes["a"].Network;Assert(g.Choose(new Point(12,0,0),n=>true)!=pinned&&g.Supplies(pinned,new Point(12,0,0),n=>true),"selection used instead of coverage");
  });
  Test("pending payment cannot be retried solely because of elapsed time",()=>{
   var r=new RecoveryAttempt(0);r.Sent();Assert(r.Expired(100)&&r.InFlight&&r.Attempts==1,"timeout fabricated a terminal result");
  });
  Test("terminal transient refusal backs off before replanning",()=>{
   var r=new RecoveryAttempt(0);r.Sent();Assert(r.Retry("owner refused: busy/reserved",1)&&!r.InFlight&&r.Due>1,"no recovery");r.Sent();Assert(r.Attempts==2&&r.InFlight,"new attempt not tracked");
  });
  Test("cancelled recipe or lost permission is not silently retried",()=>{
   foreach(string reason in new[]{"access denied","actor session mismatch","craft cancelled/changed","invalid character contribution","placement moved/cancelled","invalid owner snapshot"})Assert(!RecoveryAttempt.Transient(reason),reason);
  });
  Test("stock and path changes are retryable including completion validation",()=>{
   foreach(string reason in new[]{"network path or coverage changed","insufficient fresh resources","commit refused: ownership changed","action refused: source path/access changed before result: chest","network path or coverage changed / source path changed before completion"})Assert(RecoveryAttempt.Transient(reason),reason);
  });
  Test("safe recovery ends rather than locking an empty inventory forever",()=>{var r=new RecoveryAttempt(0);Assert(!r.Retry("insufficient fresh resources",8)&&!r.InFlight,"unbounded safe retry");});
  Test("lost output acknowledgement reuses the recorded result",()=>{
   var receipts=new OutcomeReceipts();int outputs=0;
   for(int delivery=0;delivery<4;delivery++){if(receipts.TryGet("id",out var hit)){Assert(hit.Success,"lost outcome");continue;}outputs++;receipts.Record("id",true,"result observed");}
   Assert(outputs==1,"duplicate craft");
  });
  Test("failed result receipt preserves retryable reason across lost packets",()=>{var r=new OutcomeReceipts();r.Record("id",false,"network path or coverage changed");Assert(r.TryGet("id",out var hit)&&!hit.Success&&RecoveryAttempt.Transient(hit.Reason),"reason lost");});
  Test("conflicting terminal results rejected and session clear removes receipts",()=>{var r=new OutcomeReceipts();r.Record("id",true,"ok");bool caught=false;try{r.Record("id",false,"no");}catch(InvalidOperationException){caught=true;}Assert(caught,"conflicting outcome");r.Clear();Assert(!r.TryGet("id",out _),"session receipt retained");});
  return passed;
 }
}
