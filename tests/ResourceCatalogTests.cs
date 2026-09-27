using System;
using System.Linq;
using RunicStorageNetwork.Logic;
static class ResourceCatalogTests {
 static int passed;
 static void Check(bool value){if(!value)throw new Exception("resource index assertion");}
 static void Test(string name,Action action){action();passed++;Console.WriteLine("PASS resource index "+name);}
 internal static int Run(){
  Test("added item in previously empty storage is discoverable",()=>{var c=new ResourceCatalog();c.Replace("a",1,1,new Stock[0]);c.Replace("a",1,2,new[]{new Stock("a","Wood",1,50)});Check(c.Find(new[]{"Wood"}).Single().Amount==50);});
  Test("real removal updates only the source",()=>{var c=new ResourceCatalog();c.Replace("a",1,1,new[]{new Stock("a","Wood",1,10)});c.Replace("b",1,1,new[]{new Stock("b","Wood",1,20)});c.Replace("a",1,2,new Stock[0]);Check(c.Find(new[]{"Wood"}).Single().Source=="b");});
  Test("older revision cannot overwrite newer inventory",()=>{var c=new ResourceCatalog();c.Replace("a",1,3,new[]{new Stock("a","Wood",1,10)});Check(!c.Replace("a",1,2,new Stock[0])&&c.Find(new[]{"Wood"}).Count==1);});
  Test("previous owner epoch cannot erase a new owner snapshot",()=>{var c=new ResourceCatalog();c.Replace("a",2,1,new[]{new Stock("a","Wood",1,10)});Check(!c.Replace("a",1,999,new Stock[0]));});
  Test("duplicate snapshot does not change quantities",()=>{var c=new ResourceCatalog();c.Replace("a",1,1,new[]{new Stock("a","Wood",1,10)});Check(!c.Replace("a",1,1,new[]{new Stock("a","Wood",1,10)})&&c.Find(new[]{"Wood","Wood"}).Count==1);});
  Test("unrelated stock changes do not invalidate selected ingredients",()=>{var c=new ResourceCatalog();c.Replace("a",1,1,new[]{new Stock("a","Wood",1,10)});long stamp=c.Stamp(new[]{"Wood"});c.Replace("b",1,1,new[]{new Stock("b","Stone",1,10)});Check(c.Stamp(new[]{"Wood"})==stamp);});
  Test("unchanged snapshot preserves item revision",()=>{var c=new ResourceCatalog();c.Replace("a",1,1,new[]{new Stock("a","Wood",1,10)});int rev=c.Revision;Check(!c.Replace("a",1,2,new[]{new Stock("a","Wood",1,10)})&&c.Revision==rev);});
  Test("changing iron in the same chest does not invalidate wood recipes",()=>{var c=new ResourceCatalog();c.Replace("a",1,1,new[]{new Stock("a","Wood",1,10),new Stock("a","Iron",1,5)});long wood=c.Stamp(new[]{"Wood"}),iron=c.Stamp(new[]{"Iron"});c.Replace("a",1,2,new[]{new Stock("a","Wood",1,10),new Stock("a","Iron",1,2)});Check(c.Stamp(new[]{"Wood"})==wood&&c.Stamp(new[]{"Iron"})!=iron&&c.Find(new[]{"Iron"}).Single().Amount==2);});
  Test("unload removes reverse links",()=>{var c=new ResourceCatalog();c.Replace("a",1,1,new[]{new Stock("a","Wood",1,10)});long before=c.Stamp(new[]{"Wood"});c.Remove("a");Check(c.Find(new[]{"Wood"}).Count==0&&c.Stamp(new[]{"Wood"})!=before);});
  Test("quality stays distinct",()=>{var c=new ResourceCatalog();c.Replace("a",1,1,new[]{new Stock("a","Wood",1,10),new Stock("a","Wood",2,3)});Check(c.Find(new[]{"Wood"}).Count==2);});
  Test("5000 unrelated containers do not enter a wood query",()=>{var c=new ResourceCatalog();for(int i=0;i<5000;i++)c.Replace(i.ToString(),1,1,new[]{new Stock(i.ToString(),"Other"+i,1,10)});c.Replace("wood",1,1,new[]{new Stock("wood","Wood",1,50)});Check(c.Find(new[]{"Wood"}).Count==1);});
  Test("caller mutation cannot change the stored snapshot",()=>{var c=new ResourceCatalog();var s=new Stock("a","Wood",1,10);c.Replace("a",1,1,new[]{s});s.Amount=0;Check(c.Find(new[]{"Wood"}).Single().Amount==10);});
  Test("clearing a world removes all sources",()=>{var c=new ResourceCatalog();c.Replace("a",1,1,new[]{new Stock("a","Wood",1,10)});c.Clear();Check(c.Find(new[]{"Wood"}).Count==0);});
  return passed;
 }
}
