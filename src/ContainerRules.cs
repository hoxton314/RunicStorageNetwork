using System;
using System.Collections.Generic;
using System.Linq;

namespace RunicStorageNetwork.Logic {
 // Configured container filter. Pure decision logic with no Unity or game types, so the
 // isolated tests exercise exactly the rules that the client and the coordinator apply.
 public sealed class ContainerRules {
  // The obliterator destroys whatever is placed in it; supplying it would consume resources.
  public const string DeniedPrefabDefault="piece_trashcan";
  // Component names, not prefab names, so modded machines built on the same components are
  // covered too. A name that no installed assembly uses simply never matches, so listing a
  // component that a given game version does not have costs nothing.
  public const string DeniedComponentDefault="Incinerator,Turret,Catapult,Vagon,Ship,ItemStand,ArmorStand,Smelter,CookingStation,Fermenter,Beehive,SapCollector";
  public const string ExcludedReason="excluded by configuration";
  public const string ExcludedTypeReason="excluded container type";
  readonly HashSet<string> allowed,denied,components;
  public ContainerRules(string allow,string deny,string denyComponents){
   allowed=Parse(allow);denied=Parse(deny);components=Parse(denyComponents);
  }
  public bool Restricted=>allowed.Count>0;
  public int DeniedCount=>denied.Count;
  public int DeniedComponentCount=>components.Count;
  // Players edit these by hand, so accept any common separator and ignore spacing and case.
  public static HashSet<string> Parse(string value){
   var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   if(string.IsNullOrEmpty(value))return set;
   foreach(string entry in value.Split(new[]{',',';','\n','\r','\t'},StringSplitOptions.RemoveEmptyEntries)){
    string name=entry.Trim();if(name.Length>0)set.Add(name);
   }
   return set;
  }
  // Exclusion always wins over inclusion: a denied prefab stays denied even if it is listed
  // in the allow list, so a mistake in one field cannot re-enable a destructive container.
  public string Verdict(string prefab,IEnumerable<string> componentNames){
   if(string.IsNullOrEmpty(prefab))return "unsupported prefab";
   if(denied.Contains(prefab))return ExcludedReason;
   if(components.Count>0&&componentNames!=null&&componentNames.Any(n=>n!=null&&components.Contains(n)))return ExcludedTypeReason;
   if(allowed.Count>0&&!allowed.Contains(prefab))return ExcludedReason;
   return null;
  }
  public string Summary=>"allow="+Describe(allowed,"every eligible container")+" deny="+Describe(denied,"none")+" denyComponents="+Describe(components,"none");
  static string Describe(HashSet<string> set,string empty)=>set.Count==0?empty:string.Join(",",set.OrderBy(s=>s,StringComparer.OrdinalIgnoreCase).ToArray());
 }
}
