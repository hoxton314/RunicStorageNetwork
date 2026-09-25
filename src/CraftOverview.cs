using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 internal static class CraftOverview {
  static readonly StockCatalog<Container> catalog=new StockCatalog<Container>();
  static InventoryGui gui;static Player player;static CraftingStation station;static Core core;
  static float nextScan;static bool requestRecords,rowsDirty;
  static object[] rowQueue;static int rowIndex;
  internal static void Clear(){catalog.Clear();gui=null;player=null;station=null;core=null;nextScan=0;requestRecords=rowsDirty=false;rowQueue=null;}
  internal static void Rescan(){catalog.CancelScan();nextScan=0;}
  internal static void Open(InventoryGui current,Player actor){
   var at=actor?actor.GetCurrentCraftingStation():null;
   if(!actor||!current||!at||at.m_upgrader||!Plugin.Enabled){Clear();return;}
   if(gui==current&&player==actor&&station==at)return;
   Clear();gui=current;player=actor;station=at;requestRecords=true;
  }
  internal static void Tick(){
   if(!gui)return;
   if(!InventoryGui.IsVisible()||!player||player.IsDead()||player.GetCurrentCraftingStation()!=station||!Plugin.Enabled){Clear();return;}
   if(!catalog.Running){
    if(Time.unscaledTime<nextScan)return;
    var current=Actions.Context(player,true);
    if(core!=current){core=current;catalog.Clear();requestRecords=true;rowsDirty=true;}
    if(!core){nextScan=Time.unscaledTime+.5f;return;}
    core.Scan();catalog.Begin(core.Pool.ToArray());
   }
   var clock=Stopwatch.StartNew();int revision=catalog.Revision;
   try{catalog.Step(Read,16,()=>clock.Elapsed.TotalMilliseconds>=.75);}
   catch(Exception e){catalog.Clear();nextScan=Time.unscaledTime+2;rowsDirty=true;Plugin.Error("craft-overview",e);return;}
   if(!catalog.Running){
    rowsDirty|=revision!=catalog.Revision;requestRecords=false;nextScan=Time.unscaledTime+1;
    Plugin.Debug("Craft overview refreshed; containers="+core.Pool.Count+"; revision="+catalog.Revision+"; no reservations acquired");
   }
  }
  static IEnumerable<Stock> Read(Container container){
   // Viewing an inventory must not acquire a lease or treat a MUC slot lock as
   // an empty chest. Access, world loading and network coverage still apply.
   if(!core||!player||!Access.Container(container,player.GetPlayerID(),core,out _,ownLease:true))return Array.Empty<Stock>();
   var view=R.View(container);var inventory=container.GetInventory();
   if(requestRecords&&!view.IsOwner())ZDOMan.instance.RequestZDO(view.GetZDO().m_uid);
   // Never call Load while another mod owns an inventory mutation. Vanilla
   // otherwise advances m_lastRevision even if a mutation prefix rejects Load.
   if(!Transport.Locked(inventory)&&!Integrations.IsBusy(inventory)&&!container.IsInUse()&&view.GetZDO().GetInt(ZDOVars.s_inUse)==0)R.Call(container,"Load");
   return Stockroom.Preview(container);
  }
  internal static List<Stock> Stock(Player actor,IEnumerable<Need> requirements){
   var needs=requirements.ToList();var result=Stockroom.Snapshot(actor.GetInventory(),"player",needs,false);
   if(actor==player&&core&&catalog.Ready)result.AddRange(catalog.ForItems(needs.Select(n=>n.Item)));
   return result;
  }
  internal static bool Ready(Player actor)=>actor==player&&core&&catalog.Ready;
  internal static void Fresh(string key,List<Stock> items){
   if(!core)return;
   int revision=catalog.Revision;catalog.Replace(key,items.Select(s=>s.Item),items);rowsDirty|=catalog.Revision!=revision;
  }
  internal static void RefreshRows(InventoryGui current){
   if(gui!=current||!player||Actions.Active!=null)return;
   // Recolour existing rows once a completed overview changes. Do not recreate
   // or reorder the full list, move the selection, or reserve every recipe.
   if(rowQueue==null){if(!rowsDirty)return;rowsDirty=false;rowQueue=((IList)R.Get<object>(gui,"m_availableRecipes")).Cast<object>().ToArray();rowIndex=0;}
   var clock=Stopwatch.StartNew();int budget=32;
   while(rowIndex<rowQueue.Length&&budget-->0){
    var row=rowQueue[rowIndex++];
    var type=row.GetType();var recipe=(Recipe)type.GetProperty("Recipe").GetValue(row,null);
    var item=(ItemDrop.ItemData)type.GetProperty("ItemData").GetValue(row,null);
    var element=(GameObject)type.GetProperty("InterfaceElement").GetValue(row,null);
    if(!recipe||!element)continue;
    int quality=item==null?1:item.m_quality+1;
    bool available=quality<=recipe.m_item.m_itemData.m_shared.m_maxQuality&&(player.HaveRequirements(recipe,false,quality,1)||player.NoCostCheat()||ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost));
    var icon=element.transform.Find("icon")?.GetComponent<Image>();var label=element.transform.Find("name")?.GetComponent<TMP_Text>();
    if(icon)icon.color=available?Color.white:new Color(1,0,1,0);
    if(label)label.color=available?Color.white:new Color(.66f,.66f,.66f,1);
    if(clock.Elapsed.TotalMilliseconds>=.75)break;
   }
   if(rowIndex==rowQueue.Length)rowQueue=null;
  }
 }
}
