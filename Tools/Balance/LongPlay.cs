using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Web.Script.Serialization;using System.Text;using CrazyRubbish.Core;
// External policy driver: only public player actions. No grants, teleportation, timer skipping or need suppression.
class LongPlay
{
 const long U=PlanStock.Scale;PlanSimulation m;JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=200};
 List<object> samples=new List<object>();List<object> events=new List<object>();Dictionary<string,double> tasks=new Dictionary<string,double>();
 string outDir,phase="starter",activity="startup";double nextSample=0,nextPolicy=0;bool policies,allowBuying=true,managed;int seed;double wagesMissedSeconds;int maxWarehouses=3;Dictionary<string,double> workerTimes=new Dictionary<string,double>();double workerDistance;long purchases;int payCalls;double maxFoodWait,maxWaterWait,firstAffordableRegion2=-1;long implicitPurchases,expansionSpent;Dictionary<string,int> batches=new Dictionary<string,int>();
 void Event(string name){events.Add(new{time=m.State.seconds,name=name});Console.WriteLine(seed+" "+m.State.seconds.ToString("0.0")+" "+name);File.WriteAllText(Path.Combine(outDir,"checkpoint-"+seed+"-"+events.Count.ToString("00")+".json"),json.Serialize(m.Capture()));}
 void Step(double dt)
 {
  long cashBefore=m.State.cash,retailBefore=m.State.retailIncome,serviceBefore=m.State.serviceIncome,supplyBefore=m.State.supplySpent;var active=m.State.sites.Where(site=>site.active).ToDictionary(site=>site.id,site=>Tuple.Create(site.batchId,site.recipe));double before=m.State.seconds;var positions=m.Alive.ToDictionary(p=>p.id,p=>Tuple.Create(p.x,p.y));m.Advance(dt);double elapsed=m.State.seconds-before;implicitPurchases+=cashBefore-m.State.cash+(m.State.retailIncome-retailBefore)+(m.State.serviceIncome-serviceBefore)-(m.State.supplySpent-supplyBefore);foreach(var pair in active){var site=m.Site(pair.Key);if(site!=null&&(!site.active||site.batchId!=pair.Value.Item1)){string recipe=pair.Value.Item2;if(!batches.ContainsKey(recipe))batches[recipe]=0;batches[recipe]++;}}
  foreach(var p in m.Alive){if(positions.ContainsKey(p.id))workerDistance+=Math.Abs(p.x-positions[p.id].Item1)+Math.Abs(p.y-positions[p.id].Item2);string role=p.trip!=null?"transport":p.maintaining>0?"maintenance":p.assigned>0?"production":"idle";if(!workerTimes.ContainsKey(role))workerTimes[role]=0;workerTimes[role]+=elapsed;}
  if(!tasks.ContainsKey(activity))tasks[activity]=0;tasks[activity]+=elapsed;
  if(m.Debt>0)wagesMissedSeconds+=elapsed;foreach(var person in m.Alive.Concat(new[]{m.State.player})){if(person.foodSince>=0)maxFoodWait=Math.Max(maxFoodWait,m.State.seconds-person.foodSince);if(person.waterSince>=0)maxWaterWait=Math.Max(maxWaterWait,m.State.seconds-person.waterSince);}if(firstAffordableRegion2<0&&m.State.cash>=20000)firstAffordableRegion2=m.State.seconds;
  if(policies&&m.State.seconds>=nextPolicy){nextPolicy=m.State.seconds+1;Policy();}
  if(m.State.seconds>=nextSample){nextSample=m.State.seconds+300;Sample();}
  if(!string.IsNullOrEmpty(m.State.ending))throw new Exception("ENDING: "+m.State.ending);
 }
 void Sample()
 {
  var data=new{time=m.State.seconds,phase=phase,cash=m.State.cash/100.0,workers=m.Alive.Count(),debt=m.Debt/100.0,wages=m.State.wagesPaid/100.0,sales=m.State.externalSaleIncome/100.0,retail=m.State.retailIncome/100.0,services=m.State.serviceIncome/100.0,purchases=m.State.supplySpent/100.0,food=m.Stored("food")/(double)U,water=m.Stored("water")/(double)U,boards=m.Stored("boards")/(double)U,warehouses=m.Warehouses.Count(),stored=m.Warehouses.Sum(w=>w.Used(m.Config)),storageCapacity=m.Warehouses.Sum(w=>w.capacity),ground=m.State.ground.Sum(g=>g.stock.Used(m.Config)),groundCells=m.State.ground.Sum(g=>g.width),pressure=m.Pressure(9,7),happiness=m.Alive.Select(p=>p.happiness).DefaultIfEmpty(70).Min(),hungry=m.Alive.Concat(new[]{m.State.player}).Count(p=>p.foodDebt>0),thirsty=m.Alive.Concat(new[]{m.State.player}).Count(p=>p.waterDebt>0),sites=m.State.sites.Select(s=>new{id=s.id,type=s.type,recipe=s.recipe,active=s.active,progress=s.progress,status=s.status,output=s.output.Used(m.Config)}).ToArray(),people=m.Alive.Select(p=>new{id=p.id,task=m.WorkerTask(p),x=p.x,y=p.y}).ToArray()};samples.Add(data);
  File.WriteAllText(Path.Combine(outDir,"samples-"+seed+".json"),json.Serialize(samples));Console.WriteLine("sample "+seed+" t="+m.State.seconds.ToString("0")+" cash="+(m.State.cash/100.0).ToString("0.00")+" food="+m.Stored("food")/(double)U+" water="+m.Stored("water")/(double)U+" ground="+m.State.ground.Sum(g=>g.stock.Used(m.Config)).ToString("0"));
 }
 void Policy()
 {
  // Normal explicit selling and payroll commands; retain building stock and food/water protection.
  long sell=m.Surplus("boards")-(managed?6:12)*U;if(sell>0)m.Sell("boards",Math.Min(sell,3*U));
  if(m.Debt>0&&m.State.cash>0){m.PayWages();payCalls++;}
  if(managed)foreach(string id in new[]{"seed_tuber","substrate","filter_media"})if(m.State.acquired.Contains(id)&&m.Stored(id)<U/2&&m.State.cash>m.Alive.Count()*1000+200){long before=m.State.cash;try{m.Buy(id,U);purchases+=before-m.State.cash;}catch(InvalidOperationException){}}
 }
 void Wait(double seconds){double until=m.State.seconds+seconds;while(m.State.seconds+.0001<until)Step(Math.Min(.5,until-m.State.seconds));}
 void Go(int x,int y)
 {
  string old=activity;activity="hero_walk";double deadline=m.State.seconds+600;
  while(Math.Abs(m.State.player.x-x)+Math.Abs(m.State.player.y-y)>.05){if(!m.Walk(m.State.player,x,y,.1))throw new Exception("No path "+x+","+y);Step(.1);if(m.State.seconds>deadline)throw new Exception("Walking timed out");}activity=old;
 }
 void Store()
 {
  var w=m.State.sites.Where(s=>s.type=="warehouse"&&!s.pending&&m.State.backpack.stacks.All(a=>s.input.Fits(m.Config,a.item,a.ticks))).OrderBy(s=>Math.Abs(m.Front(s).Item1-m.State.player.x)+Math.Abs(m.Front(s).Item2-m.State.player.y)).FirstOrDefault();
  if(w==null)throw new Exception("No warehouse space for backpack");var f=m.Front(w);Go(f.Item1,f.Item2);m.Deposit(w);
 }
 bool CollectOne(string item)
 {
  var p=m.State.player;var pile=m.State.ground.Where(g=>g.stock.Count(item)>0).OrderBy(g=>Math.Abs(g.x-p.x)+Math.Abs(g.y-p.y)).FirstOrDefault();if(pile==null)return false;
  if(!m.State.backpack.Fits(m.Config,item,U))Store();Go(pile.x,pile.y);if(pile.stock.Count(item)<=0)return true;
  long amount=Math.Min(U,pile.stock.Count(item));m.Pick(pile.id,item,amount);Wait(.5);return true;
 }
 void Collect(string item,int amount){for(int n=0;n<amount;n++)if(!CollectOne(item))throw new Exception("No ground item "+item);}
 bool Ensure(string item,long desired,double timeout)
 {
  double end=m.State.seconds+timeout;
  while(m.Stored(item)+m.State.backpack.Count(item)<desired)
  {
   if(CollectOne(item))continue;
   var output=m.State.sites.FirstOrDefault(s=>s.type!="warehouse"&&s.output.Count(item)>0);
   if(output!=null){if(!m.State.backpack.Fits(m.Config,item,U))Store();var f=m.Front(output);Go(f.Item1,f.Item2);if(output.output.Count(item)>0)m.TakeOutput(output,item,Math.Min(U,output.output.Count(item)));continue;}
   long deficit=desired-m.Stored(item)-m.State.backpack.Count(item);var d=m.Config.Item(item);
   if(allowBuying&&item!="boards"&&d.canBuy&&m.State.cash-m.Debt>=Math.Ceiling(d.buy*deficit/U*100)){try{long before=m.State.cash;m.Buy(item,deficit);purchases+=before-m.State.cash;continue;}catch(InvalidOperationException){}}
   if(m.State.seconds>end)return false;Wait(2);
  }return true;
 }
 PlanSite Build(string type,int cx,int cy)
 {
  if(m.State.heroSite!=0)m.AssignHero(m.State.heroSite);
  AcquireTech(m.Config.Facility(type).technology);
  foreach(var a in m.Config.Facility(type).build)if(!Ensure(a.item,a.Ticks,900))throw new Exception("Build blocked "+type+" material "+a.item);
  var spots=new List<Tuple<int,int>>();for(int y=5;y<=26;y++)for(int x=7;x<=30;x++)spots.Add(Tuple.Create(x,y));
  foreach(var at in spots.OrderBy(t=>(t.Item1-cx)*(t.Item1-cx)+(t.Item2-cy)*(t.Item2-cy)))
   if(m.BuildProblem(type,at.Item1,at.Item2)==null){var s=m.Build(type,at.Item1,at.Item2);Event("build "+type+" at "+s.x+","+s.y);return s;}
  throw new Exception("No build location "+type);
 }
 void AcquireTech(string id)
 {
  if(m.State.technologies.Contains(id))return;var tech=m.Config.technologies.First(t=>t.id==id);if(tech.region_required>m.State.region)throw new Exception("Tech region locked "+id);
  foreach(string parent in tech.parents)AcquireTech(parent);
  foreach(string item in tech.acquired_all)if(!m.State.acquired.Contains(item)&&!Ensure(item,U,1200))throw new Exception("Tech material "+item);
  if(tech.acquired_any.Length>0&&!tech.acquired_any.Any(m.State.acquired.Contains)){string item=tech.acquired_any.OrderBy(i=>m.State.ground.Any(g=>g.stock.Count(i)>0)?0:1).ThenBy(i=>m.Config.Item(i).buy).First();if(!Ensure(item,U,1200))throw new Exception("Tech any "+item);}
  if(!m.State.technologies.Contains(id))throw new Exception("Tech not acquired "+id);
 }
 void Connect(PlanSite site)
 {
  var f=m.Front(site);int x=12,y=6;
  while(x!=f.Item1||y!=f.Item2){if(x!=f.Item1)x+=Math.Sign(f.Item1-x);else y+=Math.Sign(f.Item2-y);if(m.State.sites.Any(s=>s.type=="power_line"&&s.x==x&&s.y==y))continue;if(!Ensure("wire",PlanStock.Quantity(.25),600))throw new Exception("Need wire");m.Build("power_line",x,y);}
 }
 void WorkOne(PlanSite site,string recipe,string input)
 {
  if(!Ensure(input,U,1200))throw new Exception("Need dismantling input "+input);CollectIfStoredToBag(site,input);
  m.SetSiteEnabled(site,true);m.SelectRecipe(site,recipe);var f=m.Front(site);Go(f.Item1,f.Item2);m.Deposit(site);m.AssignHero(site.id);int batch=site.batchId;bool started=false;double deadline=m.State.seconds+300;
  while(true){Wait(.5);if(site.active)started=true;if(started&&!site.active)break;if(m.State.seconds>deadline)throw new Exception("Dismantling stuck "+site.status);}
  m.AssignHero(site.id);m.SetSiteEnabled(site,false);foreach(var item in site.output.stacks.Select(a=>a.item).Distinct().ToArray())while(site.output.Count(item)>0){if(!m.State.backpack.Fits(m.Config,item,U))Store();f=m.Front(site);Go(f.Item1,f.Item2);m.TakeOutput(site,item,Math.Min(U,site.output.Count(item)));}Store();
 }
 void CollectIfStoredToBag(PlanSite site,string item)
 {
  if(m.State.backpack.Count(item)>0)return;var wh=m.State.sites.FirstOrDefault(s=>s.type=="warehouse"&&s.input.Count(item)>0);if(wh==null)return;var f=m.Front(wh);Go(f.Item1,f.Item2);m.TakeOutput(wh,item,U);
 }
 void Upgrade(bool includeLife)
 {
  phase="semi automation";managed=true;
  var dis=Build("disassembly",8,12);Ensure("screwdriver_wood",U,600);Ensure("wrench",U,600);CollectIfStoredToBag(dis,"screwdriver_wood");CollectIfStoredToBag(dis,"wrench");var f=m.Front(dis);Go(f.Item1,f.Item2);m.Deposit(dis);WorkOne(dis,"open_old_cutter","old_cutter");
  m.Recruit();Event("third hire for operator, maintenance and transport");var electric=Build("electric_wood",12,8);Connect(electric);m.SelectRecipe(electric,"electric_boards");m.SetSiteEnabled(m.State.sites.First(s=>s.type=="wood"),false);Event("semi automatic wood enabled");
  if(!includeLife){m.State.autoSupply=true;m.State.autoTools=true;phase="semi economy";return;}
  phase="life production";
  WorkOne(dis,"open_old_grow_light","old_grow_light");
  if(!Ensure("filter_module",U,1800)||!Ensure("filter_media",U,1800)||!Ensure("seed_tuber",U,1800)||!Ensure("substrate",U,1800))throw new Exception("Cannot fund life supplies");
  var intake=Build("water_intake",22,20);m.SelectRecipe(intake,"collect_freshwater");var purifier=Build("water_purifier",18,16);m.SelectRecipe(purifier,"make_drinking_water");
  var grow=Build("growing",18,11);m.SelectRecipe(grow,"grow_tuber");var lamp=Build("grow_lamp",20,11);lamp.lampBed=grow.id;Connect(lamp);var kitchen=Build("kitchen",16,14);m.SelectRecipe(kitchen,"cook_tubers");
  Event("water and crop chain built");phase="managed economy";m.State.autoSupply=true;m.State.autoTools=true;
 }
 void Starter()
 {
  m.SetPaused(false);activity="starter_collect";Collect("food",6);Store();Collect("water",8);Store();Collect("handsaw_wood",1);Collect("waste_wood",4);Collect("fasteners",2);
  var wood=m.Build("wood",8,8);m.SelectRecipe(wood,"wood_boards");Collect("waste_wood",4);var f=m.Front(wood);Go(f.Item1,f.Item2);m.Deposit(wood);m.AssignHero(wood.id);Wait(50);m.AssignHero(wood.id);
  for(int i=0;i<3;i++)m.TakeOutput(wood,"boards",U);Store();foreach(var a in m.Config.Facility("generator").build){Collect(a.item,(int)a.quantity);Store();}
  var generator=m.Build("generator",12,6);Collect("waste_wood",2);f=m.Front(generator);Go(f.Item1,f.Item2);m.Deposit(generator);Collect("wire",2);Store();for(int x=12;x<=17;x++)m.Build("power_line",x,6);Wait(.2);
  m.Recruit();m.Recruit();Event("recognized, two hires, cash "+m.State.cash);policies=true;
 }
 void Run(string configFile,string output,int seedValue,string mode)
 {
  outDir=output;seed=seedValue;Directory.CreateDirectory(outDir);var config=json.Deserialize<PlanConfig>(File.ReadAllText(configFile));config.Validate();m=new PlanSimulation(config,seed);
  try
  {
   Starter();phase="wood economy";activity="manage";
   // Initial raw stock is gathered by real employees, hero makes room by building storage.
   Build("warehouse",13,10);Event("second warehouse");if(mode.StartsWith("managed")||mode.StartsWith("semi")){Upgrade(!mode.StartsWith("semi"));if(mode.StartsWith("managed-four")){m.Recruit();Event("fourth hire to share transport and life production");}}
   if(mode=="supplies")m.State.autoSupply=true; // Existing player-facing policy toggle, budget stays at its real default.
   double end=7200;
   while(m.State.seconds<end||(m.State.region==1&&m.State.seconds<10800))
   {
    activity="manage";Wait(5);
    if(m.Warehouses.Sum(w=>w.Used(m.Config))/m.Warehouses.Sum(w=>w.capacity)>.8&&m.Warehouses.Count()<maxWarehouses){Build("warehouse",18,12);}
    if(m.State.region==1&&m.State.cash>=20000+m.Alive.Count()*1000){m.BuyRegion(2);expansionSpent+=PlanSimulation.Cents(m.Config.Region(2).purchase_cash);Event("region 2 purchased with payroll reserve");phase="region 2";end=Math.Max(end,m.State.seconds+600);}
   }
   Event("completed "+(m.State.seconds/60).ToString("0.0")+" minutes");
  }
  catch(Exception e){Event("BLOCKED: "+e.Message);}
  Sample();File.WriteAllText(Path.Combine(outDir,"state-"+seed+".json"),json.Serialize(m.Capture()));File.WriteAllText(Path.Combine(outDir,"report-"+seed+".json"),json.Serialize(new{coreSha256=BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(typeof(PlanSimulation).Assembly.Location))).Replace("-","").ToLowerInvariant(),configSha256=BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(configFile))).Replace("-","").ToLowerInvariant(),seed=seed,mode=mode,boardReserve=managed?6:12,manualConstructionKeepsFuturePayroll=false,maxFoodWait=maxFoodWait,maxWaterWait=maxWaterWait,firstAffordableRegion2=firstAffordableRegion2,elapsed=m.State.seconds,region=m.State.region,ending=m.State.ending,events=events,heroTime=tasks,workerTime=workerTimes,workerDistance=workerDistance,purchases=purchases/100.0,payCalls=payCalls,wagesMissedSeconds=wagesMissedSeconds,finalCash=m.State.cash/100.0,completedBatches=batches,implicitPurchases=implicitPurchases/100.0,expansionSpent=expansionSpent/100.0,cashLedgerDifference=(m.State.externalSaleIncome+m.State.retailIncome+m.State.serviceIncome-m.State.wagesPaid-purchases-implicitPurchases-expansionSpent-m.State.supplySpent-m.State.cash)/100.0}));
 }
 static void Main(string[] args){Console.OutputEncoding=Encoding.UTF8;new LongPlay().Run(args[0],args[1],int.Parse(args[2]),args.Length>3?args[3]:"baseline");}
}
