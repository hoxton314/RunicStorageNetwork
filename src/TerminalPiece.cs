using System;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace RunicStorageNetwork {
 // Stable save/prefab identity; the player-facing name is localized separately.
 internal static class TerminalPiece {
  internal const string PrefabName="RSN_RunicStorageTerminal";
  internal static GameObject Register(AssetBundle bundle){
   var prefab=bundle.LoadAsset<GameObject>("assets/runicstoragegame/rsn_runicstorageterminal.prefab");
   var icon=bundle.LoadAsset<Sprite>("assets/runicstoragegame/rsn_terminalicon.png");
   if(!prefab||!icon)throw new InvalidOperationException("Runic Storage Terminal assets missing");
   if(prefab.name!=PrefabName)throw new InvalidOperationException("Unexpected terminal prefab identity");
   if(!prefab.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled&&!c.isTrigger&&(!(c is MeshCollider mesh)||mesh.convex)))throw new InvalidOperationException("Storage Codex has no placement anchor. Update the AssetBundle together with the DLL.");
   prefab.SetActive(false);
   foreach(var t in prefab.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("piece");
   var view=prefab.AddComponent<ZNetView>();view.m_persistent=true;view.m_type=ZDO.ObjectType.Default;
   var piece=prefab.AddComponent<Piece>();piece.m_name="$rsn_codex_name";piece.m_description="$rsn_codex_description";piece.m_icon=icon;piece.m_canBeRemoved=true;
   var wear=prefab.AddComponent<WearNTear>();wear.m_health=400;wear.m_materialType=WearNTear.MaterialType.Stone;wear.m_noRoofWear=true;wear.m_noSupportWear=false;wear.m_burnable=false;
   prefab.AddComponent<StorageCodex>();piece.m_usage=Piece.UsageTagFlags.Storage|Piece.UsageTagFlags.Crafting;
   var config=new PieceConfig{Name=piece.m_name,Description=piece.m_description,PieceTable="Hammer",CraftingStation="piece_workbench",Category="Crafting",Usage=new[]{"Storage","Crafting"},Requirements=new[]{new RequirementConfig(RunicCodexItem.PrefabName,1,0,true),new RequirementConfig("FineWood",10,0,true),new RequirementConfig("Stone",8,0,true),new RequirementConfig("Iron",2,0,true),new RequirementConfig("JuteRed",2,0,true)}};
   if(!PieceManager.Instance.AddPiece(new CustomPiece(prefab,false,config)))throw new InvalidOperationException("Jotunn rejected Storage Codex");
   prefab.SetActive(true);Plugin.Info(PrefabName+" registered with Hammer (network storage access)");
   return prefab;
  }
 }
}
