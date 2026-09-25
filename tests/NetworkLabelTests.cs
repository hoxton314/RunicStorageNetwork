using System;
using RunicStorageNetwork.Logic;

static class NetworkLabelTests {
 internal static int Run(){
  int passed=0;
  Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS network label "+name);};
  check(NetworkLabels.Normalize(null)==""&&NetworkLabels.Normalize(" \t\r\n")=="","unnamed and cleared networks stay unnamed");
  check(NetworkLabels.Normalize("  Основная база  ")=="Основная база","Russian names and trimmed spaces");
  check(NetworkLabels.Normalize("Meadows storage")=="Meadows storage","English names");
  check(NetworkLabels.Normalize("<b>$KEY_Use</b>\n\t").IndexOfAny(new[]{'<','>','$','\n','\t'})<0,"names cannot inject markup, localization or new lines");
  check(NetworkLabels.Normalize(new string('x',80)).Length==NetworkLabels.MaxLength,"long names limited");
  string boundary=NetworkLabels.Normalize(new string('x',39)+"\ud83d\ude00");
  check(boundary.Length==39,"length limit does not split surrogate pairs");
  check(NetworkLabels.Normalize("Base \ud83d\ude00")=="Base \ud83d\ude00","complete surrogate pairs preserved");
  return passed;
 }
}
