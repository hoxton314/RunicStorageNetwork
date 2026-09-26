#if RECIPE_RUNTIME_TESTS
// Production recipe lookup and operation requirement selection; no game or Harmony runtime.
#pragma warning disable 0649
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork;
using RunicStorageNetwork.Logic;
namespace UnityEngine {
 public class Object {public string name;public bool Destroyed;public static implicit operator bool(Object value)=>value!=null&&!value.Destroyed;}
 public static class Time {public static float unscaledTime;}
 public class GameObject:Object {internal Piece Piece;public T GetComponent<T>() where T:class=>Piece as T;}
}
class CraftingStation:UnityEngine.Object {public string m_name;}
class ItemDrop:UnityEngine.Object {public ItemData m_itemData=new ItemData();public class ItemData {public Shared m_shared=new Shared();}public class Shared {public int m_maxQuality=4;}}
class Piece:UnityEngine.Object {
 public bool m_enabled=true;public Requirement[] m_resources=Array.Empty<Requirement>();
 public class Requirement {
  public ItemDrop m_resItem;public int m_amount=1,m_amountPerLevel=1,m_extraAmountOnlyOneIngredient;public bool m_upgraderResource;
  public int GetAmount(int quality)=>quality==1?m_amount:(quality-1)*m_amountPerLevel;
 }
}
class Recipe:UnityEngine.Object {
 public bool m_enabled=true,m_noCraftOnlyUpgrade,m_requireOnlyOneIngredient;
 public ItemDrop m_item;public int m_amount=1,m_minStationLevel=1;public float m_qualityResultAmountMultiplier=1;
 public CraftingStation m_craftingStation,m_repairStation;public Piece.Requirement[] m_resources=Array.Empty<Piece.Requirement>();
}
class ObjectDB:UnityEngine.Object {public static ObjectDB instance;public List<Recipe> m_recipes=new List<Recipe>();}
class ZNetScene {public static ZNetScene instance=new ZNetScene();public GameObject GetPrefab(string target)=>null;}
namespace RunicStorageNetwork {
 static class Plugin {public static readonly List<string> Messages=new List<string>();internal static void Info(string text)=>Messages.Add(text);}
 static class BuildToolPolicy {internal static bool Eligible(GameObject prefab)=>true;}
 internal sealed partial class Operation {internal string Target;internal bool Build;internal int Quality=1,Multiplier=1;internal List<Need> Needs;}
 internal static partial class Stockroom {}
}
static class RecipeRuntimeTests {
 static int passed;
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS recipe runtime "+name);}
 static Recipe Make(string name="shared",string item="Gem",string ingredient="Stone",int amount=1)=>new Recipe{name=name,m_item=new ItemDrop{name=item},m_craftingStation=new CraftingStation{name="table",m_name="$table"},m_resources=new[]{new Piece.Requirement{m_resItem=new ItemDrop{name=ingredient},m_amount=amount}}};
 static void Reset(params Recipe[] recipes){ObjectDB.instance=new ObjectDB();ObjectDB.instance.m_recipes.AddRange(recipes);RecipeIndex.Invalidate();Plugin.Messages.Clear();Time.unscaledTime=0;}
 static void Reject(string key,string expected){Assert(RecipeIndex.Find(key,out string why)==null&&why==expected,"unexpected resolution: "+why);}
 static void Mutation(string name,Action<Recipe> mutate){Test(name,()=>{var r=Make();Reset(r);string key=RecipeIndex.Key(r);Assert(RecipeIndex.Find(key)==r,"initial lookup");mutate(r);Assert(!RecipeIndex.Matches(r,key),"old selection accepted");Reject(key,"recipe changed or disabled");Assert(RecipeIndex.Find(RecipeIndex.Key(r))==r,"updated recipe missing");});}
 public static int Main(){try{
  Test("initially disabled recipe becomes available without invalidation",()=>{var r=Make();r.m_enabled=false;Reset(r);string key=RecipeIndex.Key(r);Reject(key,"recipe changed or disabled");r.m_enabled=true;Assert(RecipeIndex.Find(key)==r,"enable missed");});
  Test("disabled recipe is not returned and can be restored",()=>{var r=Make();Reset(r);string key=RecipeIndex.Key(r);Assert(RecipeIndex.Find(key)==r,"initial");r.m_enabled=false;Reject(key,"recipe changed or disabled");r.m_enabled=true;Assert(RecipeIndex.Find(key)==r,"restore");});
  Test("late registration",()=>{Reset();var r=Make();string key=RecipeIndex.Key(r);Reject(key,"recipe not registered");ObjectDB.instance.m_recipes.Add(r);Assert(RecipeIndex.Find(key)==r,"late registration");});
  Test("removed recipe never resolves",()=>{var r=Make();Reset(r);string key=RecipeIndex.Key(r);RecipeIndex.Find(key);ObjectDB.instance.m_recipes.Clear();Reject(key,"recipe not registered");});
  Test("replacement with unchanged list size resolves the new object",()=>{var r=Make();Reset(r);string key=RecipeIndex.Key(r);RecipeIndex.Find(key);var next=Make();ObjectDB.instance.m_recipes[0]=next;Assert(RecipeIndex.Find(key)==next,"stale reference");});
  Test("list replacement with unchanged count",()=>{var r=Make();Reset(r);string key=RecipeIndex.Key(r);RecipeIndex.Find(key);var next=Make();ObjectDB.instance.m_recipes=new List<Recipe>{next};Assert(RecipeIndex.Find(key)==next,"stale list");});
  Test("renamed recipe",()=>{var r=Make();Reset(r);string key=RecipeIndex.Key(r);RecipeIndex.Find(key);r.name="renamed";Reject(key,"recipe not registered");Assert(RecipeIndex.Find(RecipeIndex.Key(r))==r,"rename missing");});
  Test("destroyed recipe",()=>{var r=Make();Reset(r);string key=RecipeIndex.Key(r);RecipeIndex.Find(key);r.Destroyed=true;Reject(key,"recipe not registered");});
  Test("database change and missing database",()=>{var r=Make();Reset(r);string key=RecipeIndex.Key(r);RecipeIndex.Find(key);ObjectDB.instance=null;Reject(key,"recipe database unavailable");var next=Make();ObjectDB.instance=new ObjectDB{m_recipes=new List<Recipe>{next}};Assert(RecipeIndex.Find(key)==next,"old database");});
  Test("opposite peer order selects the requested ingredient variant",()=>{Recipe a=Make(ingredient:"Stone"),b=Make(ingredient:"Shards");Reset(a,b);string key=RecipeIndex.Key(b);Assert(RecipeIndex.Find(key)==b,"client picked first");Recipe remoteA=Make(ingredient:"Stone"),remoteB=Make(ingredient:"Shards");Reset(remoteB,remoteA);Assert(RecipeIndex.Find(key)==remoteB,"peer order changed recipe");});
  Test("single and batch variants",()=>{Recipe a=Make(),b=Make(amount:5);b.m_amount=5;Reset(a,b);Assert(RecipeIndex.Find(RecipeIndex.Key(a))==a&&RecipeIndex.Find(RecipeIndex.Key(b))==b,"batch mixed up");});
  Test("otherwise identical custom station variants",()=>{Recipe a=Make(),b=Make();b.m_craftingStation=new CraftingStation{name="other",m_name="$other"};Reset(a,b);Assert(RecipeIndex.Find(RecipeIndex.Key(a))==a&&RecipeIndex.Find(RecipeIndex.Key(b))==b,"station mixed up");});
  Test("identical enabled definitions are rejected rather than picking first",()=>{Recipe a=Make(),b=Make();Reset(a,b);Reject(RecipeIndex.Key(a),"recipe identity ambiguous");b.m_enabled=false;Assert(RecipeIndex.Find(RecipeIndex.Key(a))==a,"disabled twin blocks valid recipe");});
  Test("duplicate reference is not an ambiguity",()=>{var r=Make();Reset(r,r);Assert(RecipeIndex.Find(RecipeIndex.Key(r))==r,"same reference rejected");});
  Test("unnamed registered variants use content identity",()=>{Recipe a=Make(name:"",ingredient:"Wood"),b=Make(name:"",ingredient:"Iron");Reset(a,b);Assert(RecipeIndex.Find(RecipeIndex.Key(a))==a&&RecipeIndex.Find(RecipeIndex.Key(b))==b,"unnamed variant lost");});
  Test("unregistered temporary recipe cannot request payment",()=>{var r=Make();Reset();Reject(RecipeIndex.Key(r),"recipe not registered");});
  Test("legacy name and malformed key fail safely",()=>{Reset(Make());Reject("shared","recipe identity missing or unsupported");Reject(null,"recipe identity missing or unsupported");Reject("rsn1:bad","recipe identity missing or unsupported");});
  Mutation("ingredient quantity change",r=>r.m_resources[0].m_amount++);
  Mutation("ingredient identity change",r=>r.m_resources[0].m_resItem.name="Iron");
  Mutation("upgrade cost change",r=>r.m_resources[0].m_amountPerLevel++);
  Mutation("alternate output bonus change",r=>r.m_resources[0].m_extraAmountOnlyOneIngredient++);
  Mutation("upgrader resource flag change",r=>r.m_resources[0].m_upgraderResource=true);
  Mutation("ingredient array replacement",r=>r.m_resources=new[]{new Piece.Requirement{m_resItem=new ItemDrop{name="Iron"},m_amount=12}});
  Mutation("output quantity change",r=>r.m_amount=5);
  Mutation("output item change",r=>r.m_item=new ItemDrop{name="OtherGem"});
  Mutation("station level change",r=>r.m_minStationLevel++);
  Mutation("repair station change",r=>r.m_repairStation=new CraftingStation{name="repair",m_name="$repair"});
  Mutation("alternate ingredient mode change",r=>r.m_requireOnlyOneIngredient=true);
  Mutation("output quality multiplier change",r=>r.m_qualityResultAmountMultiplier=2);
  Mutation("upgrade only flag change",r=>r.m_noCraftOnlyUpgrade=true);
  Test("alternate ingredient order is significant",()=>{var r=Make();r.m_resources=new[]{r.m_resources[0],new Piece.Requirement{m_resItem=new ItemDrop{name="Iron"}}};Reset(r);string key=RecipeIndex.Key(r);Array.Reverse(r.m_resources);Assert(RecipeIndex.Key(r)!=key,"order ignored");});
  Test("keys do not depend on locale and fit the existing wire limit",()=>{var r=Make(name:new string('x',300));var prior=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new CultureInfo("ru-RU");string key=RecipeIndex.Key(r);CultureInfo.CurrentCulture=new CultureInfo("en-US");Assert(RecipeIndex.Key(r)==key&&key.Length<=160,"locale or length");}finally{CultureInfo.CurrentCulture=prior;}});
  Test("missing recipe diagnostics work without debug and are rate limited",()=>{Reset();string key=RecipeIndex.Key(Make());RecipeIndex.Find(key);RecipeIndex.Find(key);Assert(Plugin.Messages.Count==1&&Plugin.Messages[0].Contains("recipe not registered"),"missing/noisy diagnostic");Time.unscaledTime=31;RecipeIndex.Find(key);Assert(Plugin.Messages.Count==2,"diagnostic never repeats");});
  Test("operation reads the selected duplicate's costs on every peer",()=>{Recipe a=Make(ingredient:"Stone"),b=Make(ingredient:"Shards",amount:12);Reset(a,b);var op=new Operation{Target=RecipeIndex.Key(b)};Assert(op.ReadRequirements(out _)&&op.Needs.Single().Item=="Shards"&&op.Needs.Single().Amount==12,"wrong requirements");ObjectDB.instance.m_recipes.Reverse();Assert(op.ReadRequirements(out _)&&op.Needs.Single().Item=="Shards","peer order");Assert(op.SelectNeeds(new List<Stock>{new Stock("chest","Shards",1,12)}),"selected variant cannot be planned");});
  Test("changed recipe rejects an already prepared operation",()=>{var r=Make(amount:1);Reset(r);var op=new Operation{Target=RecipeIndex.Key(r)};Assert(op.ReadRequirements(out _),"initial requirement read");r.m_resources[0].m_amount=10;Assert(!op.SelectNeeds(new List<Stock>{new Stock("chest","Stone",1,100)}),"old plan used new recipe");Assert(!op.ReadRequirements(out _),"old operation still accepted");var next=new Operation{Target=RecipeIndex.Key(r)};Assert(next.ReadRequirements(out _)&&next.Needs.Single().Amount==10,"new operation not refreshed");});
  Test("disabled first duplicate does not supply the wrong ingredients",()=>{Recipe a=Make(ingredient:"Wood"),b=Make(ingredient:"Iron");a.m_enabled=false;Reset(a,b);var op=new Operation{Target=RecipeIndex.Key(b)};Assert(op.ReadRequirements(out _)&&op.Needs.Single().Item=="Iron","disabled recipe won");});
  Console.WriteLine("RESULT "+passed+" recipe runtime tests passed; game types are stand-ins, not in-game compatibility verification.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
#endif
