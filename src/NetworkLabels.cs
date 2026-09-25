using System.Text;

namespace RunicStorageNetwork.Logic {
 public static class NetworkLabels {
  public const int MaxLength=40;
  public static string Normalize(string value){
   var result=new StringBuilder();
   foreach(char c in value??""){
    if(result.Length>=MaxLength)break;
    // Names are plain text: prevent UI markup, localization tokens and multiline tooltips.
    if(c=='<'||c=='>'||c=='$'||char.IsControl(c))continue;
    result.Append(c);
   }
   if(result.Length>0&&char.IsHighSurrogate(result[result.Length-1]))result.Length--;
   return result.ToString().Trim();
  }
 }
}
