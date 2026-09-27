using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace RunicStorageNetwork {
 internal static class RunicCodexItem {
  internal const string PrefabName="RSN_RunicCodex";
  internal static GameObject Register(AssetBundle bundle){
   var prefab=bundle.LoadAsset<GameObject>("assets/runicstoragegame/rsn_runiccodex.prefab");
   var icon=bundle.LoadAsset<Sprite>("assets/runicstoragegame/rsn_codexicon.png");
   if(!prefab||prefab.name!=PrefabName||!icon||!prefab.transform.Find("attach")||!prefab.GetComponent<BoxCollider>())throw new InvalidOperationException("Runic Codex item assets missing");
   prefab.SetActive(false);
   foreach(var t in prefab.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("item");
   var body=prefab.AddComponent<Rigidbody>();body.mass=1;body.useGravity=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
   var view=prefab.AddComponent<ZNetView>();view.m_persistent=true;view.m_type=ZDO.ObjectType.Default;
   var sync=prefab.AddComponent<ZSyncTransform>();sync.m_syncPosition=true;sync.m_syncRotation=true;sync.m_syncBodyVelocity=true;
   var drop=prefab.AddComponent<ItemDrop>();
   drop.m_itemData.m_shared=new ItemDrop.ItemData.SharedData{
    m_name="$rsn_runic_codex_name",m_description="$rsn_runic_codex_description",m_itemType=ItemDrop.ItemData.ItemType.Material,
    m_maxStackSize=1,m_maxQuality=1,m_weight=1,m_teleportable=true,m_useDurability=false,m_icons=new[]{icon}
   };
   drop.m_itemData.m_dropPrefab=prefab;drop.m_itemData.m_stack=1;drop.m_itemData.m_quality=1;
   var config=new ItemConfig{
    Name=drop.m_itemData.m_shared.m_name,Description=drop.m_itemData.m_shared.m_description,Icon=icon,
    CraftingStation="forge",MinStationLevel=1,Amount=1,Enabled=true,RequireOnlyOneIngredient=false,
    Requirements=new[]{new RequirementConfig("Silver",4),new RequirementConfig("Crystal",2),new RequirementConfig("GreydwarfEye",6),new RequirementConfig("LinenThread",4),new RequirementConfig("LeatherScraps",4)}
   };
   if(!ItemManager.Instance.AddItem(new CustomItem(prefab,false,config)))throw new InvalidOperationException("Jotunn rejected Runic Codex");
   prefab.SetActive(true);Plugin.Info(PrefabName+" registered with forge recipe");return prefab;
  }
 }
}
