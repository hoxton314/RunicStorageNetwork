using System;
using System.Linq;
using RunicStorageNetwork.Logic;

static class CraftOverviewTests {
 static int passed;
 static void Assert(bool value,string why){if(!value)throw new Exception(why);}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS overview "+name);}
 static StockCatalog<Stock> Make(params Stock[] items){var c=new StockCatalog<Stock>();c.Begin(items);c.Step(s=>new[]{s},10000,()=>false);return c;}
 static int Count(StockCatalog<Stock> c,string name)=>c.ForItems(new[]{name}).Sum(s=>s.Amount);
 internal static int Run(){
  Test("95 chests all contribute without publishing a partial scan",()=>{
   var c=new StockCatalog<Stock>();c.Begin(Enumerable.Range(0,95).Select(i=>new Stock("chest"+i,"Wood",1,2)).ToArray());int frames=0;
   while(c.Running){c.Step(s=>new[]{s},16,()=>false);frames++;if(c.Running)Assert(!c.Ready&&Count(c,"Wood")==0,"partial count published");}
   Assert(frames==6&&Count(c,"Wood")==190,"95 chests truncated");
  });
  Test("missing leather does not hide 50 wood in an incomplete recipe",()=>{
   var c=Make(new Stock("a","Wood",1,50));var required=new[]{new Need("Wood",10,1),new Need("LeatherScraps",2,1)};
   Assert(Planner.Plan(required,c.ForItems(required.Select(n=>n.Item)))==null,"incomplete recipe craftable");Assert(Count(c,"Wood")==50,"existing ingredient hidden");
  });
  Test("switching recipes never narrows the overall browsing stock",()=>{
   var c=Make(new Stock("a","Wood",1,50),new Stock("b","Iron",1,40));
   Assert(c.ForItems(new[]{"Wood"}).Count==1&&Count(c,"Iron")==40&&Count(c,"Wood")==50,"selected recipe filtered catalogue");
  });
  Test("overview shows total stock instead of a recipe's ten item allocation",()=>{var c=Make(new Stock("a","Wood",1,50));var plan=Planner.Plan(new[]{new Need("Wood",10,1)},c.ForItems(new[]{"Wood"}));Assert(plan.Sum(d=>d.Amount)==10&&Count(c,"Wood")==50,"plan replaced stock count");});
  Test("other player's consumption replaces 50 with 30 immediately",()=>{
   var c=Make(new Stock("a","Wood",1,50),new Stock("a","Iron",1,40));c.Replace("a",new[]{"Wood"},new[]{new Stock("a","Wood",1,30)});
   Assert(Count(c,"Wood")==30&&Count(c,"Iron")==40,"scoped owner update wrong");
  });
  Test("empty owner reply removes stale positive quantities of all qualities",()=>{
   var c=Make(new Stock("a","Wood",1,50),new Stock("a","Wood",2,3),new Stock("b","Wood",1,7));
   c.Replace("a",new[]{"Wood"},new[]{new Stock("a","Wood",1,0)});Assert(Count(c,"Wood")==7,"zero failed to remove stale stock");
  });
  Test("owner correction survives an already running stale scan",()=>{
   var c=Make(new Stock("a","Wood",1,50));c.Begin(new[]{new Stock("a","Wood",1,50)});
   c.Replace("a",new[]{"Wood"},new[]{new Stock("a","Wood",1,30)});c.Step(s=>new[]{s},16,()=>false);Assert(Count(c,"Wood")==30,"stale scan restored 50");
  });
  Test("zero correction cannot be resurrected by a stale in-progress scan",()=>{
   var c=Make(new Stock("a","Wood",1,50));c.Begin(new[]{new Stock("a","Wood",1,50)});c.Replace("a",new[]{"Wood"},new Stock[0]);c.Step(s=>new[]{s},16,()=>false);Assert(Count(c,"Wood")==0,"stale stock resurrected");
  });
  Test("next completed scan may discover resources deposited later",()=>{
   var c=Make(new Stock("a","Wood",1,30));c.Begin(new[]{new Stock("a","Wood",1,60)});c.Step(s=>new[]{s},16,()=>false);Assert(Count(c,"Wood")==60,"new resources ignored");
  });
  Test("duplicate coverage does not double source counts",()=>{var c=Make(new Stock("a","Wood",1,50),new Stock("a","Wood",1,50));Assert(Count(c,"Wood")==50,"double counted");});
  Test("time budget yields while keeping the previous complete overview",()=>{
   var c=Make(new Stock("a","Wood",1,50));c.Begin(new[]{new Stock("a","Wood",1,30),new Stock("b","Wood",1,10)});c.Step(s=>new[]{s},16,()=>true);
   Assert(c.Running&&Count(c,"Wood")==50,"partial refresh replaced overview");c.Step(s=>new[]{s},16,()=>true);Assert(Count(c,"Wood")==40,"refresh incomplete");
  });
  Test("unchanged owner snapshots do not trigger list repaint",()=>{var c=Make(new Stock("a","Wood",1,50));int revision=c.Revision;c.Replace("a",new[]{"Wood"},new[]{new Stock("a","Wood",1,50)});Assert(c.Revision==revision,"unnecessary redraw");});
  Test("5000 source overview has no transaction packet-size cap",()=>{var c=Make(Enumerable.Range(0,5000).Select(i=>new Stock("chest"+i,"Wood",1,1)).ToArray());Assert(Count(c,"Wood")==5000,"payment cap leaked into browsing");});
  Test("closing station discards its entire browsing context",()=>{var c=Make(new Stock("a","Wood",1,50));c.Begin(new[]{new Stock("a","Wood",1,50)});c.Clear();Assert(!c.Running&&!c.Ready&&Count(c,"Wood")==0,"old station leaked");});
  return passed;
 }
}
