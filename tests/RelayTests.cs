using System;
using System.Collections.Generic;
using System.Linq;
using RunicStorageNetwork.Logic;

static class RelayTests {
 static int count;
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 static void Test(string title,Action body){body();count++;Console.WriteLine("PASS relay "+title);}
 static NetworkNode N(string id,double x,string net="A",bool root=false,double y=0,double z=0)=>new NetworkNode{Id=id,Network=net,Root=root,Confirmed=true,Position=new Point(x,y,z),Storage=20,Supply=20};
 static NetworkGraph G(params NetworkNode[] nodes)=>new NetworkGraph(nodes,50);
 static bool Allow(NetworkNode n)=>true;
 static KeyValuePair<string,Point> Chest(string id,double x)=>new KeyValuePair<string,Point>(id,new Point(x,0,0));
 sealed class Owner {
  public int Items=1;public string Held;public bool Paid;readonly HashSet<string> ended=new HashSet<string>();
  public bool Prepare(string op){if(ended.Contains(op)||Held!=null)return false;Held=op;return true;}
  public bool Commit(string op,Func<bool> validate){if(Held!=op)return false;if(Paid)return true;if(!validate()||Items<1)return false;Items--;Paid=true;return true;}
  public void Finish(string op,bool success){ended.Add(op);if(Held!=op)return;if(!success&&Paid)Items++;Held=null;Paid=false;}
 }
 public static int Run(){
  Test("core retains configured radii",()=>{var c=N("c",0,root:true);c.Storage=37;c.Supply=11;var g=G(c);Check(g.Covers("A",new Point(36,0,0),Allow)&&g.Choose(new Point(12,0,0),Allow)==null,"defaults overwrote core config");});
  Test("bidirectional chain and empty transit",()=>{var g=G(N("c",0,root:true),N("r1",50),N("r2",100));Check(g.Hops["r2"]==2&&g.Choose(new Point(110,0,0),Allow)=="A","relay coverage");Check(g.Pool("A",new[]{Chest("warehouse",-10),Chest("remote",110)},Allow).Count()==2,"network union");});
  Test("unbound relay cannot supply",()=>{var g=G(N("c",0,root:true),N("r",50,""));Check(!g.Hops.ContainsKey("r")&&g.Choose(new Point(65,0,0),Allow)==null,"ghost supply");});
  Test("50 inclusive boundary",()=>Check(G(N("c",0,root:true),N("r",50)).Hops.ContainsKey("r"),"boundary"));
  Test("epsilon outside and no summed radii",()=>{Check(!G(N("c",0,root:true),N("r",50.01)).Hops.ContainsKey("r"),"epsilon");Check(!G(N("c",0,root:true),N("r",99)).Hops.ContainsKey("r"),"summed radii");});
  Test("height affects range",()=>{Check(!G(N("c",0,root:true),N("r",40,y:31)).Hops.ContainsKey("r"),"2D shortcut");Check(G(N("c",0,root:true),N("r",30,y:40)).Hops["r"]==1,"3D boundary");});
  Test("cycles and deterministic shortest route",()=>{var nodes=new[]{N("c",0,root:true),N("a",40,z:20),N("b",40,z:-20),N("r",80)};var a=G(nodes);var b=G(nodes.Reverse().ToArray());Check(a.Hops["r"]==2&&a.Parent["r"]==b.Parent["r"]&&a.Parent["r"]=="a","unstable route");});
  Test("bridge removal and restoration",()=>{var c=N("c",0,root:true);var a=N("a",50);var b=N("b",100);Check(G(c,a,b).Hops.ContainsKey("b")&&!G(c,b).Hops.ContainsKey("b")&&G(c,a,b).Hops.ContainsKey("b"),"bridge");});
  Test("ring without root inactive",()=>Check(G(N("a",0),N("b",20),N("c",40)).Hops.Count==0,"self powering ring"));
  Test("alternate route survives",()=>{var c=N("c",0,root:true);var b=N("b",40,z:-20);var r=N("r",80);Check(G(c,b,r).Hops["r"]==2,"alternate");});
  Test("candidate networks deduplicated",()=>{var g=G(N("c",0,root:true),N("a",10),N("b",20));Check(g.Candidates(new Point(30,0,0),Allow).Count==1,"counting nodes instead of networks");});
  Test("ambiguity sticky until explicit choice",()=>{bool choice;string net=NetworkGraph.AutoBinding("",false,true,new[]{"A","B"},out choice);Check(net==""&&choice,"arbitrary autobind");Check(NetworkGraph.AutoBinding(net,choice,true,new[]{"A"},out choice)==""&&choice,"silent later choice");});
  Test("single complete candidate autobinds",()=>{bool choice;Check(NetworkGraph.AutoBinding("",false,true,new[]{"A","A"},out choice)=="A"&&!choice,"unique candidate");});
  Test("partial discovery cannot autobind",()=>{bool choice;Check(NetworkGraph.AutoBinding("",false,false,new[]{"A"},out choice)==""&&!choice,"partial uniqueness");});
  Test("binding survives loss and foreign core",()=>{bool choice;Check(NetworkGraph.AutoBinding("A",false,true,new[]{"B"},out choice)=="A","silent rebind");Check(G(N("foreign",0,"B",true),N("r",10)).Hops.Count==1,"foreign supplies A");});
  Test("unconfirmed intermediary not a path",()=>{var r=N("r",50);r.Confirmed=false;Check(!G(N("c",0,root:true),r,N("far",100)).Hops.ContainsKey("far"),"stale path");r.Confirmed=true;Check(G(N("c",0,root:true),r,N("far",100)).Hops.ContainsKey("far"),"load recovery");});
  Test("rebind only selected node",()=>{var a=N("a",50);var b=N("b",100);a.Network="B";var g=G(N("c",0,root:true),N("other",40,"B",true),a,b);Check(g.Hops.ContainsKey("a")&&!g.Hops.ContainsKey("b")&&b.Network=="A","subtree rebound");});
  Test("shared chest deduplicated",()=>{var g=G(N("c",0,root:true),N("r",20));Check(g.Pool("A",new[]{Chest("one",10),Chest("one",10)},Allow).Count()==1,"double inventory");});
  Test("choose nearest supplier not distant root",()=>{var g=G(N("a",0,root:true),N("r",50),N("b",70,"B",true));Check(g.Choose(new Point(52,0,0),Allow)=="A","nearest core chosen");});
  Test("network tie stable",()=>{var g=G(N("a",-10,root:true),N("b",10,"B",true));Check(g.Choose(new Point(),Allow)=="A","tie");});
  Test("UI and payment same selection",()=>{var g=G(N("a",0,root:true),N("r",50),N("b",70,"B",true));Func<NetworkNode,bool> access=n=>n.Id!="r";var point=new Point(52,0,0);string ui=g.Choose(point,access),pay=g.Choose(point,access);Check(ui=="B"&&pay==ui,"different policy");});
  Test("access filtered from pool",()=>{var g=G(N("c",0,root:true),N("r",50));Check(!g.Pool("A",new[]{Chest("denied",55)},n=>n.Id!="r").Any(),"ward bypass");});
  Test("single network plan never merges foreign stock",()=>{var g=G(N("c",0,root:true),N("b",30,"B",true));string net=g.Choose(new Point(5,0,0),Allow);var stock=g.Pool(net,new[]{Chest("own",-10),Chest("foreign",45)},Allow).Select(id=>new Stock(id,"Wood",1,3));Check(Planner.Plan(new[]{new Need("Wood",5)},stock)==null,"merged networks");});
  Test("two relay operations share physical lease",()=>{var o=new Owner();Check(o.Prepare("r1")&&!o.Prepare("r2"),"parallel reserve");Check(o.Commit("r1",()=>true)&&o.Commit("r1",()=>true)&&o.Items==0,"double debit");o.Finish("r1",true);Check(!o.Prepare("r1")&&o.Prepare("r2")&&!o.Commit("r2",()=>true),"replay or duplicate supply");});
  Test("path breaks before debit",()=>{var o=new Owner();o.Prepare("x");var graph=G(N("c",0,root:true),N("r2",100));Check(!o.Commit("x",()=>graph.Hops.ContainsKey("r2"))&&o.Items==1,"stale plan");o.Finish("x",false);});
  Test("known break after debit compensates own delta once",()=>{var o=new Owner();o.Prepare("x");o.Commit("x",()=>true);o.Finish("x",false);o.Finish("x",false);Check(o.Items==1,"double rollback");});
  Test("completed action not rolled back on later outage",()=>{var o=new Owner();o.Prepare("x");o.Commit("x",()=>true);o.Finish("x",true);o.Finish("x",false);Check(o.Items==0,"post-completion refund");});
  Test("ghost excluded from coverage and own financing",()=>{var ghost=N("ghost",50);ghost.Confirmed=false;var g=G(N("c",0,root:true),ghost);Check(g.Choose(new Point(55,0,0),Allow)==null&&!g.Pool("A",new[]{Chest("far",55)},Allow).Any(),"ghost supply");});
  Test("root identity idempotent across owners and order",()=>{Check(NetworkGraph.RootIdentity("world-instance-1")==NetworkGraph.RootIdentity("world-instance-1"),"migration changed ID");Check(NetworkGraph.RootIdentity("world-instance-1")!=NetworkGraph.RootIdentity("world-instance-2"),"rebuilt core inherited ID");});
  Test("duplicate registration cannot duplicate network",()=>{bool refused=false;try{G(N("c",0,root:true),N("c",0,root:true));}catch(InvalidOperationException){refused=true;}Check(refused,"duplicate registration accepted");});
  Test("conflicting persisted roots fail closed",()=>Check(G(N("c",0,root:true),N("other",10,root:true)).Hops.Count==0,"two roots merged"));
  Test("no hop cap",()=>{var nodes=Enumerable.Range(0,250).Select(i=>N(i.ToString("D4"),i*50,root:i==0)).ToArray();Check(G(nodes).Hops["0249"]==249,"hop cap");});
  Test("source branch loss while entry remains covered",()=>{var c=N("c",0,root:true);var r=N("r",50);var far=N("far",100);var before=G(c,r,far);var after=G(c,far);Check(before.Choose(new Point(),Allow)=="A"&&after.Choose(new Point(),Allow)=="A","entry");Check(before.Covers("A",new Point(110,0,0),Allow)&&!after.Covers("A",new Point(110,0,0),Allow),"source path not revalidated");});
  Test("alternate same network route preserves source coverage",()=>{var g=G(N("c",0,root:true),N("b",40,z:-20),N("far",80));Check(g.Covers("A",new Point(95,0,0),Allow),"valid replacement route refused");});
  Test("abort delivered before delayed prepare",()=>{var owner=new Owner();owner.Finish("late",false);Check(!owner.Prepare("late")&&owner.Items==1&&owner.Held==null,"late prepare leaked reservation");Check(owner.Prepare("next"),"unrelated request blocked");});
  return count;
 }
}
