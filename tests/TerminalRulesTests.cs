using System;
using System.Linq;
using RunicStorageNetwork.Logic;
static class TerminalRulesTests {
 static int passed;
 static void Check(bool value){if(!value)throw new Exception("terminal rule assertion");}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS terminal "+name);}
 internal static int Run(){
  Test("only positive available quantities are accepted",()=>{Check(TerminalRules.Quantity("50",100,out int n)&&n==50);foreach(string s in new[]{"0","-1","51","1.5","999999999999",""})Check(!TerminalRules.Quantity(s,50,out _));});
  Test("quantity is capped per operation",()=>Check(!TerminalRules.Quantity("10001",20000,out _)));
  Test("search is case insensitive and trims whitespace",()=>{Check(TerminalRules.Search("Древесина"," ДРЕВ "));Check(TerminalRules.Search("Wood","wood"));Check(!TerminalRules.Search("Stone","wood"));Check(TerminalRules.Search("Wood",""));});
  Test("grid keeps four rows for empty and short searches",()=>{Check(TerminalGrid.Height(0)==360);Check(TerminalGrid.Height(28)==360);Check(TerminalGrid.Height(29)==452);Check(TerminalGrid.ClampOffset(500,3)==0);});
  Test("virtual grid covers the last partial row in a large catalogue",()=>{int count=1001;float offset=TerminalGrid.ClampOffset(float.MaxValue,count);int first=TerminalGrid.FirstIndex(offset,count);Check(first%7==0&&first<=count-1&&first+35>count-1);Check(TerminalGrid.FirstIndex(-100,count)==0);});
  Test("new resources above the viewport keep the previous top resource visible",()=>{var before=Enumerable.Range(0,100).Select(i=>i.ToString()).ToArray();var after=Enumerable.Range(200,7).Select(i=>i.ToString()).Concat(before).ToArray();Check(TerminalGrid.PreserveOffset(before,after,193)==285);});
  Test("depleted resources clamp scrolling without leaving an empty page",()=>{var before=Enumerable.Range(0,100).Select(i=>i.ToString()).ToArray();Check(TerminalGrid.PreserveOffset(before,before.Take(7).ToArray(),920)==0);Check(TerminalGrid.PreserveOffset(before,Array.Empty<string>(),920)==0);});
  Test("parcel totals must exactly equal the paid sources",()=>{var plan=new[]{new Debit("a","Wood",1,10)};Check(TerminalRules.Matches(new[]{new Stock("a","Wood",1,4),new Stock("a","Wood",1,6)},plan));Check(!TerminalRules.Matches(new[]{new Stock("a","Wood",1,9)},plan));Check(!TerminalRules.Matches(new[]{new Stock("b","Wood",1,10)},plan));Check(!TerminalRules.Matches(new[]{new Stock("a","Wood",2,10)},plan));});
  Test("never withdraws from personal inventory",()=>Check(!TerminalRules.Matches(new[]{new Stock("player","Wood",1,10)},new[]{new Debit("player","Wood",1,10)})));
  Test("matching stack fills before a new slot",()=>{var slots=new[]{new DeliveryPlanner.Stack("wood",45,50),null};var plan=DeliveryPlanner.Plan(slots,new[]{new DeliveryPlanner.Stack("wood",10,50)});Check(plan.Count==2&&plan[0].Amount==5&&plan[1].Amount==5&&slots[0].Amount==45);});
  Test("full inventory still accepts a matching partial stack",()=>Check(DeliveryPlanner.Plan(new[]{new DeliveryPlanner.Stack("wood",45,50)},new[]{new DeliveryPlanner.Stack("wood",5,50)})!=null));
  Test("same prefab with different metadata is not merged",()=>Check(DeliveryPlanner.Plan(new[]{new DeliveryPlanner.Stack("wood/customA",1,50)},new[]{new DeliveryPlanner.Stack("wood/customB",1,50)})==null));
  Test("insufficient space fails before any mutation",()=>Check(DeliveryPlanner.Plan(new[]{new DeliveryPlanner.Stack("wood",49,50)},new[]{new DeliveryPlanner.Stack("wood",2,50)})==null));
  Test("parcels from separate chests can share one new stack",()=>{var plan=DeliveryPlanner.Plan(new DeliveryPlanner.Stack[1],new[]{new DeliveryPlanner.Stack("wood",20,50),new DeliveryPlanner.Stack("wood",30,50)});Check(plan.Count==2&&plan.All(s=>s.Slot==0));});
  Test("invalid parcel sizes are rejected",()=>Check(DeliveryPlanner.Plan(new DeliveryPlanner.Stack[2],new[]{new DeliveryPlanner.Stack("wood",51,50)})==null));
  return passed;
 }
}
