using System;
using System.Collections.Generic;
using System.Linq;

namespace RunicStorageNetwork.Logic {
 // Read-only browsing snapshot. Recipe feasibility and payment reservations
 // never filter this catalogue; a missing ingredient cannot hide other stock.
 internal sealed class StockCatalog<T> {
  T[] queue;int index;
  Dictionary<string,Stock> pending;
  HashSet<string> corrected;
  Dictionary<string,Stock> published=new Dictionary<string,Stock>(StringComparer.Ordinal);
  Dictionary<string,List<Stock>> byItem=new Dictionary<string,List<Stock>>(StringComparer.Ordinal);
  internal bool Running=>queue!=null;
  internal bool Ready {get;private set;}
  internal int Revision {get;private set;}
  internal void Begin(T[] sources){queue=sources;index=0;pending=new Dictionary<string,Stock>(StringComparer.Ordinal);corrected=new HashSet<string>(StringComparer.Ordinal);}
  internal void CancelScan(){queue=null;pending=null;corrected=null;}
  internal void Clear(){CancelScan();published.Clear();byItem.Clear();Ready=false;Revision++;}
  internal void Step(Func<T,IEnumerable<Stock>> read,int limit,Func<bool> expired){
   if(queue==null)return;
   int count=0;
   while(index<queue.Length&&count<limit){
    foreach(var item in read(queue[index])){
     if(corrected.Contains(item.Source+"\n"+item.Item))continue;
     if(item.Amount<0)throw new InvalidOperationException("Negative stock in overview");
     if(item.Amount==0||!item.Allowed)continue;
     string key=item.Source+"\n"+item.Item+"\n"+item.Quality;
     if(pending.TryGetValue(key,out var previous)&&previous.Amount!=item.Amount)throw new InvalidOperationException("Conflicting overview source");
     pending[key]=new Stock(item.Source,item.Item,item.Quality,item.Amount);
    }
    index++;count++;if(expired())break;
   }
   if(index<queue.Length)return;
   bool changed=!Ready||published.Count!=pending.Count||pending.Any(p=>!published.TryGetValue(p.Key,out var old)||old.Amount!=p.Value.Amount);
   if(changed){published=pending;byItem=published.Values.GroupBy(s=>s.Item).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.Ordinal);Revision++;}
   Ready=true;queue=null;pending=null;corrected=null;
  }
  internal List<Stock> ForItems(IEnumerable<string> names){
   var result=new List<Stock>();
   foreach(string name in names.Distinct(StringComparer.Ordinal))if(byItem.TryGetValue(name,out var values))result.AddRange(values);
   return result;
  }
  internal void Replace(string source,IEnumerable<string> names,IEnumerable<Stock> values){
   var scope=new HashSet<string>(names,StringComparer.Ordinal);var fresh=values.ToArray();
   var old=published.Values.Where(s=>s.Source==source&&scope.Contains(s.Item)).ToDictionary(s=>s.Item+"\n"+s.Quality,s=>s.Amount,StringComparer.Ordinal);
   var incoming=fresh.Where(s=>s.Source==source&&scope.Contains(s.Item)&&s.Amount>0&&s.Allowed).ToDictionary(s=>s.Item+"\n"+s.Quality,s=>s.Amount,StringComparer.Ordinal);
   bool changed=old.Count!=incoming.Count||incoming.Any(s=>!old.TryGetValue(s.Key,out int amount)||amount!=s.Value);
   Action<Dictionary<string,Stock>> replace=map=>{
    foreach(var key in map.Where(p=>p.Value.Source==source&&scope.Contains(p.Value.Item)).Select(p=>p.Key).ToArray())map.Remove(key);
    foreach(var s in fresh)if(s.Source==source&&scope.Contains(s.Item)&&s.Amount>0&&s.Allowed)map[source+"\n"+s.Item+"\n"+s.Quality]=new Stock(source,s.Item,s.Quality,s.Amount);
   };
   replace(published);if(pending!=null){replace(pending);foreach(string name in scope)corrected.Add(source+"\n"+name);}
   if(changed){byItem=published.Values.GroupBy(s=>s.Item).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.Ordinal);Revision++;}
  }
 }
}
