using System;
using System.Collections.Generic;
using System.Linq;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 // A receipt covers the whole delivery. On failure only our own additions are
 // undone; existing slots and item metadata are never replaced wholesale.
 internal sealed class TerminalDelivery {
  sealed class Change {internal int X,Y,Before;internal ItemDrop.ItemData Original,Added;}
  readonly Inventory inventory;readonly List<Change> changes=new List<Change>();
  internal bool Complete {get;private set;}
  internal TerminalDelivery(Inventory value){inventory=value;}
  static string Key(ItemDrop.ItemData item){var copy=item.Clone();copy.m_stack=1;copy.m_gridPos=new Vector2i(0,0);var p=new ZPackage();copy.Save(p);return Convert.ToBase64String(p.GetArray());}
  internal void Apply(List<ItemDrop.ItemData> parcels){
   if(Complete||changes.Count!=0)throw new InvalidOperationException("delivery already attempted");
   int width=inventory.GetWidth(),height=inventory.GetHeight();var slots=new DeliveryPlanner.Stack[width*height];
   for(int y=0;y<height;y++)for(int x=0;x<width;x++){var item=inventory.GetItemAt(x,y);if(item!=null)slots[y*width+x]=new DeliveryPlanner.Stack(Key(item),item.m_stack,item.m_shared.m_maxStackSize);}
   var plan=DeliveryPlanner.Plan(slots,parcels.Select(p=>new DeliveryPlanner.Stack(Key(p),p.m_stack,p.m_shared.m_maxStackSize)).ToList());
   if(plan==null)throw new InvalidOperationException("terminal inventory full");
   foreach(var step in plan){
    int x=step.Slot%width,y=step.Slot/width;var original=inventory.GetItemAt(x,y);
    var change=new Change{X=x,Y=y,Original=original,Before=original?.m_stack??0};changes.Add(change);
    var item=parcels[step.Parcel].Clone();item.m_stack=step.Amount;
    bool added=false;
    try{added=(bool)R.Call(inventory,"AddItem",new[]{typeof(ItemDrop.ItemData),typeof(int),typeof(int),typeof(int),typeof(bool)},item,step.Amount,x,y,false);}
    finally{if(original==null)change.Added=inventory.GetItemAt(x,y);}
    var current=inventory.GetItemAt(x,y);
    if(!added||current==null||current.m_stack!=change.Before+step.Amount||Key(current)!=Key(parcels[step.Parcel]))throw new InvalidOperationException("terminal insertion refused");
   }
   Complete=true;
  }
  internal void Restore(){
   for(int i=changes.Count-1;i>=0;i--){
    var c=changes[i];var item=c.Original??c.Added;
    if(item!=null&&inventory.ContainsItem(item)){
     int added=item.m_stack-c.Before;
     if(added>0)inventory.RemoveItem(item,added);
     if(c.Original==null?inventory.ContainsItem(item):!inventory.ContainsItem(item)||item.m_stack!=c.Before)throw new InvalidOperationException("terminal rollback incomplete");
    }else if(c.Original!=null)throw new InvalidOperationException("terminal original stack moved");
    changes.RemoveAt(i);
   }
  }
 }
}
