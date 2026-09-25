using System;
using System.Collections.Generic;

namespace RunicStorageNetwork.Logic {
 // Only explicit terminal refusals may create a new payment attempt. Lost
 // messages replay the same ID and cannot turn an unknown outcome into a retry.
 public sealed class RecoveryAttempt {
  public bool InFlight {get;private set;}
  public int Attempts {get;private set;}
  public double Due {get;private set;}
  readonly double until;
  public RecoveryAttempt(double now){until=now+8;Due=now;}
  public void Sent(){InFlight=true;Attempts++;}
  public bool Retry(string reason,double now){
   InFlight=false;
   if(now>=until||!Transient(reason))return false;
   Due=now+Math.Min(2,.5*Math.Pow(2,Math.Min(Attempts,2)));return true;
  }
  public bool Expired(double now)=>now>=until;
  public static bool Transient(string reason){
   if(string.IsNullOrEmpty(reason))return false;
   foreach(string prefix in new[]{"owner refused: ","commit refused: ","action refused: "})if(reason.StartsWith(prefix,StringComparison.Ordinal))return Transient(reason.Substring(prefix.Length));
   int suffix=reason.IndexOf(" / source path changed",StringComparison.Ordinal);if(suffix>=0)return Transient(reason.Substring(0,suffix));
   if(reason.StartsWith("Fresh stock insufficient:",StringComparison.Ordinal)||reason.StartsWith("source path/access changed before result:",StringComparison.Ordinal))return true;
   switch(reason){
    case "offer expired":case "network path or coverage changed":case "network path/storage coverage unavailable":case "unloaded":case "unconfirmed loaded area":case "inventory unavailable":case "busy/reserved":case "owner unavailable":case "ownership changed":case "reservation missing":case "prepare timeout":case "queue timeout":case "previous operation pending":case "insufficient fresh resources":case "Stale inventory":case "actor data unavailable":case "No coordinator":case "dispatch failed":return true;
    default:return false;
   }
  }
 }
 public static class CraftCapacity {
  public static int Maximum(int amount,int multiplier,int bonus)=>checked(amount+Math.Max(0,bonus)*multiplier*(multiplier+1)/2);
 }
 public sealed class OfferWindow {
  public bool Confirmed {get;private set;}
  public bool Claimed {get;private set;}
  public bool Consumed {get;private set;}
  public bool Cancelled {get;private set;}
  public double Until {get;private set;}
  public bool Ready(double now)=>Confirmed&&!Cancelled&&!Consumed&&now<Until;
  public void Confirm(double until){if(Cancelled||Consumed||Claimed||Confirmed)return;Confirmed=true;Until=until;}
  public bool Claim(double now){if(!Ready(now)||Claimed)return false;Claimed=true;Until=now+20;return true;}
  public bool Consume(double now){if(!Claimed||!Ready(now))return false;Consumed=true;return true;}
  public void Cancel(){Cancelled=true;Confirmed=false;}
 }
 public sealed class OutcomeReceipts {
  public sealed class Receipt {public bool Success;public string Reason;}
  readonly Dictionary<string,Receipt> values=new Dictionary<string,Receipt>(StringComparer.Ordinal);
  public bool TryGet(string id,out Receipt result)=>values.TryGetValue(id,out result);
  public void Record(string id,bool result,string reason){if(values.TryGetValue(id,out var old)&&old.Success!=result)throw new InvalidOperationException("Conflicting action outcome");values[id]=new Receipt{Success=result,Reason=reason};}
  public void Clear()=>values.Clear();
 }
}
