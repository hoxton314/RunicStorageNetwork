using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace RunicStorageNetwork {
 internal static class CoreMaterials {
  // Exact healthy materials inspected in the installed game's prefab hierarchy.
  static readonly string[] Slots={"SNC_Stone","SNC_Timber","SNC_Iron","SNC_Plinth"};
  static readonly string[] Donors={"stone_wall_2x1","wood_door","iron_floor_1x1","stone_wall_2x1"};
  static readonly string[] Names={"stone_mat","door_wood","metalwall","stone_mat"};
  const string Prefix="RSN_Vanilla_";
  internal static void Apply(GameObject core,bool relay=false){
   var slots=relay?new[]{"RR_Stone","RR_Timber","RR_Iron"}:Slots;
   if(!core)throw new InvalidOperationException("Core prefab unavailable");
   var renderers=core.GetComponentsInChildren<MeshRenderer>(true);
   var targets=new MeshRenderer[slots.Length];int existing=0;
   foreach(var renderer in renderers){
    var material=renderer.sharedMaterial;if(!material)throw new InvalidOperationException("Missing core material");
    for(int i=0;i<slots.Length;i++){
     if(material.name==Prefix+slots[i])existing++;
     if(material.name==slots[i]){
      if(targets[i])throw new InvalidOperationException("Duplicate core material slot "+slots[i]);
      targets[i]=renderer;
     }
    }
   }
   if(existing==slots.Length)return; // OnVanillaPrefabsAvailable fires on each menu load.
   if(existing!=0)throw new InvalidOperationException("Partial native material assignment");
   var replacements=new List<Material>();
   try {
    for(int i=0;i<slots.Length;i++){
     if(!targets[i])throw new InvalidOperationException("Core material slot missing: "+slots[i]);
     var donor=PrefabManager.Instance.GetPrefab(Donors[i]);
     if(!donor)throw new InvalidOperationException("Material donor missing: "+Donors[i]);
     Material source=null;
     foreach(var renderer in donor.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
      if(material&&material.name==Names[i]&&material.shader&&material.shader.name=="Custom/Piece"){source=material;break;}
     if(!source)throw new InvalidOperationException("Native material missing: "+Donors[i]+"/"+Names[i]);
     var replacement=new Material(source){name=Prefix+slots[i]};replacements.Add(replacement);
     // Native triplanar sampling fits this untextured model without using its glow UVs.
     // All changes are on our copies; the game's shared materials/textures stay untouched.
     foreach(string property in new[]{"_TriplanarMap","_TriplanarLocalPos","_TriplanarScale","_Cull","_Cutoff","_TwoSidedNormals"})
      if(!replacement.HasProperty(property))throw new InvalidOperationException("Native shader property missing: "+property);
     replacement.SetFloat("_TriplanarMap",1);replacement.SetFloat("_TriplanarLocalPos",1);replacement.SetFloat("_TriplanarScale",1);
     replacement.SetFloat("_Cull",0);replacement.SetFloat("_TwoSidedNormals",1);replacement.SetFloat("_Cutoff",0);
     if(i!=2){
      // Recesses and their emissive floors must share a fixed geometric surface.
      replacement.SetFloat("_RippleDistance",0);replacement.SetFloat("_MoveableObject",1);
     }
     if(i==3){
      // Keep the native stone albedo/tint, but leave the plinth surface flat.
      replacement.SetTexture("_BumpMap",null);
      replacement.SetFloat("_BumpScale",0);replacement.SetFloat("_ValueNoise",0);
     }
     if(i<2||i==3){
      // Surface UVs and tangents are baked into our mesh copies. Native stone is
      // an atlas, so full-texture triplanar sampling would cross unrelated patches.
      if(i<2&&!replacement.GetTexture("_BumpMap"))throw new InvalidOperationException("Native normal map missing: "+Names[i]);
      replacement.SetFloat("_TriplanarMap",0);
      foreach(string texture in new[]{"_MainTex","_BumpMap"}){
       replacement.SetTextureScale(texture,Vector2.one);replacement.SetTextureOffset(texture,Vector2.zero);
      }
      replacement.SetVector("_MainTex_ST",new Vector4(1,1,0,0));
      replacement.SetFloat("_BumpScale",i==3?0:(relay?(i==0?.55f:.65f):(i==0?1.15f:1.25f)));
      // Keep the native wall/door roughness and tint; no artificial base emission.
     }
    }
   }catch{foreach(var material in replacements)UnityEngine.Object.Destroy(material);throw;}
   // Resolve every donor before replacing anything. Emissive core/runes/apex stay intact.
   for(int i=0;i<slots.Length;i++){
    targets[i].sharedMaterial=replacements[i];
    Plugin.Info((relay?"Relay":"Core")+" material "+slots[i]+" <- "+Donors[i]+"/"+Names[i]+"; shader=Custom/Piece; "+(i<2?"engraved surface UV; normal="+replacements[i].GetTexture("_BumpMap").name+" strength="+replacements[i].GetFloat("_BumpScale"):(i==2?"local triplanar (unchanged since 0.1.2)":"textured flat stone; surface UV; normal/noise disabled")));
   }
  }
 }
}
