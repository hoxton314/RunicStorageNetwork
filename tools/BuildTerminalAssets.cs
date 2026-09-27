using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RunicStorage.Build {
public static partial class BuildAssets {
 const string TerminalSource="Assets/RunicStorage/TerminalSource";
 const string TerminalAsset=Root+"/RSN_RunicStorageTerminal.prefab";
 const string TerminalIcon=Root+"/RSN_TerminalIcon.png";
 [Serializable] class TerminalMaterials {public TerminalMaterial[] materials;}
 [Serializable] class TerminalMaterial {
  public string name;public string[] objects;public float[] base_color,emission_color;
  public float metallic,roughness,emission_strength;public bool backface_culling;
 }
 static Vector4 TerminalColor(float[] values){
  Check(values!=null&&values.Length==4,"Terminal material color missing");
  return new Vector4(values[0],values[1],values[2],values[3]);
 }
 static GameObject BuildTerminal(string output){
  var records=JsonUtility.FromJson<TerminalMaterials>(File.ReadAllText(TerminalSource+"/materials.json")).materials;
  Check(records.Length==13&&records.SelectMany(r=>r.objects).Distinct().Count()==13,"Expected v19 terminal materials");
  var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/CoreRuntime.shader");
  Check(shader&&!ShaderUtil.ShaderHasError(shader),"Terminal runtime shader missing");
  var materials=new System.Collections.Generic.Dictionary<string,Material>();
  foreach(var record in records){
   Check(record.objects.Length==1,"Unexpected terminal material grouping");
   string slot=record.objects[0],path=Root+"/Materials/"+slot+".mat";
   var material=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
   material.shader=shader;material.name=slot;
   material.SetVector("_BaseLinear",TerminalColor(record.base_color));
   material.SetVector("_EmissionLinear",TerminalColor(record.emission_color));
   material.SetFloat("_Metallic",record.metallic);material.SetFloat("_Smoothness",1-record.roughness);
   material.SetFloat("_EmissionStrength",record.emission_strength);material.SetFloat("_Cull",record.backface_culling?2:0);
   material.SetFloat("_UseFacetMask",0);material.SetColor("_Color",Color.white);material.SetColor("_EmissionColor",Color.clear);
   EditorUtility.SetDirty(material);materials.Add(slot,material);
  }
  string modelPath=TerminalSource+"/RunicStorageTerminal.fbx";
  var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);Check(importer,"Terminal FBX missing");
  importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
  importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.None;
  importer.meshCompression=ModelImporterMeshCompression.Off;importer.weldVertices=false;
  importer.optimizeMeshPolygons=false;importer.optimizeMeshVertices=false;importer.isReadable=true;
  importer.generateSecondaryUV=false;importer.preserveHierarchy=true;importer.addCollider=false;
  importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.None;
  importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
  foreach(var record in records)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),record.name),materials[record.objects[0]]);
  importer.SaveAndReimport();
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);Check(model,"Terminal FBX import failed");
  var go=new GameObject("RSN_RunicStorageTerminal");
  try {
   var child=(GameObject)PrefabUtility.InstantiatePrefab(model);child.transform.SetParent(go.transform,false);
   PrefabUtility.UnpackPrefabInstance(child,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   var renderers=go.GetComponentsInChildren<MeshRenderer>();
   Check(renderers.Length==13&&go.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)==3710,"Terminal v19 geometry changed");
   var bounds=renderers[0].bounds;foreach(var renderer in renderers){
    Check(materials.ContainsKey(renderer.name),"Unknown terminal mesh "+renderer.name);
    renderer.sharedMaterial=materials[renderer.name];renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;bounds.Encapsulate(renderer.bounds);
    if(renderer.name=="RST_Stone"||renderer.name=="RST_Timber")SurfaceUv(renderer,renderer.name=="RST_Stone");
    // Physical surfaces of the stand/book, without a solid box spanning the gaps.
    if(new[]{"RST_Stone","RST_Timber","RST_Iron","RST_Silver","RST_Leather","RST_PageEdges","RST_Parchment"}.Contains(renderer.name)){
     var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;collider.convex=false;
    }
   }
   Check(Vector3.Distance(bounds.size,new Vector3(1.082f,1.494148f,.820455f))<.002f&&Mathf.Abs(bounds.min.y)<.001f,"Terminal scale/origin differs from v19");
   Check(go.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"Terminal must remain a visual build piece");
   PrefabUtility.SaveAsPrefabAsset(go,TerminalAsset);
   RenderIcon(go,bounds,TerminalIcon,RenderingPath.Forward,256,1.1f);
   AssetDatabase.Refresh();var icon=(TextureImporter)AssetImporter.GetAtPath(TerminalIcon);
   icon.textureType=TextureImporterType.Sprite;icon.spriteImportMode=SpriteImportMode.Single;icon.mipmapEnabled=false;icon.alphaIsTransparency=true;icon.textureCompression=TextureImporterCompression.Uncompressed;icon.SaveAndReimport();AssetDatabase.SaveAssets();
   return go;
  }catch{UnityEngine.Object.DestroyImmediate(go);throw;}
 }
 static void ValidateTerminal(AssetBundle bundle,GameObject prepared,string output){
  var built=bundle.LoadAsset<GameObject>(TerminalAsset);
  Check(built&&built.name=="RSN_RunicStorageTerminal"&&bundle.LoadAsset<Sprite>(TerminalIcon),"Terminal bundle assets missing");
  Check(built.GetComponentsInChildren<MeshRenderer>().Length==13&&built.GetComponentsInChildren<MeshCollider>().Length==7,"Terminal mesh/collision count differs");
  Check(built.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&built.GetComponentsInChildren<Camera>(true).Length==0&&built.GetComponentsInChildren<Light>(true).Length==0,"Unexpected terminal components");
  foreach(var renderer in built.GetComponentsInChildren<MeshRenderer>()){
   var expected=prepared.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name==renderer.name);
   var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var source=expected.GetComponent<MeshFilter>().sharedMesh;
   Check(mesh.vertices.SequenceEqual(source.vertices)&&mesh.normals.SequenceEqual(source.normals)&&mesh.triangles.SequenceEqual(source.triangles),"Terminal bundle geometry changed");
   Check(renderer.sharedMaterial&&renderer.sharedMaterial.shader&&!ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader),"Terminal material missing");
  }
  File.WriteAllText(Path.Combine(output,"TerminalAssetReport.json"),"{\"source\":\"v19\",\"triangles\":3710,\"renderers\":13,\"colliders\":7,\"bundleReload\":true,\"interaction\":false}");
 }
}
}
