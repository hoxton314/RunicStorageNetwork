using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RunicStorage.Build {
public static partial class BuildAssets {
 static void SurfaceUv(MeshRenderer renderer,bool stone) {
  var filter=renderer.GetComponent<MeshFilter>();var source=filter.sharedMesh;
  var vertices=source.vertices;var normals=source.normals;var indices=source.triangles;
  Check(normals.Length==vertices.Length,"Source normals missing");
  // Weld coincident positions only for finding separate blocks/posts; do not alter geometry.
  var parent=Enumerable.Range(0,vertices.Length).ToArray();
  Func<int,int> find=null;find=i=>parent[i]==i?i:(parent[i]=find(parent[i]));
  Action<int,int> join=(a,b)=>parent[find(a)]=find(b);
  var welded=new System.Collections.Generic.Dictionary<Vector3Int,int>();
  for(int i=0;i<vertices.Length;i++){
   var v=vertices[i];var key=new Vector3Int(Mathf.RoundToInt(v.x*10000),Mathf.RoundToInt(v.y*10000),Mathf.RoundToInt(v.z*10000));
   if(welded.TryGetValue(key,out int other))join(i,other);else welded[key]=i;
  }
  for(int i=0;i<indices.Length;i+=3){join(indices[i],indices[i+1]);join(indices[i],indices[i+2]);}
  var bounds=new System.Collections.Generic.Dictionary<int,Bounds>();
  for(int i=0;i<vertices.Length;i++){
   int group=find(i);Vector3 p=renderer.transform.TransformPoint(vertices[i]);
   if(bounds.TryGetValue(group,out var b)){b.Encapsulate(p);bounds[group]=b;}else bounds[group]=new Bounds(p,Vector3.zero);
  }
  var dst=new Vector3[indices.Length];var ns=new Vector3[indices.Length];var uv=new Vector2[indices.Length];
  for(int i=0;i<indices.Length;i+=3){
   var a=renderer.transform.TransformPoint(vertices[indices[i]]);var b=renderer.transform.TransformPoint(vertices[indices[i+1]]);var c=renderer.transform.TransformPoint(vertices[indices[i+2]]);
   var face=Vector3.Cross(b-a,c-a).normalized;
   int axis=Mathf.Abs(face.y)>Mathf.Abs(face.x)&&Mathf.Abs(face.y)>Mathf.Abs(face.z)?1:(Mathf.Abs(face.x)>Mathf.Abs(face.z)?0:2);
   for(int j=0;j<3;j++){
    int k=i+j,index=indices[k],group=find(index);var box=bounds[group];var p=renderer.transform.TransformPoint(vertices[index]);
    dst[k]=vertices[index];ns[k]=normals[index];
    if(stone){
     Vector3 q=p-box.min;Vector3 size=box.size;
     float u=axis==0?q.z/Mathf.Max(size.z,0.0001f):q.x/Mathf.Max(size.x,0.0001f);
     float v=axis==1?q.z/Mathf.Max(size.z,0.0001f):q.y/Mathf.Max(size.y,0.0001f);
     // Verified stone_wall_2x1 UV region: x=.012..72, y=.002..22.
     // Stay inside its opaque stone patch, with a margin for mip filtering.
     uv[k]=new Vector2(0.035f+(group%3)*0.21f+Mathf.Clamp01(u)*0.16f,0.025f+Mathf.Clamp01(v)*0.16f);
    }else{
     // Planks5 grain runs horizontally in texture space: U follows the post height.
     float across=axis==0?p.z:p.x;
     uv[k]=axis==1?new Vector2(p.x*0.75f,p.z*0.75f):new Vector2(p.y*0.5f,across*0.75f);
    }
   }
  }
  string slot=renderer.sharedMaterial.name;
  var mesh=new Mesh{name=slot.StartsWith("RST_")?"RSN_Terminal_"+slot+"_SurfaceUV":slot.StartsWith("RR_")?"RSN_Relay_"+slot+"_SurfaceUV":(slot=="SNC_Plinth"?"RSN_Plinth_SurfaceUV":(stone?"RSN_Stone_SurfaceUV":"RSN_Timber_SurfaceUV"))};
  mesh.vertices=dst;mesh.normals=ns;mesh.uv=uv;mesh.triangles=Enumerable.Range(0,indices.Length).ToArray();
  if(source.colors.Length==vertices.Length)mesh.colors=indices.Select(i=>source.colors[i]).ToArray();
  mesh.RecalculateTangents();mesh.RecalculateBounds();
  Check(mesh.triangles.Length==indices.Length && mesh.tangents.All(t=>!float.IsNaN(t.x)&&!float.IsInfinity(t.x)&&new Vector3(t.x,t.y,t.z).sqrMagnitude>0.5f),"Invalid surface tangents");
  for(int i=0;i<indices.Length;i++)Check(dst[i]==vertices[indices[i]]&&ns[i]==normals[indices[i]],"Surface mapping changed geometry");
  Directory.CreateDirectory(Root+"/Meshes");string path=Root+"/Meshes/"+mesh.name+".asset";
  var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(saved){
   // Mesh setters invalidate the GPU buffers as well as the serialized asset.
   saved.Clear();saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.uv=mesh.uv;saved.triangles=mesh.triangles;
   saved.colors=mesh.colors;saved.tangents=mesh.tangents;saved.RecalculateBounds();EditorUtility.SetDirty(saved);
   UnityEngine.Object.DestroyImmediate(mesh);
  }else{AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
  filter.sharedMesh=saved;
  Debug.Log("RSN_SURFACE_UV "+saved.name+" groups="+bounds.Count+" triangles="+indices.Length/3+"; positions/normals unchanged; tangents rebuilt");
 }
}
}
