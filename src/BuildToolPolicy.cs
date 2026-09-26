using System.Collections.Generic;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 internal static class BuildToolPolicy {
  static readonly Dictionary<GameObject,string> components=new Dictionary<GameObject,string>();
  static BuildToolRules rules;static ObjectDB database;
  internal static void Invalidate(){database=null;rules=null;components.Clear();}
  static string Text(BepInEx.Configuration.ConfigEntry<string> entry)=>entry==null?"":entry.Value??"";
  internal static BuildToolRules Rules {
   get {
    if(database!=ObjectDB.instance){Invalidate();database=ObjectDB.instance;}
    if(rules==null)rules=new BuildToolRules(Text(Plugin.AllowedBuildTools),Text(Plugin.DeniedBuildTools),Text(Plugin.DeniedPieceComponents));
    return rules;
   }
  }

  // Serving trays use the same placement/requirements flow as other build tools.
  // Removal capabilities do not determine whether a menu can draw from the network.
  internal static bool Table(PieceTable table)=>table;
  internal static bool Tool(ItemDrop.ItemData tool)=>tool!=null&&tool.m_shared!=null&&tool.m_dropPrefab&&Table(tool.m_shared.m_buildPieces)&&Rules.Tool(R.Id(tool.m_dropPrefab));

  internal static string PieceReason(GameObject prefab){
   if(!prefab)return "invalid build piece";
   var piece=prefab.GetComponent<Piece>();
   if(!piece||!piece.m_enabled||piece.m_repairPiece||piece.m_removePiece)return "invalid build piece";
   var current=Rules;
   if(!components.TryGetValue(prefab,out string reason)){
    reason=current.Piece(R.Components(prefab))?null:BuildToolRules.ExcludedPieceReason;
    components[prefab]=reason;
   }
   return reason;
  }

  // Called for transaction validation, not per menu entry. Read live membership so
  // pieces/tools registered or removed after the menu first opened take effect.
  internal static string Reason(GameObject prefab){
   string reason=PieceReason(prefab);if(reason!=null)return reason;
   if(!ObjectDB.instance||ObjectDB.instance.m_items==null)return "supply unavailable";
   foreach(var item in ObjectDB.instance.m_items){
    var drop=item?item.GetComponent<ItemDrop>():null;
    var table=drop?drop.m_itemData?.m_shared?.m_buildPieces:null;
    if(Table(table)&&Rules.Tool(item.name)&&table.m_pieces!=null&&table.m_pieces.Contains(prefab))return null;
   }
   return BuildToolRules.ExcludedToolReason;
  }
  internal static bool Eligible(GameObject prefab)=>Reason(prefab)==null;
 }
}
