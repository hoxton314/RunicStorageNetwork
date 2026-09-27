using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 // Cache membership, not eligibility or recipe content. ObjectDB has no public
 // recipe-list revision: compare references/names without rebuilding unchanged buckets.
 // Only candidates for the requested name are fingerprinted, not the whole database.
 internal static class RecipeIndex {
  const string Prefix="rsn1:";
  static NameIndex<Recipe> index=new NameIndex<Recipe>();
  static ObjectDB catalogued;
  static Recipe[] members=Array.Empty<Recipe>();
  static string[] names=Array.Empty<string>();
  static readonly FieldInfo listVersion=typeof(List<Recipe>).GetField("_version",BindingFlags.Instance|BindingFlags.NonPublic);
  static List<Recipe> recipeList;static int version;static float nextAudit;
  static readonly Dictionary<string,float> reported=new Dictionary<string,float>(StringComparer.Ordinal);
  internal static void Invalidate(){catalogued=null;members=Array.Empty<Recipe>();names=Array.Empty<string>();index=new NameIndex<Recipe>();reported.Clear();}

  static bool Ready(bool audit=false){
   var db=ObjectDB.instance;if(!db||db.m_recipes==null)return false;
   int currentVersion=listVersion==null?0:(int)listVersion.GetValue(db.m_recipes);
   bool changed=catalogued!=db||recipeList!=db.m_recipes||members.Length!=db.m_recipes.Count||currentVersion!=version;
   if(!changed&&!audit)return true;
   for(int i=0;!changed&&i<members.Length;i++){
    var recipe=db.m_recipes[i];
    changed=!ReferenceEquals(members[i],recipe)||names[i]!=(recipe?recipe.name:null);
   }
   if(changed){
    catalogued=db;recipeList=db.m_recipes;version=currentVersion;members=db.m_recipes.ToArray();names=new string[members.Length];index=new NameIndex<Recipe>();
    for(int i=0;i<members.Length;i++){
     var recipe=members[i];names[i]=recipe?recipe.name:null;
     // Include disabled and unnamed registered recipes. Their eligibility is live.
     if(recipe)index.Add(NameKey(recipe.name),recipe);
    }
   }
   return true;
  }
  internal static void Ensure(){Ready(true);}
  // Catches in-place renames by mods that do not change List's version. This
  // checks membership only; no recipe/resource fingerprints are recomputed.
  internal static void Background(){if(Time.unscaledTime<nextAudit)return;nextAudit=Time.unscaledTime+2;Ready(true);}
  internal static string Key(Recipe recipe)=>recipe?Prefix+NameKey(recipe.name)+":"+Fingerprint(recipe):"";
  internal static bool Matches(Recipe recipe,string key)=>recipe&&recipe.m_enabled&&Key(recipe)==key;
  static bool Parts(string key,out string bucket,out string fingerprint){
   bucket=fingerprint=null;
   if(key==null||key.Length!=134||!key.StartsWith(Prefix,StringComparison.Ordinal)||key[69]!=':')return false;
   bucket=key.Substring(5,64);fingerprint=key.Substring(70,64);return true;
  }
  internal static Recipe Find(string key)=>Find(key,out _);
  internal static Recipe Find(string key,out string reason)=>Find(key,out reason,true);
  static Recipe Find(string key,out string reason,bool retry){
   Recipe found=null;reason="recipe unavailable";
   if(!Ready())reason="recipe database unavailable";
   else if(!Parts(key,out string bucket,out string fingerprint))reason="recipe identity missing or unsupported";
   else {
    var candidates=index.Candidates(bucket);
    reason=candidates.Count==0?"recipe not registered":"recipe changed or disabled";
    foreach(var recipe in candidates){
     if(!recipe||!recipe.m_enabled||!recipe.m_item||Fingerprint(recipe)!=fingerprint)continue;
     if(found){reason="recipe identity ambiguous";Report(key,reason);return null;}
     found=recipe;
    }
    if(found){reason="ok";return found;}
   }
   if(retry&&reason!="recipe database unavailable"&&reason!="recipe identity missing or unsupported"){
    Ready(true);return Find(key,out reason,false);
   }
   Report(key,reason);return null;
  }
  internal static void Report(string key,string reason){
   string id=(key??"")+"/"+reason;float now=Time.unscaledTime;
   if(reported.TryGetValue(id,out float at)&&now-at<30)return;
   if(reported.Count>=128)reported.Clear();reported[id]=now;
   string label=key??"<empty>";
   if(Parts(key,out string bucket,out _)&&index.Candidates(bucket).Count>0&&index.Candidates(bucket)[0])label=index.Candidates(bucket)[0].name+" ["+key+"]";
   Plugin.Info("Recipe lookup refused: "+reason+"; target="+label+". Check that peers use matching mod versions and recipe settings.");
  }
  static string NameKey(string name)=>Hash(Encoding.UTF8.GetBytes(name??""));
  static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
  static void Station(BinaryWriter writer,CraftingStation station){writer.Write(station?station.name:"");writer.Write(station?station.m_name:"");}
  static string Fingerprint(Recipe recipe){
   // BinaryWriter length-prefixes strings and writes numbers independently of locale.
   // Resource order matters for recipes accepting any one of several ingredients.
   using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){
    writer.Write(recipe.GetType().FullName);writer.Write(recipe.name??"");writer.Write(recipe.m_item?recipe.m_item.name:"");
    writer.Write(recipe.m_amount);writer.Write(recipe.m_qualityResultAmountMultiplier);writer.Write(recipe.m_noCraftOnlyUpgrade);
    Station(writer,recipe.m_craftingStation);Station(writer,recipe.m_repairStation);
    writer.Write(recipe.m_minStationLevel);writer.Write(recipe.m_requireOnlyOneIngredient);
    var requirements=recipe.m_resources;writer.Write(requirements?.Length??-1);
    if(requirements!=null)foreach(var req in requirements){
     writer.Write(req!=null);if(req==null)continue;
     writer.Write(req.m_resItem?req.m_resItem.name:"");writer.Write(req.m_amount);writer.Write(req.m_amountPerLevel);
     writer.Write(req.m_extraAmountOnlyOneIngredient);writer.Write(req.m_upgraderResource);
    }
    writer.Flush();return Hash(stream.ToArray());
   }
  }
 }
}
