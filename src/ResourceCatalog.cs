using System;
using System.Collections.Generic;
using System.Linq;

namespace RunicStorageNetwork.Logic {
 // Reverse index: replacing one chest touches only the items in that chest.
 // Epoch changes invalidate responses from a previous owner/loaded instance.
 internal sealed class ResourceCatalog {
  sealed class Entry {internal long Epoch,Revision;internal List<Stock> Items;}
  readonly Dictionary<string,Entry> sources=new Dictionary<string,Entry>(StringComparer.Ordinal);
  readonly Dictionary<string,Dictionary<string,List<Stock>>> items=new Dictionary<string,Dictionary<string,List<Stock>>>(StringComparer.Ordinal);
  readonly Dictionary<string,int> versions=new Dictionary<string,int>(StringComparer.Ordinal);
  internal int Revision {get;private set;}
  internal void Clear(){sources.Clear();items.Clear();versions.Clear();Revision++;}
  internal bool Replace(string source,long epoch,long revision,IEnumerable<Stock> values){
   if(sources.TryGetValue(source,out var old)&&(epoch<old.Epoch||epoch==old.Epoch&&revision<=old.Revision))return false;
   var snapshot=values.Where(s=>s.Source==source&&s.Amount>0&&s.Allowed).Select(s=>new Stock(source,s.Item,s.Quality,s.Amount)).ToList();
   var before=(old?.Items??new List<Stock>()).ToLookup(s=>s.Item,StringComparer.Ordinal);
   var after=snapshot.ToLookup(s=>s.Item,StringComparer.Ordinal);
   var changed=before.Select(g=>g.Key).Union(after.Select(g=>g.Key),StringComparer.Ordinal)
    .Where(name=>old==null||old.Epoch!=epoch||before[name].Count()!=after[name].Count()||before[name].Any(a=>!after[name].Any(b=>a.Quality==b.Quality&&a.Amount==b.Amount))).ToArray();
   sources[source]=new Entry{Epoch=epoch,Revision=revision,Items=snapshot};
   if(changed.Length==0)return false;
   Revision++;
   foreach(string name in changed){
    if(!items.TryGetValue(name,out var bucket))items[name]=bucket=new Dictionary<string,List<Stock>>(StringComparer.Ordinal);
    bucket.Remove(source);var valuesForItem=after[name].ToList();
    if(valuesForItem.Count>0)bucket[source]=valuesForItem;
    if(bucket.Count==0)items.Remove(name);
    versions[name]=Revision;
   }
   return true;
  }
  internal void Remove(string source){
   if(!sources.TryGetValue(source,out var old))return;
   foreach(string name in old.Items.Select(s=>s.Item).Distinct())if(items.TryGetValue(name,out var bucket)){bucket.Remove(source);if(bucket.Count==0)items.Remove(name);}
   sources.Remove(source);Revision++;foreach(var name in old.Items.Select(s=>s.Item))versions[name]=Revision;
  }
  internal List<Stock> Find(IEnumerable<string> names){
   var result=new List<Stock>();
   foreach(string name in names.Distinct(StringComparer.Ordinal))if(items.TryGetValue(name,out var bucket))foreach(var stock in bucket.Values)result.AddRange(stock);
   return result;
  }
  internal List<Stock> Source(string source)=>sources.TryGetValue(source,out var value)?value.Items:new List<Stock>();
  internal long Stamp(IEnumerable<string> names){long stamp=17;unchecked{foreach(string name in names.Distinct(StringComparer.Ordinal).OrderBy(n=>n,StringComparer.Ordinal)){versions.TryGetValue(name,out int v);stamp=stamp*31+v;}}return stamp;}
 }
}
