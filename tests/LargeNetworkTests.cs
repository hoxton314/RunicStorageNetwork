using System;
using System.Linq;
using RunicStorageNetwork.Logic;

static class LargeNetworkTests {
 static int count;
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Test(string name,Action action){action();count++;Console.WriteLine("PASS large network "+name);}
 internal static int Run(){
  Test("5000 irrelevant chests do not participate",()=>{
   var stock=Enumerable.Range(0,5000).Select(i=>new Stock("stone"+i,"Stone",1,50)).Concat(new[]{new Stock("wood","Wood",1,10)});
   var plan=Planner.Plan(new[]{new Need("Wood",10)},stock,true);
   Check(SourceSelection.Sources(plan).SequenceEqual(new[]{"wood"}),"irrelevant chest selected");
  });
  Test("empty chests and fuller stack selection",()=>{
   var stock=Enumerable.Range(0,2000).Select(i=>new Stock("empty"+i,"Wood",1,0)).Concat(Enumerable.Range(0,100).Select(i=>new Stock("fragment"+i,"Wood",1,1))).Concat(new[]{new Stock("full","Wood",1,100)});
   var plan=Planner.Plan(new[]{new Need("Wood",70)},stock,true);
   Check(plan.Count==1&&plan[0].Source=="full"&&plan[0].Amount==70,"did not use fuller chest");
  });
  Test("player resources retain priority",()=>{
   var plan=Planner.Plan(new[]{new Need("Wood",10)},new[]{new Stock("chest","Wood",1,100),new Stock("player","Wood",1,3)},true);
   Check(plan[0].Source=="player"&&plan[0].Amount==3&&plan[1].Amount==7,"lost player priority");
  });
  Test("300 contributing chests and all-owner confirmation",()=>{
   var plan=Planner.Plan(new[]{new Need("Wood",300)},Enumerable.Range(0,300).Select(i=>new Stock("c"+i.ToString("D4"),"Wood",1,1)),true);
   var sources=SourceSelection.Sources(plan);Check(sources.Length==300,"old cap remains");
   var decision=new Decision("large",sources);foreach(string id in sources.Reverse())decision.Prepared(id);decision.Commit();
   foreach(string id in sources.Take(299)){decision.Paid(id);decision.Paid(id);}Check(decision.Phase==Phase.Committing,"completed without final owner");
   decision.Paid(sources.Last());Check(decision.Complete(),"large payment not completed");
  });
  Test("1024 entry boundary is per payment",()=>{
   var stock=Enumerable.Range(0,1100).Select(i=>new Stock("c"+i.ToString("D4"),"Wood",1,1)).ToArray();
   Check(SourceSelection.Sources(Planner.Plan(new[]{new Need("Wood",1024)},stock,true)).Length==1024,"boundary rejected");
   Check(SourceSelection.Sources(Planner.Plan(new[]{new Need("Wood",1025)},stock,true))==null,"oversize payment accepted");
   Check(SourceSelection.Sources(Planner.Plan(new[]{new Need("Wood",1)},stock,true)).Length==1,"network size limited");
   Check(stock.All(s=>s.Amount==1),"preview mutated stock");
  });
  Test("multiple ingredients deduplicate source reservations",()=>{
   var plan=Planner.Plan(new[]{new Need("Wood",10),new Need("Stone",5)},new[]{new Stock("a","Wood",1,10),new Stock("a","Stone",1,5)},true);
   Check(plan.Count==2&&SourceSelection.Sources(plan).Length==1,"duplicate physical reservation");
  });
  Test("ties deterministic and access and quality preserved",()=>{
   var stock=new[]{new Stock("denied","Wood",2,1000,false),new Stock("wrong","Wood",1,1000),new Stock("b","Wood",2,10),new Stock("a","Wood",2,10)};
   var plan=Planner.Plan(new[]{new Need("Wood",7,2)},stock,true);var reverse=Planner.Plan(new[]{new Need("Wood",7,2)},stock.Reverse(),true);
   Check(plan.Single().Source=="a"&&reverse.Single().Source=="a"&&plan[0].Quality==2,"access/quality/order broken");
  });
  Test("stale selected stock fails before payment",()=>{
   var preview=Planner.Plan(new[]{new Need("Wood",10)},new[]{new Stock("a","Wood",1,10)},true);Check(SourceSelection.Sources(preview).Length==1,"preview failed");
   Check(Planner.Plan(new[]{new Need("Wood",10)},new[]{new Stock("a","Wood",1,9)},true)==null,"stale quantity accepted");
  });
  Test("overlapping requirements cannot overdraw compact plan",()=>{
   Check(Planner.Plan(new[]{new Need("Wood",6),new Need("Wood",6)},new[]{new Stock("a","Wood",1,10)},true)==null,"double debit");
  });
  return count;
 }
}
