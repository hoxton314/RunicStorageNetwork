using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RunicStorage.Build {
public static partial class BuildAssets {
 public static void PreviewCodex(){
  GameObject model=null,stand=null;
  try{
   Check(!EditorApplication.isPlaying,"Play Mode prohibited");
   string output=Arg("-rsnOutput");Directory.CreateDirectory(output);
   model=BuildCodex(output);
   TerminalView(model,output+"/01-cover.png",CodexIconDirection,false,framing:.245f,ground:true);
   TerminalView(model,output+"/02-spine.png",new Vector3(-4,3,-5),false,framing:.245f,ground:true);
   model.transform.rotation=Quaternion.Euler(0,0,180);
   TerminalView(model,output+"/03-back.png",CodexIconDirection,false,framing:.245f,ground:true);
   model.transform.rotation=Quaternion.identity;
   var renderers=model.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;
   foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
   File.Copy(CodexIcon,output+"/04-inventory-256.png",true);
   RenderIcon(model,bounds,output+"/05-inventory-64.png",RenderingPath.Forward,64,.255f,CodexIconDirection);
   // Re-render the existing book with the same lighting and shared material binder.
   var reference=AssetDatabase.LoadAssetAtPath<GameObject>(TerminalAsset);Check(reference,"Prepared stand model missing");
   stand=UnityEngine.Object.Instantiate(reference);stand.name="RSN_RunicStorageTerminal";
   TerminalView(stand,output+"/06-open-book-reference.png",new Vector3(2,3.6f,-5),true);
   Debug.Log("RSN_CODEX_PREVIEW_SUCCESS");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  finally{if(model)UnityEngine.Object.DestroyImmediate(model);if(stand)UnityEngine.Object.DestroyImmediate(stand);}
 }
}
}
