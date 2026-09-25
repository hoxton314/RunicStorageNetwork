using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 // Separate read-only owner queries from Prepare/Commit. Selecting an
 // incomplete recipe still refreshes every one of its ingredients.
 internal static class CraftInspection {
  sealed class Source {internal string Key;internal long Owner;internal ZDOID Id;internal float Sent,FirstSent=-1;internal bool Delivered;}
  static Operation operation;
  static Recipe recipe;static ItemDrop.ItemData upgrade;
  static readonly Dictionary<string,Source> sources=new Dictionary<string,Source>();
  static float nextRefresh;
  internal static bool Ready {get;private set;}
  internal static void Clear(){operation=null;recipe=null;upgrade=null;sources.Clear();Ready=false;nextRefresh=0;}
  internal static void Ensure(Actions.Pending selected,Core core){
   bool same=operation!=null&&recipe==selected.Recipe&&upgrade==selected.Upgrade&&operation.Core==core.Id&&operation.Station==selected.Op.Station&&operation.Quality==selected.Op.Quality&&operation.Multiplier==selected.Op.Multiplier;
   if(same&&(!Ready||Time.unscaledTime<nextRefresh))return;
   Clear();recipe=selected.Recipe;upgrade=selected.Upgrade;
   var op=Actions.Create(selected.Player,core,false,recipe.name,selected.Op.Quality,selected.Op.Multiplier);
   if(!op.ReadRequirements(out _))return;
   var proposal=new Actions.Pending{Op=op,Player=selected.Player};
   if(!Actions.Propose(proposal,new List<Debit>()))return;
   operation=op;
   foreach(var container in core.Pool){
    if(!Access.Container(container,op.PlayerId,core,out _,ownLease:true))continue;
    var z=R.View(container).GetZDO();string key=R.Key(z.m_uid);
    sources[key]=new Source{Key=key,Owner=z.GetOwner(),Id=z.m_uid,Sent=-100};
   }
   // No cap on the number of browsed chests. Only each packet/frame is bounded.
   if(sources.Count==0){Ready=true;nextRefresh=Time.unscaledTime+2;}
  }
  internal static void Tick(){
   if(operation==null||Ready)return;
   float now=Time.unscaledTime;
   foreach(var source in sources.Values.Where(s=>!s.Delivered)){
    var z=RemoteContext.Data(source.Id);
    if(z!=null&&z.GetOwner()!=source.Owner){source.Owner=z.GetOwner();source.Sent=-100;source.FirstSent=-1;}
   }
   foreach(var source in sources.Values.Where(s=>!s.Delivered&&s.FirstSent>=0&&now-s.FirstSent>6).ToArray()){
    Apply(source,new List<Stock>());Plugin.Debug("Selected ingredient inspection timed out; unconfirmed source counts cleared: "+source.Key);
   }
   if(sources.Values.All(s=>s.Delivered)){Ready=true;nextRefresh=now+2;return;}
   var first=sources.Values.FirstOrDefault(s=>!s.Delivered&&now-s.Sent>=2);
   if(first==null)return;
   var batch=sources.Values.Where(s=>!s.Delivered&&s.Owner==first.Owner&&now-s.Sent>=2).Take(16).ToArray();
   var p=operation.Write();p.Write(batch.Length);
   foreach(var source in batch){source.Sent=now;if(source.FirstSent<0)source.FirstSent=now;p.Write(source.Id);}
   try{Transport.Send(first.Owner,"inspect",p);}catch(Exception e){Plugin.Debug("Ingredient inspection retry: "+e.Message);}
  }
  internal static void Request(long sender,ZPackage p){
   var op=Operation.Read(p);int count=p.ReadInt();if(count<1||count>16||op.Build||op.Quote||op.Peer!=sender)return;
   if(!op.ReadRequirements(out _)||!RemoteContext.Actor(op.Actor,sender,op.PlayerId,out _,out _))return;
   var context=new RemoteContext(op);
   for(int i=0;i<count;i++){
    var id=p.ReadZDOID();string key=R.Key(id);var container=ZNetScene.instance.FindInstance(id)?.GetComponent<Container>();
    var items=new List<Stock>();
    if(context.OwnerSource(container,out _,ownLease:true)){
     var inventory=container.GetInventory();var view=R.View(container);
     if(!Transport.Locked(inventory)&&!Integrations.IsBusy(inventory)&&!container.IsInUse()&&view.GetZDO().GetInt(ZDOVars.s_inUse)==0)R.Call(container,"Load");
     items=Stockroom.Snapshot(inventory,key,op.Needs,true);
    }
    var reply=new ZPackage();reply.Write(op.Id);reply.Write(key);Wire.Stocks(reply,items);Transport.Send(sender,"inspected",reply);
   }
  }
  internal static void Response(long sender,ZPackage p){
   string id=p.ReadString(),key=p.ReadString();var items=Wire.Stocks(p);
   if(operation?.Id!=id||!sources.TryGetValue(key,out var source)||source.Owner!=sender||source.Delivered)return;
   var current=RemoteContext.Data(source.Id);if(current==null||current.GetOwner()!=sender)return;
   if(items.Any(s=>s.Source!=key||!operation.Needs.Any(n=>n.Item==s.Item)))return;
   Apply(source,items);
   if(sources.Values.All(s=>s.Delivered)){Ready=true;nextRefresh=Time.unscaledTime+2;}
  }
  static void Apply(Source source,List<Stock> items){
   // Explicit zeros are essential: absence must replace a former positive
   // count, while unrelated item types remain in the overview.
   foreach(var need in operation.Needs)if(!items.Any(s=>s.Item==need.Item))items.Add(new Stock(source.Key,need.Item,1,0));
   source.Delivered=true;Stockroom.Observe(source.Key,items);
  }
 }
}
