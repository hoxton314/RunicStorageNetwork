using System;

namespace RunicStorageNetwork.Logic {
 // Display-only work queue. Never publishes a partial count or retains a finished snapshot.
 internal sealed class IncrementalCount<T> {
  T[] items;int index,pending;
  internal int? Value {get;private set;}
  internal bool Running=>items!=null;
  internal void Begin(T[] snapshot){items=snapshot??throw new ArgumentNullException(nameof(snapshot));index=pending=0;}
  internal void Reset(){items=null;index=pending=0;Value=null;}
  internal void Step(Func<T,bool> include,int maxItems,Func<bool> timeExpired){
   if(items==null)return;
   int processed=0;
   while(index<items.Length&&processed<maxItems){
    if(include(items[index]))pending++;
    index++;processed++;
    if(timeExpired())break;
   }
   if(index==items.Length){Value=pending;items=null;}
  }
 }
}
