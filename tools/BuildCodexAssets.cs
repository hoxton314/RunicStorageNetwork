using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RunicStorage.Build {
public static partial class BuildAssets {
 const string CodexSource="Assets/RunicStorage/CodexSource";
 const string CodexAsset=Root+"/RSN_RunicCodex.prefab";
 const string CodexIcon=Root+"/RSN_CodexIcon.png";
 const string CodexFbxSha256="6807987f8463347a7152fc9a354ba3790ad2c99835ec5d09fc61cc129fb2511f";
 static readonly Vector3 CodexIconDirection=new Vector3(4,6,-5);

 // Item visual and compact collision shape only. ItemDrop, networking and inventory
 // registration belong to the later item integration, not this art preparation pass.
 static GameObject BuildCodex(string output){
  string modelPath=CodexSource+"/RunicCodex.fbx";
  using(var sha=SHA256.Create())Check(string.Equals(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(modelPath))).Replace("-",""),CodexFbxSha256,StringComparison.OrdinalIgnoreCase),"Codex export differs from reviewed v04");
  var records=JsonUtility.FromJson<TerminalMaterials>(File.ReadAllText(CodexSource+"/materials.json")).materials;
  var stand=JsonUtility.FromJson<TerminalMaterials>(File.ReadAllText(TerminalSource+"/materials.json")).materials;
  Check(records.Length==7&&records.SelectMany(r=>r.objects).Distinct().Count()==7,"Expected seven v04 codex materials");
  var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/CoreRuntime.shader");
  Check(shader&&!ShaderUtil.ShaderHasError(shader),"Codex shader missing");
  var materials=new System.Collections.Generic.Dictionary<string,Material>();
  foreach(var record in records){
   Check(record.objects.Length==1,"Unexpected codex material grouping");
   string slot=record.objects[0],path=Root+"/Materials/"+slot+".mat";
   var material=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
   material.shader=shader;material.name=slot;
   var baseColor=TerminalColor(record.base_color);var emission=TerminalColor(record.emission_color);
   if(slot=="RC_RunesPrimary"||slot=="RC_RunesSecondary"||slot=="RC_Crystal"){
    // Match the open book's cyan hue, retaining the closed book's quieter
    // secondary marks and faceted clasp rather than one uniform glow.
    var palette=stand.Single(r=>r.objects[0]==(slot=="RC_RunesSecondary"?"RST_BookSmallRunes":"RST_BookMainRune"));
    baseColor=TerminalColor(palette.base_color);emission=TerminalColor(palette.emission_color);
   }
   material.SetVector("_BaseLinear",baseColor);material.SetVector("_EmissionLinear",emission);
   // Brushed silver keeps some diffuse response on the horizontal closed cover;
   // the stand's tilted, polished trim retains its existing settings.
   material.SetFloat("_Metallic",slot=="RC_Silver"?.82f:record.metallic);material.SetFloat("_Smoothness",slot=="RC_Silver"?.55f:1-record.roughness);
   material.SetFloat("_EmissionStrength",record.emission_strength);material.SetFloat("_Cull",record.backface_culling?2:0);
   material.SetFloat("_UseFacetMask",0);material.SetColor("_Color",Color.white);material.SetColor("_EmissionColor",Color.clear);
   EditorUtility.SetDirty(material);materials.Add(slot,material);
  }
  var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);Check(importer,"Codex FBX missing");
  importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
  importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.None;
  importer.meshCompression=ModelImporterMeshCompression.Off;importer.weldVertices=false;
  importer.optimizeMeshPolygons=false;importer.optimizeMeshVertices=false;importer.isReadable=true;
  importer.generateSecondaryUV=false;importer.preserveHierarchy=true;importer.addCollider=false;
  importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.None;
  importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
  foreach(var record in records)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),record.name),materials[record.objects[0]]);
  importer.SaveAndReimport();
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);Check(model,"Codex FBX import failed");
  var go=new GameObject("RSN_RunicCodex");
  try{
   var visual=(GameObject)PrefabUtility.InstantiatePrefab(model);visual.transform.SetParent(go.transform,false);
   PrefabUtility.UnpackPrefabInstance(visual,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);visual.name="attach";
   var renderers=go.GetComponentsInChildren<MeshRenderer>();
   Check(renderers.Length==7&&go.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)==813,"Codex v04 geometry changed");
   var bounds=renderers[0].bounds;
   foreach(var renderer in renderers){
    Check(materials.ContainsKey(renderer.name),"Unknown codex mesh "+renderer.name);
    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
    Check(mesh.normals.Length==mesh.vertexCount&&mesh.normals.All(n=>n.sqrMagnitude>.99f&&n.sqrMagnitude<1.01f),"Codex normals missing or invalid: "+renderer.name);
    renderer.sharedMaterial=materials[renderer.name];renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;bounds.Encapsulate(renderer.bounds);
   }
   Check(Vector3.Distance(bounds.size,new Vector3(.253350f,.098500f,.382000f))<.0001f&&Mathf.Abs(bounds.min.y)<.00001f,"Codex scale/origin differs from v04");
   PrepareCodexClasp(go,materials["RC_Leather"]);
   renderers=go.GetComponentsInChildren<MeshRenderer>();
   var preparedBounds=renderers[0].bounds;foreach(var renderer in renderers)preparedBounds.Encapsulate(renderer.bounds);
   Check(Vector3.Distance(bounds.min,preparedBounds.min)<.00001f&&Vector3.Distance(bounds.max,preparedBounds.max)<.00001f,"Clasp refinement changed the item envelope");
   // The bookmark and fine glowing marks should not enlarge a dropped item's collider.
   var rigid=renderers.Where(r=>r.name=="RC_Leather"||r.name=="RC_ClaspLeather"||r.name=="RC_Silver").ToArray();
   var body=rigid[0].bounds;foreach(var renderer in rigid)body.Encapsulate(renderer.bounds);
   var collider=go.AddComponent<BoxCollider>();collider.center=body.center;collider.size=body.size;
   Check(collider.size.x>.2f&&collider.size.y>.05f&&collider.size.z>.25f&&collider.size.z<bounds.size.z,"Codex body collider dimensions invalid");
   PrefabUtility.SaveAsPrefabAsset(go,CodexAsset);AssetDatabase.SaveAssets();
   Check(go.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&go.GetComponentsInChildren<Rigidbody>().Length==0,"Unexpected item behavior in visual prefab");
   RenderIcon(go,bounds,CodexIcon,RenderingPath.Forward,256,.255f,CodexIconDirection);
   AssetDatabase.Refresh();var icon=(TextureImporter)AssetImporter.GetAtPath(CodexIcon);
   icon.textureType=TextureImporterType.Sprite;icon.spriteImportMode=SpriteImportMode.Single;icon.mipmapEnabled=false;icon.alphaIsTransparency=true;icon.textureCompression=TextureImporterCompression.Uncompressed;icon.SaveAndReimport();AssetDatabase.SaveAssets();
   var saved=AssetDatabase.LoadAssetAtPath<GameObject>(CodexAsset);
   Check(saved&&saved.transform.Find("attach")&&saved.GetComponent<BoxCollider>()&&AssetDatabase.LoadAssetAtPath<Sprite>(CodexIcon),"Codex visual assets were not saved");
   ValidateCodexVisual(saved,go);
   foreach(var renderer in saved.GetComponentsInChildren<MeshRenderer>())Check(renderer.sharedMaterial.shader==shader,"Native preview material was saved into item prefab");
   File.WriteAllText(Path.Combine(output,"CodexAssetReport.json"),"{\"source\":\"v04\",\"unity\":\""+Application.unityVersion+"\",\"triangles\":813,\"renderers\":8,\"boxColliders\":1,\"sourceNormals\":true,\"claspSeparateMaterial\":true,\"claspFrameScale\":1.18,\"dimensions\":[0.25335,0.0985,0.382],\"iconSize\":256,\"visualOnly\":true,\"bundleBuilt\":false,\"gameValidated\":false}");
   return go;
  }catch{UnityEngine.Object.DestroyImmediate(go);throw;}
 }
 static void ValidateCodex(AssetBundle bundle,GameObject prepared,string output){
  var built=bundle.LoadAsset<GameObject>(CodexAsset);
  Check(built&&bundle.LoadAsset<Sprite>(CodexIcon),"Codex bundle assets missing");
  ValidateCodexVisual(built,prepared);
  File.WriteAllText(Path.Combine(output,"CodexBundleReport.json"),"{\"triangles\":813,\"renderers\":8,\"boxColliders\":1,\"bundleReload\":true,\"visualOnly\":true,\"gameValidated\":false}");
 }
 static void ValidateCodexVisual(GameObject built,GameObject prepared){
  var renderers=built.GetComponentsInChildren<MeshRenderer>();
  Check(built.name=="RSN_RunicCodex"&&built.transform.Find("attach")&&renderers.Length==8,"Codex visual hierarchy changed");
  Check(built.GetComponentsInChildren<Collider>().Length==1&&built.GetComponent<BoxCollider>(),"Codex collision shape changed");
  Check(built.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&built.GetComponentsInChildren<Rigidbody>().Length==0,"Unexpected behavior in codex visual");
  foreach(var renderer in renderers){
   var expected=prepared.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name==renderer.name);
   var actual=renderer.GetComponent<MeshFilter>().sharedMesh;var source=expected.GetComponent<MeshFilter>().sharedMesh;
   Check(actual.vertices.SequenceEqual(source.vertices)&&actual.normals.SequenceEqual(source.normals)&&actual.triangles.SequenceEqual(source.triangles),"Saved codex geometry changed: "+renderer.name);
   Check(renderer.sharedMaterial&&renderer.sharedMaterial.name==expected.sharedMaterial.name&&renderer.sharedMaterial.shader&&!ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader),"Codex material missing: "+renderer.name);
  }
  var collider=built.GetComponent<BoxCollider>();var expectedCollider=prepared.GetComponent<BoxCollider>();
  Check(collider.center==expectedCollider.center&&collider.size==expectedCollider.size,"Saved codex collider changed");
 }
 static void PrepareCodexClasp(GameObject go,Material leather){
  var filter=go.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="RC_Leather");
  var source=filter.sharedMesh;var positions=source.vertices;var triangles=source.triangles;
  var strap=new System.Collections.Generic.List<int>();var cover=new System.Collections.Generic.List<int>();
  // All three strap islands in v04 are inside this small band. The source hash
  // and triangle count guard the selector when a future export replaces v04.
  Func<int,bool> onStrap=i=>{var p=filter.transform.TransformPoint(positions[i]);return p.x>.0579f&&Mathf.Abs(p.z)<.0126f;};
  for(int i=0;i<triangles.Length;i+=3){
   var target=onStrap(triangles[i])&&onStrap(triangles[i+1])&&onStrap(triangles[i+2])?strap:cover;
   target.Add(triangles[i]);target.Add(triangles[i+1]);target.Add(triangles[i+2]);
  }
  Check(strap.Count==36*3&&cover.Count+strap.Count==triangles.Length,"Codex clasp islands differ from v04");
  filter.sharedMesh=SaveCodexMesh(source,cover.ToArray(),"Cover");
  var clasp=new GameObject("RC_ClaspLeather");clasp.transform.SetParent(filter.transform.parent,false);
  clasp.transform.localPosition=filter.transform.localPosition;clasp.transform.localRotation=filter.transform.localRotation;clasp.transform.localScale=filter.transform.localScale;
  clasp.AddComponent<MeshFilter>().sharedMesh=SaveCodexMesh(source,strap.ToArray(),"Clasp");
  string path=Root+"/Materials/RC_ClaspLeather.mat";
  var material=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!material){material=new Material(leather);AssetDatabase.CreateAsset(material,path);}else material.CopyPropertiesFromMaterial(leather);
  material.name="RC_ClaspLeather";material.SetVector("_BaseLinear",new Vector4(.065f,.032f,.014f,1));EditorUtility.SetDirty(material);
  var renderer=clasp.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
  var silver=go.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="RC_Silver");
  var silverSource=silver.sharedMesh;var points=silverSource.vertices;int changed=0;
  for(int i=0;i<points.Length;i++){
   var p=silver.transform.TransformPoint(points[i]);
   if(p.x<.0629f||p.x>.0931f||Mathf.Abs(p.z)>.0231f||p.y<.0834f||p.y>.0861f)continue;
   p.x=.078f+(p.x-.078f)*1.18f;p.z*=1.18f;points[i]=silver.transform.InverseTransformPoint(p);changed++;
  }
  Check(changed>0&&changed<points.Length,"Codex clasp frame not found");
  silver.sharedMesh=SaveCodexMesh(silverSource,silverSource.triangles,"Silver",points);
  Check(go.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)==813,"Codex clasp refinement lost triangles");
 }
 static Mesh SaveCodexMesh(Mesh source,int[] triangles,string suffix,Vector3[] positions=null){
  var indices=triangles.Distinct().ToArray();var map=new System.Collections.Generic.Dictionary<int,int>();
  for(int i=0;i<indices.Length;i++)map.Add(indices[i],i);
  var vertices=positions??source.vertices;var normals=source.normals;
  string name="RSN_Codex_"+suffix,path=Root+"/Meshes/"+name+".asset";Directory.CreateDirectory(Root+"/Meshes");
  var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=!mesh;if(create)mesh=new Mesh{name=name};else mesh.Clear();
  mesh.vertices=indices.Select(i=>vertices[i]).ToArray();mesh.normals=indices.Select(i=>normals[i]).ToArray();
  var uv=source.uv;var colors=source.colors;var tangents=source.tangents;
  if(uv.Length==source.vertexCount)mesh.uv=indices.Select(i=>uv[i]).ToArray();
  if(colors.Length==source.vertexCount)mesh.colors=indices.Select(i=>colors[i]).ToArray();
  if(tangents.Length==source.vertexCount)mesh.tangents=indices.Select(i=>tangents[i]).ToArray();
  mesh.triangles=triangles.Select(i=>map[i]).ToArray();mesh.RecalculateBounds();
  if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);return mesh;
 }
}
}
