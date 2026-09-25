using System.Globalization;
using Jotunn.Managers;
namespace RunicStorageNetwork {
 internal static class RsnLocalization {
  internal static void Add(){var l=LocalizationManager.Instance.GetLocalization();l.AddTranslation("English",TranslationCatalog.English);l.AddTranslation("Russian",TranslationCatalog.Russian);}
  internal static string Text(string key,params object[] args){
   string language=Localization.instance!=null?Localization.instance.GetSelectedLanguage():"English";
   string text=TranslationCatalog.Get(language,"rsn_"+key);
   return args.Length==0?text:string.Format(CultureInfo.GetCultureInfo(language=="Russian"?"ru-RU":"en-US"),text,args);
  }
  // Reasons remain stable on the wire and in logs. The recipient chooses the language.
  internal static string Reason(string reason)=>Text(TranslationCatalog.ReasonKey(reason).Substring(4));
 }
}
