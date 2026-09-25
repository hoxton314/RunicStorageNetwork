using System;
using System.Linq;
using RunicStorageNetwork.Logic;

static class CraftOfferTests {
 static int passed;
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS preflight "+name);}
 internal static int Run(){
  Test("cached stock does not confirm an offer",()=>Assert(!new OfferWindow().Ready(0),"unconfirmed craft enabled"));
  Test("deadline disables availability before server expiry",()=>{var w=new OfferWindow();w.Confirm(8);Assert(w.Ready(7.99)&&!w.Ready(8),"expired stock offered");});
  Test("late duplicate offer cannot extend its deadline",()=>{var w=new OfferWindow();w.Confirm(8);w.Confirm(100);Assert(!w.Ready(8),"replay extended reservation");});
  Test("cancellation wins against delayed confirmation",()=>{var w=new OfferWindow();w.Cancel();w.Confirm(8);Assert(!w.Ready(1)&&!w.Claim(1),"cancelled recipe revived");});
  Test("claim pins reservation through six-second multi craft",()=>{var w=new OfferWindow();w.Confirm(8);Assert(w.Claim(7)&&w.Ready(14)&&w.Consume(14),"animation invalidated paid intent");});
  Test("repeat clicks and claims cannot extend the pinned deadline",()=>{var w=new OfferWindow();w.Confirm(8);w.Claim(7);Assert(!w.Claim(26)&&!w.Ready(27),"unbounded claim");});
  Test("one offer cannot produce two accept operations",()=>{var w=new OfferWindow();w.Confirm(8);w.Claim(2);Assert(w.Consume(4)&&!w.Consume(4)&&!w.Ready(4),"duplicate accept");});
  Test("unclaimed and expired offers cannot be consumed",()=>{var w=new OfferWindow();w.Confirm(8);Assert(!w.Consume(2),"click skipped");w.Claim(2);Assert(!w.Consume(22),"expired claim accepted");});
  Test("cancel during animation prevents acceptance",()=>{var w=new OfferWindow();w.Confirm(8);w.Claim(1);w.Cancel();Assert(!w.Consume(3),"cancelled animation paid");});
  Test("owner preparation does not debit before explicit accept",()=>{var d=new Decision("offer",new[]{"a","b"});d.Prepared("a");d.Prepared("b");d.Paid("a");Assert(d.Phase==Phase.Prepared,"prepare advanced to payment");d.Commit();d.Paid("a");Assert(d.Phase==Phase.Committing,"partial payment accepted");d.Paid("b");Assert(d.Complete()&&!d.Complete(),"output duplication");});
  Test("two players cannot reserve the same last ingredient",()=>{var gate=new SourceGate();Assert(gate.TryAcquire("one",new[]{"chest"})&&!gate.TryAcquire("two",new[]{"chest"}),"double promise");gate.Released("one","chest");Assert(gate.TryAcquire("two",new[]{"chest"}),"release did not unblock second player");Assert(Planner.Plan(new[]{new Need("Iron",1)},new[]{new Stock("chest","Iron",1,0)})==null,"second player's stale stock accepted");});
  Test("unrelated inventories remain available during an offer",()=>{var gate=new SourceGate();Assert(gate.TryAcquire("one",new[]{"a"})&&gate.TryAcquire("two",new[]{"b"}),"global lock");});
  Test("last lost release acknowledgement blocks a conflicting new payment",()=>{var gate=new SourceGate();gate.TryAcquire("one",new[]{"a","b"});gate.Released("one","a");Assert(!gate.TryAcquire("two",new[]{"b"}),"unknown release guessed");gate.Released("one","b");Assert(gate.TryAcquire("two",new[]{"b"}),"confirmed release ignored");});
  Test("personal contribution changing invalidates a previously sufficient plan",()=>{var needs=new[]{new Need("Iron",2,1)};Assert(Planner.Plan(needs,new[]{new Stock("player","Iron",1,1)})==null,"personal deficit accepted");Assert(Planner.Plan(needs,new[]{new Stock("player","Iron",2,2)})==null,"quality substituted");});
  Test("maximum bonus capacity matches native accumulated loop",()=>{foreach(int multiplier in new[]{1,2,5,20,100}){int amount=3*multiplier,total=amount,bonus=0;for(int i=0;i<multiplier;i++){bonus+=2;total+=bonus;}Assert(CraftCapacity.Maximum(amount,multiplier,2)==total,"bonus capacity underestimated");}});
  Test("offer expiry retries only after confirmed refusal and within eight seconds",()=>{var r=new RecoveryAttempt(0);r.Sent();Assert(r.InFlight&&r.Retry("offer expired",1)&&!r.InFlight,"expiry not recoverable");Assert(!r.Retry("offer expired",8),"old 45 second retry remained");});
  return passed;
 }
}
