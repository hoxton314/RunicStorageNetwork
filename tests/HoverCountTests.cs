using System;
using System.Linq;
using RunicStorageNetwork.Logic;

static class HoverCountTests {
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 internal static int Run(){
  var count=new IncrementalCount<int>();int calls=0,frames=0;
  count.Begin(Enumerable.Range(0,10000).ToArray());
  while(count.Running){int before=calls;count.Step(i=>{calls++;return i%2==0;},32,()=>false);frames++;Assert(calls-before<=32,"Unbounded hover work");if(count.Running)Assert(!count.Value.HasValue,"Partial count shown as final");}
  Assert(count.Value==5000&&calls==10000&&frames==313,"Large count incorrect");
  Console.WriteLine("PASS hover 10000 containers bounded to 32 checks per frame; complete count only (313 simulated frames)");

  count.Reset();calls=0;count.Begin(new[]{1,2,3});
  count.Step(i=>{calls++;return true;},32,()=>true);
  Assert(calls==1&&count.Running&&!count.Value.HasValue,"Time budget ignored");
  count.Step(i=>{calls++;return true;},32,()=>false);
  Assert(calls==3&&count.Value==3,"Time-sliced continuation skipped/doubled items");
  Console.WriteLine("PASS hover elapsed-time budget yields after an indivisible check");

  count.Begin(new[]{0,1});count.Step(i=>i==1,1,()=>false);
  Assert(count.Value==3&&count.Running,"Previous completed result lost during refresh");
  count.Step(i=>i==1,1,()=>false);
  Assert(count.Value==1&&!count.Running,"Refresh result not published atomically");
  Console.WriteLine("PASS hover cached result remains available until replacement is complete");

  count.Begin(new[]{10,11,12});count.Step(i=>true,1,()=>false);count.Reset();
  Assert(!count.Running&&!count.Value.HasValue,"Old player/target/world result retained");
  count.Begin(new[]{99});count.Step(i=>false,32,()=>false);
  Assert(count.Value==0,"Cancelled work leaked into next context");
  Console.WriteLine("PASS hover context reset discards partial and completed results");

  calls=0;count.Begin(Array.Empty<int>());count.Step(i=>{calls++;return true;},32,()=>false);
  Assert(count.Value==0&&!count.Running&&calls==0,"Empty/disconnected storage requires work");
  Console.WriteLine("PASS hover empty network completes without access checks");

  count.Step(i=>{throw new Exception("Completed snapshot visited again");},32,()=>false);
  Assert(count.Value==0,"Completed result changed");
  Console.WriteLine("PASS hover completed work is released and never scanned again");
  return 6;
 }
}
