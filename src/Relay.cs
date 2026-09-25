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
  Renderer[] glow;NetworkMember member;MaterialPropertyBlock block;string state;GameObject preview;Material lineMaterial;
  void Awake(){member=GetComponent<NetworkMember>();glow=GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterial&&(r.sharedMaterial.name=="RR_Core"||r.sharedMaterial.name=="RR_Runes")).ToArray();block=new MaterialPropertyBlock();}
  void Update(){
   if(!Player.m_localPlayer)return;
   if(member&&member.Valid){string next=Topology.State(member);if(next!=state){state=next;foreach(var r in glow){r.GetPropertyBlock(block);block.SetFloat("_EmissionStrength",r.sharedMaterial.GetFloat("_EmissionStrength")*(state=="$rsn_connected"?1:.35f));r.SetPropertyBlock(block);}}return;}
   var ghost=R.Get<GameObject>(Player.m_localPlayer,"m_placementGhost");bool visible=ghost&&ghost==gameObject&&Player.m_localPlayer.InPlaceMode();
   if(!visible){if(preview)preview.SetActive(false);return;}
   if(!preview){var shader=Shader.Find("Sprites/Default");if(!shader)return;lineMaterial=new Material(shader);preview=new GameObject("RSN_RelayPlacementCoverage");preview.transform.SetParent(transform,false);AddLine("Supply",65);AddLine("Storage",65);AddLine("Link",2);}
   preview.SetActive(true);float[] radii={Plugin.RelaySupply.Value,Plugin.RelayStorage.Value};Color[] colors={Color.cyan,new Color(1,.7f,.2f)};
   for(int j=0;j<2;j++){var line=preview.transform.GetChild(j).GetComponent<LineRenderer>();line.startColor=line.endColor=colors[j];for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;line.SetPosition(i,transform.position+new Vector3(Mathf.Cos(a)*radii[j],.1f+j*.05f,Mathf.Sin(a)*radii[j]));}}
   Topology.Refresh();var candidates=Topology.ForActor(Player.m_localPlayer.GetPlayerID()).Candidates(Topology.Position(transform.position),n=>true);
   var link=preview.transform.GetChild(2).GetComponent<LineRenderer>();link.enabled=candidates.Count>0;if(link.enabled){var n=candidates.Values.Select(Topology.Member).Where(m=>m).OrderBy(m=>(m.transform.position-transform.position).sqrMagnitude).First();link.SetPosition(0,transform.position+Vector3.up);link.SetPosition(1,n.transform.position+Vector3.up);link.startColor=link.endColor=Color.cyan;}
  }
  void AddLine(string name,int points){var obj=new GameObject(name);obj.transform.SetParent(preview.transform,false);var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.useWorldSpace=true;line.positionCount=points;line.startWidth=line.endWidth=.035f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;}
  void OnDestroy(){if(lineMaterial)Destroy(lineMaterial);}
 }
}
