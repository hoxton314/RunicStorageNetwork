using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RunicStorageNetwork;

static class LocalizationTests {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 internal static int Run(){
  var en=TranslationCatalog.English;var ru=TranslationCatalog.Russian;
  Assert(en.Count>50&&en.Keys.OrderBy(k=>k).SequenceEqual(ru.Keys.OrderBy(k=>k)),"Language key coverage differs");
  foreach(var key in en.Keys){
   Assert(!string.IsNullOrWhiteSpace(en[key])&&!string.IsNullOrWhiteSpace(ru[key]),"Empty translation: "+key);
   Func<string,string> slots=s=>string.Join(",",Regex.Matches(s,@"\{\d+\}").Cast<Match>().Select(m=>m.Value).OrderBy(s2=>s2));
   Assert(slots(en[key])==slots(ru[key]),"Format arguments differ: "+key);
   foreach(string language in new[]{"English","Russian"})string.Format(CultureInfo.InvariantCulture,TranslationCatalog.Get(language,key),"node","network","state","hops");
  }
  Console.WriteLine("PASS localization complete EN/RU keys and format arguments ("+en.Count+" entries)");
  Assert(TranslationCatalog.Get("German","rsn_name")==en["rsn_name"]&&TranslationCatalog.Get(null,"rsn_name")==en["rsn_name"],"English fallback");
  Assert(TranslationCatalog.Get("Russian","rsn_name")=="Ядро сети хранилищ"&&TranslationCatalog.Get("English","rsn_name")=="Storage Network Core","Language selection");
  Console.WriteLine("PASS localization player language and English fallback");
  string key2=TranslationCatalog.ReasonKey("owner refused: commit refused: access denied");
  Assert(key2=="rsn_error_access"&&TranslationCatalog.Get("Russian",key2)!=TranslationCatalog.Get("English",key2),"Recipient-side reason translation");
  foreach(var reason in TranslationCatalog.ReasonKeys.Keys)Assert(en.ContainsKey(TranslationCatalog.ReasonKey(reason)),"Unmapped reason: "+reason);
  Assert(TranslationCatalog.ReasonKey("ok / source path changed before completion")=="rsn_error_path","Path suffix");
  Assert(TranslationCatalog.ReasonKey("Fresh stock insufficient: Wood")=="rsn_error_resources","Resource prefix");
  Assert(TranslationCatalog.ReasonKey("ok")!="rsn_available","Failed owner check must not display available");
  Console.WriteLine("PASS localization nested network refusals and dynamic resource reasons");
  Assert(TranslationCatalog.ReasonKey("Unexpected technical exception: secret details")=="rsn_error_unknown"&&TranslationCatalog.ReasonKey(null)=="rsn_error_unknown","Safe unknown reason fallback");
  Console.WriteLine("PASS localization unknown errors use translated fallback");
  return 4;
 }
}
