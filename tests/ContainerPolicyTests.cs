using System;
using System.Linq;
using RunicStorageNetwork.Logic;

static class ContainerPolicyTests {
 static int passed;
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS container policy "+name);}
 static ContainerRules Defaults()=>new ContainerRules("",ContainerRules.DeniedPrefabDefault,ContainerRules.DeniedComponentDefault);
 static string[] Chest=>new[]{"Container","Piece","ZNetView","WearNTear"};

 internal static int Run(){
  Test("modded containers connect without configuration",()=>{
   var rules=Defaults();
   foreach(string prefab in new[]{"piece_chest_wood","piece_chest","piece_chest_blackmetal","piece_chest_barrel","SomeModdedCrate","jewelcrafting_chest"})
    Assert(rules.Verdict(prefab,Chest)==null,"rejected "+prefab);
   Assert(!rules.Restricted,"default configuration must not restrict");
  });
  Test("obliterator excluded by default prefab and component",()=>{
   var rules=Defaults();
   Assert(rules.Verdict("piece_trashcan",Chest)==ContainerRules.ExcludedReason,"prefab rule");
   Assert(rules.Verdict("modded_shredder",new[]{"Container","Piece","Incinerator"})==ContainerRules.ExcludedTypeReason,"component rule");
  });
  Test("component rule matches subclasses and ignores case",()=>{
   var rules=Defaults();
   Assert(rules.Verdict("modded_ballista",new[]{"Container","Piece","Turret","ModTurret"})==ContainerRules.ExcludedTypeReason,"turret base type");
   Assert(rules.Verdict("modded_cart",new[]{"container","piece","vagon"})==ContainerRules.ExcludedTypeReason,"case insensitive");
  });
  Test("allow list restricts to the listed prefabs",()=>{
   var rules=new ContainerRules("piece_chest_wood, piece_chest","",ContainerRules.DeniedComponentDefault);
   Assert(rules.Restricted,"allow list must restrict");
   Assert(rules.Verdict("piece_chest_wood",Chest)==null&&rules.Verdict("piece_chest",Chest)==null,"listed prefab rejected");
   Assert(rules.Verdict("piece_chest_blackmetal",Chest)==ContainerRules.ExcludedReason,"unlisted prefab accepted");
  });
  Test("exclusion wins over inclusion",()=>{
   var rules=new ContainerRules("piece_trashcan,piece_chest",ContainerRules.DeniedPrefabDefault,ContainerRules.DeniedComponentDefault);
   Assert(rules.Verdict("piece_trashcan",Chest)==ContainerRules.ExcludedReason,"denied prefab re-enabled by allow list");
   Assert(rules.Verdict("burner",new[]{"Container","Incinerator"})==ContainerRules.ExcludedTypeReason,"denied component re-enabled by allow list");
  });
  Test("hand written lists tolerate spacing, blanks and separators",()=>{
   var rules=new ContainerRules(" piece_chest_wood ,, \n piece_chest;piece_chest_barrel ","","");
   foreach(string prefab in new[]{"piece_chest_wood","piece_chest","piece_chest_barrel"})Assert(rules.Verdict(prefab,Chest)==null,"lost "+prefab);
   Assert(rules.Verdict("piece_chest_blackmetal",Chest)==ContainerRules.ExcludedReason,"empty entry widened the list");
   Assert(ContainerRules.Parse(null).Count==0&&ContainerRules.Parse("   ").Count==0,"blank list must be empty");
  });
  Test("cleared lists connect everything and keep nothing hidden",()=>{
   var rules=new ContainerRules("","","");
   Assert(rules.Verdict("piece_trashcan",Chest)==null&&rules.Verdict("anything",Chest)==null,"empty lists must not exclude");
   Assert(rules.Verdict(null,Chest)=="unsupported prefab"&&rules.Verdict("",Chest)=="unsupported prefab","unnamed prefab accepted");
  });
  Test("summary reports the effective lists",()=>{
   Assert(new ContainerRules("","","").Summary.Contains("every eligible container"),"default allow description");
   Assert(Defaults().Summary.Contains(ContainerRules.DeniedPrefabDefault),"denied prefab missing from summary");
   Assert(Defaults().DeniedComponentCount==ContainerRules.DeniedComponentDefault.Split(',').Length,"component defaults lost");
  });
  return passed;
 }
}
