using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RunicStorage.Build {
public static partial class BuildAssets {
 const string TerminalSource="Assets/RunicStorage/TerminalSource";
 const string TerminalAsset=Root+"/RSN_RunicStorageTerminal.prefab";
 const string TerminalIcon=Root+"/RSN_TerminalIcon.png";
 [Serializable] class TerminalMaterials {public TerminalMaterial[] materials;}
 [Serializable] class TerminalEngraving {public EngravedMesh[] items,source;public string sourceSHA256;}
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
   ApplyTerminalEngraving(go,modelPath);
   var bounds=renderers[0].bounds;foreach(var renderer in renderers){
    Check(materials.ContainsKey(renderer.name),"Unknown terminal mesh "+renderer.name);
    renderer.sharedMaterial=materials[renderer.name];renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;bounds.Encapsulate(renderer.bounds);
    if(renderer.name=="RST_Stone"||renderer.name=="RST_Timber")SurfaceUv(renderer,renderer.name=="RST_Stone");
    // Physical surfaces of the stand/book, without a solid box spanning the gaps.
    if(new[]{"RST_Stone","RST_Timber","RST_Iron","RST_Silver","RST_Leather","RST_PageEdges","RST_Parchment"}.Contains(renderer.name)){
     var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
     // Valheim ignores non-convex meshes when anchoring the placement ghost.
     // Use the solid stone base as the anchor; retain the other surface shapes.
     collider.convex=renderer.name=="RST_Stone";
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
  int triangles=built.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
  int placements=ValidateTerminalPlacement(built);
  File.WriteAllText(Path.Combine(output,"TerminalAssetReport.json"),"{\"source\":\"v19\",\"triangles\":"+triangles+",\"renderers\":13,\"colliders\":7,\"convexBase\":true,\"placementChecks\":"+placements+",\"oldPlacementFailureReproduced\":true,\"pedestalEngraved\":true,\"bundleReload\":true,\"interaction\":false}");
 }
 static int ValidateTerminalPlacement(GameObject prefab){
  // Editor physics only: replay the collider-selection/ClosestPoint calculation
  // in the installed Player.UpdatePlacementGhost, without loading game scripts.
  var ghost=UnityEngine.Object.Instantiate(prefab);
  try{
   ghost.SetActive(true);
   var colliders=ghost.GetComponentsInChildren<MeshCollider>();
   var stone=colliders.Single(c=>c.name=="RST_Stone");
   Check(stone.convex&&colliders.Count(c=>c.convex)==1,"Terminal needs a convex stone placement anchor");
   int checks=0;
   foreach(var point in new[]{Vector3.zero,new Vector3(160,58,-96),new Vector3(-2030,87,5000)})
   foreach(float yaw in new[]{0f,37f,135f,270f}){
    var result=TerminalPlacementPosition(ghost,point,Vector3.up,Quaternion.Euler(0,yaw,0),out int anchors);
    Check(anchors==1&&Vector3.Distance(result,point)<.015f,"Terminal placement drift: "+(result-point));checks++;
   }
   var slopePoint=new Vector3(1700,60,-900);
   foreach(var normal in new[]{new Vector3(.25f,1,-.15f).normalized,new Vector3(-.4f,1,.2f).normalized})
   foreach(float yaw in new[]{0f,90f,225f}){
    var result=TerminalPlacementPosition(ghost,slopePoint,normal,Quaternion.Euler(0,yaw,0),out int anchors);
    Check(anchors==1&&Vector3.Distance(result,slopePoint)<2f,"Terminal placement escapes sloped surface");checks++;
   }
   stone.convex=false;
   var wrong=TerminalPlacementPosition(ghost,slopePoint,Vector3.up,Quaternion.identity,out int missing);
   Check(missing==0&&Vector3.Distance(wrong,slopePoint)>100f,"Placement check failed to reproduce the old all-concave collider bug");
   Debug.Log("RSN_TERMINAL_PLACEMENT_OK checks="+checks+"; old collider setup reproduces distant ghost");return checks;
  }finally{UnityEngine.Object.DestroyImmediate(ghost);}
 }
 static Vector3 TerminalPlacementPosition(GameObject ghost,Vector3 point,Vector3 normal,Quaternion rotation,out int anchors){
  ghost.transform.SetPositionAndRotation(point+normal*50f,rotation);Physics.SyncTransforms();
  var closest=Vector3.zero;float distance=float.MaxValue;anchors=0;
  foreach(var collider in ghost.GetComponentsInChildren<Collider>()){
   if(collider.isTrigger||!collider.enabled||collider is MeshCollider mesh&&!mesh.convex)continue;
   anchors++;var candidate=collider.ClosestPoint(point);float next=Vector3.Distance(candidate,point);
   if(next<distance){closest=candidate;distance=next;}
  }
  return point+ghost.transform.position-closest;
 }
 static void ApplyTerminalEngraving(GameObject go,string modelPath){
  var data=JsonUtility.FromJson<TerminalEngraving>(File.ReadAllText(TerminalSource+"/TerminalEngraving.json"));
  Check(data.items.Length==2&&data.source.Length==2,"Terminal engraving data missing");
  using(var sha=SHA256.Create())Check(string.Equals(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(modelPath))).Replace("-",""),data.sourceSHA256,StringComparison.OrdinalIgnoreCase),"Terminal engraving source is stale; regenerate from the current FBX");
  var filters=go.GetComponentsInChildren<MeshFilter>();
  foreach(var item in data.source){
   Check(item.name=="RST_Stone"||item.name=="RST_BaseRune","Unexpected terminal engraving target");
   var filter=filters.Single(f=>f.name==item.name);
   var points=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
   Check(item.vertices.All(p=>points.Any(q=>(p-q).sqrMagnitude<1e-10f)),"Terminal Blender/Unity coordinate mismatch: "+item.name);
  }
  foreach(var item in data.items){
   Check(item.name=="RST_Stone"||item.name=="RST_BaseRune","Unexpected terminal engraving output");
   var filter=filters.Single(f=>f.name==item.name);
   var mesh=new Mesh{name="RSN_Terminal_"+item.name+"_Engraved"};
   mesh.vertices=item.vertices.Select(v=>filter.transform.InverseTransformPoint(go.transform.TransformPoint(v))).ToArray();
   mesh.normals=item.normals.Select(n=>filter.transform.InverseTransformDirection(go.transform.TransformDirection(n))).ToArray();
   mesh.triangles=item.triangles;mesh.RecalculateBounds();
   Directory.CreateDirectory(Root+"/Meshes");var path=Root+"/Meshes/"+mesh.name+".asset";
   var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(saved){
    saved.Clear();saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.triangles=mesh.triangles;saved.RecalculateBounds();EditorUtility.SetDirty(saved);
    UnityEngine.Object.DestroyImmediate(mesh);
   }else{AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
   filter.sharedMesh=saved;
  }
 }
}
}
