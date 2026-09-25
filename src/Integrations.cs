using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace RunicStorageNetwork {
 internal static class Integrations {
  static Type blockType;static MethodInfo getBlock,isBlocked,fallback;
  internal static void Install(Harmony h){
   foreach(var plugin in Chainloader.PluginInfos.Values){
    string key=(plugin.Metadata.GUID+" "+plugin.Metadata.Name).ToLowerInvariant();
    if(new[]{"nearbycrafting","azucraftyboxes","dvergerautomation","craftfromcontainers","craftfromchests"}.Any(key.Contains))Plugin.Disable("overlapping supply mod: "+plugin.Metadata.GUID);
    if(new[]{"epicloot","extraslots","backpack","quickstack","warfare","armory","magicplugin","xportal","multiuserchest"}.Any(key.Contains))Plugin.Info("Integration detected: "+plugin.Metadata.GUID+" "+plugin.Metadata.Version);
   }
   var quick=Chainloader.PluginInfos.Values.FirstOrDefault(p=>p.Metadata.Name.ToLowerInvariant().Contains("quick stack")||p.Metadata.GUID.ToLowerInvariant().Contains("quickstackstore"));
   if(quick!=null){
    if(quick.Metadata.Version.ToString()!="1.4.15")Plugin.Disable("unverified Quick Stack API "+quick.Metadata.Version);
    else {
     var assembly=quick.Instance.GetType().Assembly;int count=0;
     foreach(string name in new[]{"QuickStackModule","RestockModule","StoreTakeAllModule","SortModule"})foreach(var method in assembly.GetType("QuickStackStore."+name,true).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(m=>m.GetParameters().Any(p=>p.ParameterType==typeof(Inventory)||p.ParameterType==typeof(Player)||p.ParameterType==typeof(Container)))){
      h.Patch(method,prefix:new HarmonyMethod(typeof(Integrations),nameof(QuickGuard)){priority=Priority.First});count++;
     }
     if(count==0)throw new MissingMethodException("Quick Stack guard entrypoints");Plugin.Info("Quick Stack 1.4.15 guarded entrypoints="+count);
     if(!Chainloader.PluginInfos.ContainsKey("com.maxsch.valheim.MultiUserChest")){
      var options=assembly.GetType("QuickStackStore.QSSConfig+QuickStackRestockConfig",true);
      var field=options.GetField("AllowAreaStackingInMultiplayerWithoutMUC",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
      if(field==null)throw new MissingFieldException("Quick Stack multiplayer safety setting");
      var config=(BepInEx.Configuration.ConfigEntry<bool>)field.GetValue(null);
      Action check=()=>{if(config.Value)Plugin.Disable("Quick Stack allows unacknowledged multiplayer ownership claims without MultiUserChest");};check();config.SettingChanged+=(s,e)=>check();
     }
    }
   }
   var muc=Chainloader.PluginInfos.Values.FirstOrDefault(p=>p.Metadata.Name=="MultiUserChest"||p.Metadata.GUID.ToLowerInvariant().Contains("multiuserchest"));
   if(muc==null){Plugin.Info("MultiUserChest absent: vanilla owner protocol");return;}
   if(muc.Metadata.Version.ToString()!="0.6.2"){Plugin.Disable("unverified MultiUserChest API "+muc.Metadata.Version);return;}
   var asm=muc.Instance.GetType().Assembly;blockType=asm.GetType("MultiUserChest.InventoryBlock",true);
   getBlock=blockType.GetMethod("Get",new[]{typeof(Inventory)});isBlocked=blockType.GetMethod("IsAnySlotBlocked",Type.EmptyTypes);
   var handlers=asm.GetType("MultiUserChest.ContainerRPCHandler",true);
   fallback=handlers.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="FallbackResponse"&&m.IsGenericMethodDefinition);
   foreach(string name in new[]{"RequestItemAdd","RequestItemRemove","RequestItemConsume","RequestItemMove","RequestDrop"}){
    var methods=handlers.GetMethods(BindingFlags.Static|BindingFlags.Public).Where(m=>m.Name==name&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(Inventory)).ToArray();
    if(methods.Length!=1)throw new MissingMethodException("MUC "+name);
    h.Patch(methods[0],prefix:new HarmonyMethod(typeof(Integrations),nameof(MucRequest)){priority=Priority.First});Plugin.Info("MUC owner handler protected: "+methods[0]);
   }
   Plugin.Info("MultiUserChest 0.6.2 adapter: InventoryBlock + owner request fallback; busy chests excluded");
  }
  internal static bool IsBusy(Inventory inv)=>blockType!=null&&(bool)isBlocked.Invoke(getBlock.Invoke(null,new object[]{inv}),null);
  internal static void Block(Inventory inv,bool value){if(blockType==null)return;var b=getBlock.Invoke(null,new object[]{inv});blockType.GetProperty("BlockAllSlots").SetValue(b,value,null);}
  static bool MucRequest(Inventory __0,object __1,MethodBase __originalMethod,ref object __result){
   if(!Transport.Locked(__0))return true;
   __result=fallback.MakeGenericMethod(((MethodInfo)__originalMethod).ReturnType).Invoke(null,new[]{__1});return false;
  }
  static bool QuickGuard(object[] __args){if(Transport.InternalMutation>0)return true;return !__args.Any(a=>a is Inventory i&&Transport.Locked(i)||a is Player p&&Transport.Locked(p.GetInventory())||a is Container c&&(Transport.Locked(c.GetInventory())||Transport.Reserved(R.View(c)?.GetZDO())));}
 }
}
