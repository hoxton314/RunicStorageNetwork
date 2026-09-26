using System;
using System.Collections.Generic;
using System.Linq;

namespace RunicStorageNetwork.Logic {
 // Preserve every candidate. Callers must disambiguate a shared name using recipe
 // content, never the order in which peers registered their recipes.
 public sealed class NameIndex<T> where T:class {
  readonly Dictionary<string,List<T>> byName=new Dictionary<string,List<T>>(StringComparer.Ordinal);
  readonly List<string> duplicates=new List<string>();
  public int Count=>byName.Count;
  public int Unnamed {get;private set;}
  public IEnumerable<string> Duplicates=>duplicates;
  public int DuplicateCount=>duplicates.Count;
  public bool Ambiguous(string name)=>name!=null&&duplicates.Contains(name);

  public void Add(string name,T value){
   if(value==null)return;
   if(string.IsNullOrEmpty(name)){Unnamed++;return;}
   if(!byName.TryGetValue(name,out var entries))byName[name]=entries=new List<T>();
   if(entries.Any(e=>ReferenceEquals(e,value)))return;
   entries.Add(value);
   if(entries.Count==2)duplicates.Add(name);
  }
  public IReadOnlyList<T> Candidates(string name)=>!string.IsNullOrEmpty(name)&&byName.TryGetValue(name,out var values)?values:Array.Empty<T>();
  public T Find(string name){var values=Candidates(name);return values.Count==1?values[0]:null;}

  public string Report {
   get {
    var parts=new List<string>{Count+" named"};
    if(Unnamed>0)parts.Add(Unnamed+" unnamed");
    if(duplicates.Count>0)parts.Add(duplicates.Count+" ambiguous: "+string.Join(", ",duplicates.OrderBy(d=>d,StringComparer.Ordinal).ToArray()));
    return string.Join("; ",parts.ToArray());
   }
  }
 }
}
