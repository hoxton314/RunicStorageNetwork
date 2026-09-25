using System.Collections.Generic;
using UnityEngine;

namespace RunicStorageNetwork {
 // Hover requests only schedule work. Read the existing topology in NetworkSystem.Update;
 // never scan inventory or refresh topology from Container.GetHoverText.
 internal static class ContainerHover {
  static Container target;static Player player;static int frame;static float next;
  static string text="";static readonly DisplayAccessCache access=new DisplayAccessCache();
  internal static string Text(Container container){
   if(!Plugin.Enabled||!container||!Player.m_localPlayer)return "";
   if(target!=container||player!=Player.m_localPlayer){Clear();target=container;player=Player.m_localPlayer;}
   frame=Time.frameCount;return text;
  }
  internal static void Clear(){target=null;player=null;text="";next=0;access.Clear();}
  internal static void Tick(){
   if(!Plugin.Enabled||!target||!player||player!=Player.m_localPlayer||frame<Time.frameCount-1){Clear();return;}
   if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
   access.Clear();var lines=new List<string>();
   foreach(string network in Topology.ContainerNetworks(target)){
    var core=Topology.RootSnapshot(network);
    // An open or reserved chest is still connected. Respect access and loaded coverage.
    if(!core||!Access.Container(target,player.GetPlayerID(),core,out _,ownLease:true,display:access))continue;
    string name=NetworkName.Read(Topology.LabelRootSnapshot(network));
    string line=name.Length==0?RsnLocalization.Text("chest_connected"):RsnLocalization.Text("chest_connected_named",name);
    if(!lines.Contains(line))lines.Add(line);
   }
   text=string.Join("\n",lines);
  }
 }
}
