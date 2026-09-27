using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 // UI lifetime is independent of the building. A future runic book can open
 // this same view after resolving its network access point.
 public sealed class NetworkTerminal:MonoBehaviour {
  sealed class Entry {internal string Id,Name;internal int Quality,Count;internal ItemDrop Item;internal string Key=>Id+"/"+Quality;}
  sealed class Row {internal GameObject Object;internal RectTransform Rect;internal Image Icon;internal Text Name,Count;internal Button Button;internal Entry Entry;}
  static NetworkTerminal instance;
  Core core;Player player;GameObject panel;bool blocked;
  InputField search,quantity;Text heading,network,detail,available,carried,status,empty,qualityLabel;Image selectedIcon;Button take;
  ScrollRect scroll;RectTransform content;readonly List<Row> rows=new List<Row>();
  List<Entry> entries=new List<Entry>(),filtered=new List<Entry>();Entry selected;
  float nextRefresh,statusUntil;string query="",selectedKey;int visibleStart=-1;bool dirtyRows;
  static readonly Vector2 Center=new Vector2(.5f,.5f);
  static readonly Color Gold=new Color(1f,.79f,.42f),Muted=new Color(.78f,.75f,.67f);
  const float RowHeight=52;
  void Awake(){instance=this;}
  internal static bool Showing(Core value)=>instance&&instance.panel&&instance.panel.activeSelf&&instance.core==value;
  internal static bool Open(Core value,Player actor){
   if(!instance||!TerminalTransfer.CanUse(value,actor)||TerminalTransfer.Busy||Actions.Waiting!=null||CraftPreparation.HasReservation||!GUIManager.CustomGUIFront)return false;
   Close();if(InventoryGui.IsVisible())InventoryGui.instance.Hide();
   instance.core=value;instance.player=actor;
   try{instance.Create();StorageIndex.Reconcile(value);instance.Refresh();GUIManager.BlockInput(true);instance.blocked=true;return true;}
   catch(Exception e){Plugin.Error("terminal UI",e);Close();return false;}
  }
  internal static void Close(){
   if(!instance)return;TerminalTransfer.Cancel();
   if(instance.blocked){GUIManager.BlockInput(false);instance.blocked=false;}
   if(instance.panel)Destroy(instance.panel);instance.panel=null;instance.rows.Clear();instance.entries.Clear();instance.filtered.Clear();instance.selected=null;instance.selectedKey=null;instance.core=null;instance.player=null;instance.visibleStart=-1;instance.statusUntil=0;
  }
  void OnDestroy(){if(instance==this){Close();instance=null;}}
  void Update(){
   if(!panel)return;
   if(!TerminalTransfer.CanUse(core,player)||ZInput.GetKeyDown(KeyCode.Escape)||ZInput.GetButtonDown("JoyButtonB")){Close();return;}
   try{
    var parent=panel.transform.parent as RectTransform;
    if(parent){float scale=Mathf.Min(1f,parent.rect.width/1080f,parent.rect.height/720f);panel.transform.localScale=Vector3.one*Mathf.Max(.4f,scale);}
    if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+1;Refresh();}
    RenderRows();UpdateDetail();
   }catch(Exception e){Plugin.Error("terminal update",e);Close();}
  }
  static string T(string key,params object[] args)=>RsnLocalization.Text(key,args);
  Text Label(string text,Transform parent,float x,float y,float width,float height,int size=20,bool title=false){
   var go=GUIManager.Instance.CreateText(text,parent,Center,Center,new Vector2(x,y),title?GUIManager.Instance.NorseBold:GUIManager.Instance.AveriaSerif,size,title?Gold:Color.white,true,Color.black,width,height,false);
   var label=go.GetComponent<Text>();label.alignment=TextAnchor.MiddleLeft;label.supportRichText=false;label.raycastTarget=false;return label;
  }
  Button Button(string text,Transform parent,float x,float y,float width,float height,Action click){
   var go=GUIManager.Instance.CreateButton(text,parent,Center,Center,new Vector2(x,y),width,height);var button=go.GetComponent<Button>();button.onClick.AddListener(()=>click());return button;
  }
  static Image Icon(Transform parent,float x,float y,float size){
   var go=new GameObject("ItemIcon",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=Center;rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(size,size);
   var image=go.GetComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;return image;
  }
  void Create(){
   var ui=GUIManager.Instance;panel=ui.CreateWoodpanel(GUIManager.CustomGUIFront.transform,Center,Center,Vector2.zero,1040,680,false);panel.name="RSN_NetworkTerminal";
   heading=Label(T("terminal_title"),panel.transform,0,285,850,55,34,true);heading.alignment=TextAnchor.MiddleCenter;
   network=Label("",panel.transform,0,243,900,30,20);network.alignment=TextAnchor.MiddleCenter;
   Button("×",panel.transform,470,292,42,42,Close);
   search=ui.CreateInputField(panel.transform,Center,Center,new Vector2(-205,189),InputField.ContentType.Standard,T("terminal_search"),20,550,42).GetComponent<InputField>();search.characterLimit=80;
   search.onValueChanged.AddListener(value=>{query=value;Filter(true);});
   Label(T("terminal_resource"),panel.transform,-210,146,540,28,18).color=Muted;
   var countHeader=Label(T("terminal_in_network"),panel.transform,-210,146,540,28,18);countHeader.alignment=TextAnchor.MiddleRight;countHeader.color=Muted;
   var view=ui.CreateScrollView(panel.transform,false,true,10,2,ColorBlock.defaultColorBlock,new Color(0,0,0,.12f),568,388);
   var rect=view.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Center;rect.anchoredPosition=new Vector2(-202,-65);rect.sizeDelta=new Vector2(568,388);
   scroll=view.GetComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
   content=scroll.content;
   // Own the content geometry; retain the game's styled viewport and scrollbar.
   foreach(var layout in content.GetComponents<LayoutGroup>())Destroy(layout);
   foreach(var fitter in content.GetComponents<ContentSizeFitter>())Destroy(fitter);
   content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
   for(int i=0;i<10;i++){
    var row=new Row();row.Button=Button("",content,0,0,540,48,()=>Select(row.Entry));row.Object=row.Button.gameObject;row.Rect=row.Object.GetComponent<RectTransform>();row.Rect.anchorMin=row.Rect.anchorMax=new Vector2(.5f,1);
    row.Icon=Icon(row.Object.transform,-234,0,40);row.Name=Label("",row.Object.transform,-15,0,350,42,20);row.Count=Label("",row.Object.transform,210,0,95,42,20);row.Count.alignment=TextAnchor.MiddleRight;rows.Add(row);
   }
   empty=Label("",panel.transform,-205,-60,510,100,22);empty.alignment=TextAnchor.MiddleCenter;empty.color=Muted;
   selectedIcon=Icon(panel.transform,290,115,112);selectedIcon.enabled=false;
   detail=Label(T("terminal_select"),panel.transform,290,25,340,65,28,true);detail.alignment=TextAnchor.MiddleCenter;
   qualityLabel=Label("",panel.transform,290,-20,340,26,18);qualityLabel.alignment=TextAnchor.MiddleCenter;
   available=Label("",panel.transform,290,-56,340,28,20);available.alignment=TextAnchor.MiddleCenter;
   carried=Label("",panel.transform,290,-86,340,28,20);carried.alignment=TextAnchor.MiddleCenter;
   var amountTitle=Label(T("terminal_quantity"),panel.transform,290,-130,340,28,20);amountTitle.alignment=TextAnchor.MiddleCenter;
   quantity=ui.CreateInputField(panel.transform,Center,Center,new Vector2(290,-169),InputField.ContentType.IntegerNumber,"1",22,160,42).GetComponent<InputField>();quantity.characterLimit=5;quantity.text="1";
   Button("−",panel.transform,178,-169,48,42,()=>ChangeAmount(-1));Button("+",panel.transform,402,-169,48,42,()=>ChangeAmount(1));
   Button("1",panel.transform,209,-215,66,34,()=>SetAmount(1));Button("10",panel.transform,283,-215,66,34,()=>SetAmount(10));Button(T("terminal_stack"),panel.transform,375,-215,100,34,()=>SetAmount(selected?.Item.m_itemData.m_shared.m_maxStackSize??1));
   take=Button(T("terminal_take",1),panel.transform,290,-270,310,48,Take);
   status=Label("",panel.transform,-5,-310,960,24,17);status.alignment=TextAnchor.MiddleCenter;status.color=Muted;
   query="";nextRefresh=0;dirtyRows=true;
  }
  void Refresh(){
   if(!core||!player)return;
   string name=NetworkName.For(core.GetComponent<NetworkMember>());network.text=name.Length>0?T("terminal_network_name",name):T("terminal_unnamed");
   entries=StorageIndex.Browse(core,player.GetPlayerID()).GroupBy(s=>(s.Item,s.Quality)).Select(g=>{
    var prefab=ZNetScene.instance.GetPrefab(g.Key.Item);var item=prefab?prefab.GetComponent<ItemDrop>():null;
    return item?new Entry{Id=g.Key.Item,Quality=g.Key.Quality,Count=(int)Math.Min(int.MaxValue,g.Sum(s=>(long)s.Amount)),Item=item,Name=Localization.instance.Localize(item.m_itemData.m_shared.m_name)}:null;
   }).Where(e=>e!=null&&e.Count>0).OrderBy(e=>e.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(e=>e.Quality).ToList();
   if(selectedKey!=null){var found=entries.FirstOrDefault(e=>e.Key==selectedKey);if(found!=null)selected=found;else if(selected!=null)selected.Count=0;}
   Filter(false);
  }
  void Filter(bool reset){
   filtered=entries.Where(e=>TerminalRules.Search(e.Name,query)).ToList();content.sizeDelta=new Vector2(0,Mathf.Max(388,filtered.Count*RowHeight));
   if(reset)scroll.verticalNormalizedPosition=1;dirtyRows=true;
   empty.text=filtered.Count==0?T(entries.Count==0?"terminal_empty":"terminal_no_results"):"";
  }
  void RenderRows(){
   int first=Mathf.Clamp((int)(content.anchoredPosition.y/RowHeight),0,Mathf.Max(0,filtered.Count-1));
   if(first==visibleStart&&!dirtyRows)return;visibleStart=first;dirtyRows=false;
   for(int i=0;i<rows.Count;i++){
    var row=rows[i];int index=first+i;row.Object.SetActive(index<filtered.Count);if(index>=filtered.Count)continue;
    row.Entry=filtered[index];row.Rect.anchoredPosition=new Vector2(-6,-index*RowHeight-RowHeight/2);
    row.Icon.sprite=row.Entry.Item.m_itemData.GetIcon();row.Name.text=row.Entry.Name+(row.Entry.Quality>1?" · "+T("terminal_quality",row.Entry.Quality):"");row.Count.text=row.Entry.Count.ToString("N0");
    var colors=row.Button.colors;colors.normalColor=row.Entry.Key==selectedKey?new Color(1,.78f,.42f):Color.white;row.Button.colors=colors;
   }
  }
  void Select(Entry entry){if(entry==null||TerminalTransfer.Busy)return;selected=entry;selectedKey=entry.Key;SetAmount(1);dirtyRows=true;UpdateDetail();}
  void SetAmount(int value){quantity.text=Mathf.Clamp(value,1,Math.Max(1,Math.Min(selected?.Count??1,TerminalRules.MaxAmount))).ToString();}
  void ChangeAmount(int delta){int.TryParse(quantity.text,out int value);SetAmount(value+delta);}
  void UpdateDetail(){
   bool chosen=selected!=null;selectedIcon.enabled=chosen;if(chosen)selectedIcon.sprite=selected.Item.m_itemData.GetIcon();
   detail.text=chosen?selected.Name:T("terminal_select");qualityLabel.text=chosen&&selected.Quality>1?T("terminal_quality",selected.Quality):"";
   available.text=chosen?T("terminal_available",selected.Count.ToString("N0")):"";
   int held=chosen?player.GetInventory().GetAllItems().Where(i=>i.m_dropPrefab&&i.m_dropPrefab.name==selected.Id&&i.m_quality==selected.Quality).Sum(i=>i.m_stack):0;
   carried.text=chosen?T("terminal_carried",held.ToString("N0")):"";
   bool valid=TerminalRules.Quantity(quantity.text,selected?.Count??0,out int amount);take.interactable=chosen&&valid&&!TerminalTransfer.Busy;quantity.interactable=!TerminalTransfer.Busy;search.interactable=!TerminalTransfer.Busy;
   var label=take.GetComponentInChildren<Text>();if(label)label.text=TerminalTransfer.Busy?T("terminal_pending"):T("terminal_take",valid?amount:0);
   if(Time.unscaledTime>=statusUntil)status.text=TerminalTransfer.Busy?T("terminal_pending"):T("terminal_close");
  }
  void Take(){
   if(selected==null||!TerminalRules.Quantity(quantity.text,selected.Count,out int amount)||TerminalTransfer.Busy)return;
   if(!TerminalTransfer.Start(core,player,selected.Id,selected.Quality,amount))TransferStatus("terminal_retry");else TransferStatus("terminal_pending");
   UpdateDetail();
  }
  internal static void TransferStatus(string key,params object[] args){if(!instance||!instance.panel)return;instance.status.text=T(key,args);instance.statusUntil=Time.unscaledTime+5;instance.nextRefresh=0;}
 }
}
