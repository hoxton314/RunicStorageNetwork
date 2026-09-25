using System;
using System.Collections.Generic;
using System.Linq;

namespace RunicStorageNetwork.Logic {
 public sealed class Need {
  public string Item; public int Amount; public int Quality;
  public Need(string item,int amount,int quality=-1){Item=item;Amount=amount;Quality=quality;}
 }
 public sealed class Stock {
  public string Source,Item; public int Quality,Amount; public bool Allowed;
  public Stock(string source,string item,int quality,int amount,bool allowed=true){Source=source;Item=item;Quality=quality;Amount=amount;Allowed=allowed;}
 }
 public sealed class Debit {
  public string Source,Item;public int Quality,Amount;
  public Debit(string source,string item,int quality,int amount){Source=source;Item=item;Quality=quality;Amount=amount;}
 }
 public static class Planner {
  // A source snapshot must contain aggregated quantities per prefab and quality.
  // Duplicate network IDs are never additional stock. Conflicting duplicates fail closed.
  public static List<Debit> Plan(IEnumerable<Need> needs,IEnumerable<Stock> stock,bool compact=false) {
   var unique=new Dictionary<string,Stock>(StringComparer.Ordinal);
   foreach(var s in stock) {
    if(s.Amount<0)throw new InvalidOperationException("Negative stock");
    string key=s.Source+"\n"+s.Item+"\n"+s.Quality;
    if(unique.TryGetValue(key,out var other)) {
     if(other.Amount!=s.Amount || other.Allowed!=s.Allowed)throw new InvalidOperationException("Conflicting duplicate snapshot");
    } else unique.Add(key,new Stock(s.Source,s.Item,s.Quality,s.Amount,s.Allowed));
   }
   var ordered=unique.Values.Where(s=>s.Allowed).OrderBy(s=>s.Source=="player"?0:1).ThenBy(s=>s.Source,StringComparer.Ordinal).ThenBy(s=>s.Quality).ToList();
   var result=new List<Debit>();
   foreach(var n in needs) {
    if(n.Amount<0 || n.Amount>100000)throw new InvalidOperationException("Invalid requirement");
    int left=n.Amount;
    var eligible=ordered.Where(s=>s.Item==n.Item&&(n.Quality<0||s.Quality==n.Quality));
    // Keep player-first payment, then prefer fuller sources to avoid reserving
    // hundreds of fragmentary stacks when one chest can satisfy the recipe.
    if(compact)eligible=eligible.OrderBy(s=>s.Source=="player"?0:1).ThenByDescending(s=>s.Amount).ThenBy(s=>s.Source,StringComparer.Ordinal).ThenBy(s=>s.Quality);
    foreach(var s in eligible) {
     int take=Math.Min(left,s.Amount);if(take==0)continue;
     result.Add(new Debit(s.Source,s.Item,s.Quality,take));s.Amount-=take;left-=take;if(left==0)break;
    }
    if(left!=0)return null;
   }
   return result;
  }
 }
 public static class SourceSelection {
  // Packet/work bound for one payment, never a bound on connected storage.
  public const int MaxEntries=1024;
  public static string[] Sources(IReadOnlyCollection<Debit> plan){
   if(plan==null||plan.Count>MaxEntries)return null;
   return plan.Where(d=>d.Amount>0&&d.Source!="player").Select(d=>d.Source).Distinct(StringComparer.Ordinal).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
  }
 }
 public enum Phase { Preparing, Prepared, Committing, Paid, Completed, Aborted, Uncertain }
 public sealed class Decision {
  public readonly string Id;public Phase Phase {get;private set;}=Phase.Preparing;
  readonly HashSet<string> expected,prepared=new HashSet<string>(),paid=new HashSet<string>();
  public Decision(string id,IEnumerable<string> sources){Id=id;expected=new HashSet<string>(sources);if(expected.Count==0)Phase=Phase.Prepared;}
  public void Prepared(string source){if(Phase!=Phase.Preparing)return;if(!expected.Contains(source))throw new InvalidOperationException("Unexpected source");prepared.Add(source);if(prepared.SetEquals(expected))Phase=Phase.Prepared;}
  public void Commit(){if(Phase!=Phase.Prepared)throw new InvalidOperationException("Not prepared");Phase=expected.Count==0?Phase.Paid:Phase.Committing;}
  public void Paid(string source){if(Phase!=Phase.Committing)return;if(!expected.Contains(source))throw new InvalidOperationException("Unexpected payer");paid.Add(source);if(paid.SetEquals(expected))Phase=Phase.Paid;}
  public bool Complete(){if(Phase==Phase.Completed)return false;if(Phase!=Phase.Paid)throw new InvalidOperationException("Unconfirmed payment");Phase=Phase.Completed;return true;}
  public void Refuse(){if(Phase==Phase.Preparing||Phase==Phase.Prepared)Phase=Phase.Aborted;else if(Phase!=Phase.Completed&&Phase!=Phase.Aborted)Phase=Phase.Uncertain;}
  public void Timeout(){Refuse();}
 }
}
