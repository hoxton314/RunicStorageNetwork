using System;
using System.Linq;
using RunicStorageNetwork.Logic;

static class MultiplayerTests {
 static int passed;
 static void Assert(bool value,string text){if(!value)throw new Exception(text);}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS multiplayer "+name);}
 internal static int Run(){
  Test("identity can be validated without a local Player instance",()=>Assert(SessionGuard.Check(true,20,20,123,123,false,true,false,true)==null,"valid remote actor"));
  Test("missing network record fails closed",()=>Assert(SessionGuard.Check(false,20,20,123,123,false,true,false,true)=="actor data unavailable","missing actor accepted"));
  Test("reconnected peer cannot use the previous session",()=>Assert(SessionGuard.Check(true,21,20,123,123,false,true,false,true)=="actor session mismatch","stale session accepted"));
  Test("sender cannot substitute another character",()=>Assert(SessionGuard.Check(true,20,20,123,123,false,true,false,false)=="sender does not own character","spoof accepted"));
  Test("player identity mismatch rejected",()=>Assert(SessionGuard.Check(true,20,20,123,456,false,true,false,true)=="actor identity mismatch","identity accepted"));
  Test("unsynchronized player ID rejected",()=>Assert(SessionGuard.Check(true,20,20,0,0,false,true,false,true)=="actor identity mismatch","zero identity"));
  Test("dead actor rejected",()=>Assert(SessionGuard.Check(true,20,20,123,123,true,true,false,true)=="actor dead","dead actor"));
  Test("host own character has no remote peer requirement",()=>Assert(SessionGuard.Check(true,20,20,123,123,false,true,true,false)==null,"local actor rejected"));
  Test("two crafters serialize shared sources until acknowledgement",()=>{
   var gate=new SourceGate();Assert(gate.TryAcquire("a",new[]{"wood","stone"}),"first acquire");
   Assert(!gate.TryAcquire("b",new[]{"stone"}),"overlapping operation");
   var payment=new Decision("a",new[]{"wood","stone"});payment.Prepared("wood");payment.Prepared("stone");payment.Commit();payment.Paid("wood");payment.Paid("stone");payment.Complete();
   Assert(!gate.TryAcquire("b",new[]{"stone"}),"completed output is not release acknowledgement");
   gate.Released("a","wood");Assert(!gate.TryAcquire("b",new[]{"stone"}),"partial acknowledgement");
   gate.Released("a","stone");Assert(gate.TryAcquire("b",new[]{"stone"}),"second operation remains blocked");
  });
  Test("failed acquisition holds no additional chests",()=>{
   var gate=new SourceGate();gate.TryAcquire("a",new[]{"shared"});Assert(!gate.TryAcquire("b",new[]{"free","shared"}),"conflict");Assert(gate.TryAcquire("c",new[]{"free"}),"partial acquisition leaked");
  });
  Test("unrelated sources can be held independently",()=>{var g=new SourceGate();Assert(g.TryAcquire("a",new[]{"x"})&&g.TryAcquire("b",new[]{"y"}),"independent conflict");});
  Test("duplicate and stale releases cannot unlock the next payment",()=>{
   var g=new SourceGate();g.TryAcquire("a",new[]{"x"});g.Released("other","x");Assert(g.Held("x"),"wrong operation release");
   g.Released("a","x");g.TryAcquire("b",new[]{"x"});g.Released("a","x");Assert(g.Held("x","a")&&!g.Held("x","b"),"late release unlocked next operation");
  });
  Test("validation failure cancels only its own holds",()=>{var g=new SourceGate();g.TryAcquire("a",new[]{"x","y"});g.TryAcquire("b",new[]{"z"});g.Cancel("a");Assert(!g.Held("x")&&!g.Held("y")&&g.Held("z"),"cancellation");});
  Test("uncertain payment retains reservations",()=>{var g=new SourceGate();g.TryAcquire("a",new[]{"x"});var d=new Decision("a",new[]{"x"});d.Prepared("x");d.Commit();d.Timeout();Assert(d.Phase==Phase.Uncertain&&!g.TryAcquire("b",new[]{"x"}),"uncertain debit released");});
  Test("world unload clears session reservations",()=>{var g=new SourceGate();g.TryAcquire("old",new[]{"x"});g.Clear();Assert(g.TryAcquire("new",new[]{"x"}),"old session retained");});
  Test("remote metadata graph works away from host and loses destroyed bridge",()=>{
   var nodes=new[]{new NetworkNode{Id="root",Network="net",Root=true,Confirmed=true,Position=new Point(6000,10,6000),Supply=20,Storage=20},new NetworkNode{Id="bridge",Network="net",Confirmed=true,Position=new Point(6045,10,6000),Supply=20,Storage=20},new NetworkNode{Id="end",Network="net",Confirmed=true,Position=new Point(6090,10,6000),Supply=20,Storage=20}};
   var graph=new NetworkGraph(nodes,50);Assert(graph.Choose(new Point(6095,10,6000),n=>true)=="net"&&graph.Covers("net",new Point(6100,10,6000),n=>true),"remote graph");
   nodes[1].Confirmed=false;graph=new NetworkGraph(nodes,50);Assert(!graph.Covers("net",new Point(6100,10,6000),n=>true),"denied/destroyed transit remained connected");
  });
  return passed;
 }
}
