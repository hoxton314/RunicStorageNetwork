using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RunicStorageNetwork {
 public sealed class Relay:NetworkMember,Hoverable {
  public string GetHoverName()=>Localization.instance.Localize("$rsn_relay_name");
  public float GetHoverOffset()=>1;
  public string GetHoverText()=>HoverInfo.Text(this);
 }
 public sealed class RelayPresentation:MonoBehaviour {
  Renderer[] glow;NetworkMember member;MaterialPropertyBlock block;string state;
  GameObject preview;Material lineMaterial;LineRenderer supply,storage;
  readonly List<LineRenderer> links=new List<LineRenderer>();
  readonly List<NetworkMember> targets=new List<NetworkMember>();
  float nextQuery;bool checkedConnections,areaReady,placementAllowed;
  long actor;int revision=-1;
  static readonly Color Connected=new Color(.2f,1f,1f,1f);
  static readonly Color Secondary=new Color(.2f,.8f,1f,.4f);
  static readonly Color Storage=new Color(1f,.7f,.2f,.8f);
  internal string PlacementText {get;private set;}
  void Awake(){
   member=GetComponent<NetworkMember>();
   glow=GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterial&&(r.sharedMaterial.name=="RR_Core"||r.sharedMaterial.name=="RR_Runes")).ToArray();
   block=new MaterialPropertyBlock();
  }
  void Update(){
   if(member&&member.Valid){
    HidePreview();
    string next=Topology.State(member);
    if(next!=state){state=next;foreach(var r in glow){r.GetPropertyBlock(block);block.SetFloat("_EmissionStrength",r.sharedMaterial.GetFloat("_EmissionStrength")*(state=="$rsn_connected"?1:.35f));r.SetPropertyBlock(block);}}
    return;
   }
   var player=Player.m_localPlayer;
   var ghost=player?R.Get<GameObject>(player,"m_placementGhost"):null;
   if(!player||!player.InPlaceMode()||ghost!=gameObject||!gameObject.activeInHierarchy){HidePreview();return;}
   try {UpdatePreview(player);}catch(Exception e){HidePreview();Plugin.Error("relay placement preview",e);}
  }
  void UpdatePreview(Player player){
   if(!preview){
    var shader=Shader.Find("Sprites/Default");if(!shader)return;
    lineMaterial=new Material(shader);preview=new GameObject("RSN_RelayPlacementCoverage");preview.transform.SetParent(transform,false);
    supply=AddLine("Supply",65);storage=AddLine("Storage",65);
   }
   preview.SetActive(true);
   var position=transform.position;long currentActor=player.GetPlayerID();
   // Reuse the regular topology snapshot; preview never scans chests or forces a rebuild.
   // Throttle movement queries, but invalidate immediately on player/topology changes.
   if(!checkedConnections||currentActor!=actor||revision!=Topology.DisplayRevision||Time.unscaledTime>=nextQuery){
    actor=currentActor;revision=Topology.DisplayRevision;nextQuery=Time.unscaledTime+.1f;
    targets.Clear();areaReady=ZNetScene.instance&&ZNetScene.instance.IsAreaReady(position);
    placementAllowed=areaReady&&Access.Ward(position,actor);
    if(Plugin.Enabled&&placementAllowed){
     var graph=Topology.ForActor(actor);
     foreach(var node in graph.PlacementConnections(Topology.Position(position),n=>true)){
      var target=Topology.Member(node.Id);if(target)targets.Add(target);
     }
    }
    checkedConnections=true;
   }
   // Never retain a green link to an unloaded/destroyed target or outside link range.
   for(int i=targets.Count-1;i>=0;i--){
    var target=targets[i];
    if(!target||!target.Valid||(target.transform.position-position).sqrMagnitude>Plugin.RelayLink.Value*Plugin.RelayLink.Value){targets.RemoveAt(i);nextQuery=0;}
   }
   for(int i=0;i<targets.Count;i++){
    if(i==links.Count)links.Add(AddLine("Connection_"+i,2));
    var line=links[i];line.enabled=true;line.startWidth=line.endWidth=i==0?.05f:.022f;
    line.startColor=line.endColor=i==0?Connected:Secondary;
    line.SetPosition(0,position+Vector3.up);line.SetPosition(1,targets[i].transform.position+Vector3.up);
   }
   for(int i=targets.Count;i<links.Count;i++)links[i].enabled=false;
   bool same=Mathf.Approximately(Plugin.RelaySupply.Value,Plugin.RelayStorage.Value);
   DrawRing(supply,Plugin.RelaySupply.Value,Connected,position);
   storage.enabled=!same;if(!same)DrawRing(storage,Plugin.RelayStorage.Value,Storage,position);
   string key=!Plugin.Enabled?"disabled":!areaReady?"placement_checking":!placementAllowed?"error_access":targets.Count>0?"placement_connected":"placement_disconnected";
   string color=key=="placement_connected"?"#66FFFF":key=="placement_checking"?"#DDDDDD":"#FFCC66";
   PlacementText="<color="+color+">"+RsnLocalization.Text(key)+"</color>\n";
   if(same)PlacementText+="<color=#66FFFF>"+RsnLocalization.Text("placement_shared_range",Plugin.RelaySupply.Value)+"</color>";
   else PlacementText+="<color=#66FFFF>"+RsnLocalization.Text("placement_supply_range",Plugin.RelaySupply.Value)+"</color>\n<color=#FFCC66>"+RsnLocalization.Text("placement_storage_range",Plugin.RelayStorage.Value)+"</color>";
  }
  static void DrawRing(LineRenderer line,float radius,Color color,Vector3 center){
   line.enabled=true;line.startColor=line.endColor=color;
   for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;line.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radius,.1f,Mathf.Sin(a)*radius));}
  }
  LineRenderer AddLine(string name,int points){
   var obj=new GameObject(name);obj.transform.SetParent(preview.transform,false);
   var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.useWorldSpace=true;line.positionCount=points;
   line.startWidth=line.endWidth=.035f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
   return line;
  }
  void HidePreview(){if(preview)preview.SetActive(false);PlacementText=null;checkedConnections=false;targets.Clear();}
  void OnDisable(){HidePreview();}
  void OnDestroy(){if(lineMaterial)Destroy(lineMaterial);}
 }
}
