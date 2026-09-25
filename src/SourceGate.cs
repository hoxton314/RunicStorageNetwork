using System;
using System.Collections.Generic;
using System.Linq;

namespace RunicStorageNetwork.Logic {
 // A source remains held until its owner acknowledges release, including rollback.
 public sealed class SourceGate {
  readonly Dictionary<string,string> holders=new Dictionary<string,string>(StringComparer.Ordinal);
  public bool TryAcquire(string operation,IEnumerable<string> sources){
   var keys=sources.Distinct(StringComparer.Ordinal).ToArray();
   if(keys.Any(k=>holders.TryGetValue(k,out var owner)&&owner!=operation))return false;
   foreach(string key in keys)holders[key]=operation;return true;
  }
  public bool Held(string key,string except=null)=>holders.TryGetValue(key,out var owner)&&owner!=except;
  public void Released(string operation,string key){if(holders.TryGetValue(key,out var owner)&&owner==operation)holders.Remove(key);}
  public void Cancel(string operation){foreach(string key in holders.Where(p=>p.Value==operation).Select(p=>p.Key).ToArray())holders.Remove(key);}
  public void Clear()=>holders.Clear();
 }
 public static class SessionGuard {
  public static string Check(bool exists,long owner,long sender,long actualPlayer,long expectedPlayer,bool dead,bool server,bool localSender,bool matchesCharacter){
   if(!exists)return "actor data unavailable";
   if(owner!=sender)return "actor session mismatch";
   if(actualPlayer==0||actualPlayer!=expectedPlayer)return "actor identity mismatch";
   if(dead)return "actor dead";
   if(server&&!localSender&&!matchesCharacter)return "sender does not own character";
   return null;
  }
 }
}
