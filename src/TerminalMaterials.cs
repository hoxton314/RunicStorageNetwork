using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunicStorageNetwork {
 // Shared by runtime and the Editor icon pass. Native assets are never bundled.
 internal static class TerminalMaterials {
  const string Prefix="RSN_Vanilla_";
  static readonly string[] Plain={"RST_Silver","RST_Leather","RST_Parchment","RST_PageEdges","RST_Cloth","RST_Cloth_RedBorder","RST_BannerSymbol"};
  internal static void Apply(GameObject prefab,Func<string,GameObject> resolve){
   var pending=new List<KeyValuePair<MeshRenderer,Material>>();
   try{
    foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true)){
     var original=renderer.sharedMaterial;
     if(!original)throw new InvalidOperationException("Terminal material missing");
     string slot=original.name;if(slot.StartsWith(Prefix,StringComparison.Ordinal))continue;
     bool stone=slot=="RST_Stone",wood=slot=="RST_Timber",iron=slot=="RST_Iron",plain=Array.IndexOf(Plain,slot)>=0;
     if(!stone&&!wood&&!iron&&!plain)continue; // Three geometric rune groups retain independent emission.
     string donor=stone?"stone_wall_2x1":iron?"iron_floor_1x1":"wood_door";
     string name=stone?"stone_mat":iron?"metalwall":"door_wood";
     var sourcePrefab=resolve(donor);if(!sourcePrefab)throw new InvalidOperationException("Terminal donor missing: "+donor);
     Material source=null;
     foreach(var r in sourcePrefab.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)
      if(m&&m.name==name&&m.shader&&m.shader.name=="Custom/Piece")source=m;
     if(!source)throw new InvalidOperationException("Terminal donor material missing: "+name);
     var mat=new Material(source){name=Prefix+slot};pending.Add(new KeyValuePair<MeshRenderer,Material>(renderer,mat));
     mat.SetFloat("_Cull",0);mat.SetFloat("_TwoSidedNormals",1);mat.SetFloat("_Cutoff",0);
     mat.SetFloat("_TriplanarMap",iron?1:0);mat.SetFloat("_TriplanarLocalPos",1);mat.SetFloat("_TriplanarScale",1);
     mat.SetFloat("_RippleDistance",0);mat.SetFloat("_MoveableObject",1);mat.SetFloat("_ValueNoise",0);mat.SetFloat("_AddRain",0);
     foreach(string property in new[]{"_MainTex","_BumpMap"}){mat.SetTextureScale(property,Vector2.one);mat.SetTextureOffset(property,Vector2.zero);}
     mat.SetVector("_MainTex_ST",new Vector4(1,1,0,0));
     if(stone||wood)mat.SetFloat("_BumpScale",stone?.25f:.35f);
     if(plain){
      // A neutral native building material, not an unrelated item/vegetation atlas.
      // Preserve authored blue-grey cloth, red trim, leather, paper and silver.
      foreach(string property in mat.GetTexturePropertyNames())mat.SetTexture(property,null);
      mat.SetTexture("_MainTex",Texture2D.whiteTexture);
      mat.SetFloat("_BumpScale",0);mat.SetFloat("_Metallic",original.GetFloat("_Metallic"));
      mat.SetFloat("_Glossiness",original.GetFloat("_Smoothness"));mat.SetFloat("_MetallicAlphaGloss",0);
      mat.SetVector("_Color",original.GetVector("_BaseLinear"));
      mat.SetColor("_EmissionColor",Color.clear);mat.SetColor("_Emissive",Color.clear);
     }
    }
   }catch{foreach(var pair in pending)Destroy(pair.Value);throw;}
   foreach(var pair in pending)pair.Key.sharedMaterial=pair.Value;
  }
  internal static void ReleasePreview(GameObject model){
   foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
    if(renderer.sharedMaterial&&renderer.sharedMaterial.name.StartsWith(Prefix+"RST_",StringComparison.Ordinal))Destroy(renderer.sharedMaterial);
  }
  static void Destroy(Material material){if(Application.isPlaying)UnityEngine.Object.Destroy(material);else UnityEngine.Object.DestroyImmediate(material);}
 }
}
