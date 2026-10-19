using System;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private InputField buildSearchField;
        private RectTransform buildResults;
        private string buildSearch="";
        private string buildCatalogKey;
        private float buildResultsY;
        private bool showLockedBuildings;
        private bool EditingText=>EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.activeInHierarchy&&EventSystem.current.currentSelectedGameObject.TryGetComponent<InputField>(out var field)&&field.isFocused;
        private void BuildSearchBox()
        {
            var root=Rect(panelContent,"Building search",5,rowY,302,36);root.gameObject.AddComponent<Image>().color=new Color32(18,31,34,255);
            var input=root.gameObject.AddComponent<InputField>();buildSearchField=input;input.targetGraphic=root.GetComponent<Image>();input.navigation=new Navigation{mode=Navigation.Mode.None};
            var label=Text(root,"Text","",8,5,286,28,16);label.supportRichText=false;label.verticalOverflow=VerticalWrapMode.Truncate;
            var placeholder=Text(root,"Placeholder","搜索设施或建材名称",8,5,286,28,15);placeholder.color=new Color32(157,169,164,255);
            input.textComponent=label;input.placeholder=placeholder;input.characterLimit=40;input.text=buildSearch;
            input.onValueChanged.AddListener(value=>{buildSearch=value;RefreshBuildResults();});
            Button(panelContent,"清空",314,rowY,60,36,()=>{input.text="";EventSystem.current?.SetSelectedGameObject(null);});rowY+=44;
            Row(showLockedBuildings?"设施范围：全部（含未解锁）":"设施范围：仅已解锁",()=>{showLockedBuildings=!showLockedBuildings;RebuildPanel();});
        }
        private PlanFacility[] MatchingBuildings()
        {
            string query=(buildSearch??"").Trim();
            return Model.Config.facilities.Where(d=>showLockedBuildings||d.region_required<=Model.State.region&&Model.State.technologies.Contains(d.technology))
                .Where(d=>query.Length==0||d.name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||d.id.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||d.build.Any(a=>Model.Config.Item(a.item).name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
        }
        private string BuildCatalogKey=>Model.State.region+":"+string.Join(",",Model.State.technologies)+":"+showLockedBuildings+":"+buildSearch;
        private void RefreshCatalogList()
        {if(buildResults!=null&&managementOpen&&panelTab==1&&buildCatalogKey!=BuildCatalogKey)RefreshBuildResults(true);}
        private void RefreshBuildResults(bool preservePosition=false)
        {
            if(buildResults==null||!managementOpen||panelTab!=1)return;
            for(int i=buildResults.childCount-1;i>=0;i--)Remove(buildResults.GetChild(i).gameObject);
            var parent=panelContent;var position=parent.anchoredPosition;
            try
            {
                panelContent=buildResults;rowY=0;var matches=MatchingBuildings();Line("找到 "+matches.Length+" 种设施",16,30);
                foreach(var d in matches)
                {
                    var def=d;bool unlocked=d.region_required<=Model.State.region&&Model.State.technologies.Contains(d.technology);
                    if(unlocked)Row(d.name+" "+d.size[0]+"×"+d.size[1],()=>{buildType=def.id;CloseContext();HideManagement();Notice="点击地图放置 "+def.name+"；R旋转，右键取消。";});
                    else Row("未解锁："+d.name,()=>OpenFacilityRequirements(def));
                    var cost=Text(panelContent,"Build cost",Amounts(d.build),5,rowY,374,10,13);float height=Mathf.Max(34,cost.preferredHeight+6);cost.rectTransform.sizeDelta=new Vector2(374,height);rowY+=height+5;
                    if(unlocked)Row("查看缺料 / 采购建材",()=>OpenBuildPurchase(def));
                    if(PlanSimulation.IsWaterIntake(d.id))Line("放在已开放岸边；采出的原水需要净化。",14,50);
                }
                if(matches.Length==0)Line("没有匹配设施。可清空搜索或切换显示未解锁设施。",16,70);
                buildResults.sizeDelta=new Vector2(384,rowY);parent.sizeDelta=new Vector2(384,Mathf.Max(614,buildResultsY+rowY+20));
                parent.anchoredPosition=preservePosition?position:Vector2.zero;buildCatalogKey=BuildCatalogKey;
            }
            finally{panelContent=parent;rowY=buildResultsY+buildResults.sizeDelta.y;}
        }
        private void OpenBuildPurchase(PlanFacility facility)
        {
            var quote=Model.QuoteMissingBuildMaterials(facility.id);BeginContext("建设采购 · "+facility.name,CatalogPoint);
            if(quote.missing.Length==0)SourceLine("当前背包与仓库的未预约建材已经齐全。");
            else
            {
                SourceLine("缺少："+Amounts(quote.missing));SourceLine("合计 "+(quote.cents/100m).ToString("0.00")+" 钱 · 当前现金 "+(Model.State.cash/100m).ToString("0.00")+" 钱");
                SourceLine("仅采购缺少的材料，放入有实际容量的仓库；不会自动建造。手动采购沿用现有价格与现金规则。");
                if(quote.problem!=null)SourceLine(quote.problem);else ContextAction("确认采购缺料",()=>Act(()=>Model.BuyMissingBuildMaterials(facility.id)));
            }
            EndContext();
        }
        private void SourceLine(string value)
        {
            var text=Text(contextBody,"Catalog information",value,4,contextY,302,10,15);float height=Mathf.Max(28,ReadoutHeight(text));text.rectTransform.sizeDelta=new Vector2(302,height);contextY+=height+6;
        }
        private string TechName(string id)=>Model.Config.technologies.FirstOrDefault(t=>t.id==id)?.name??id;
        private string TechnologyConditions(string id)
        {
            var tech=Model.Config.technologies.FirstOrDefault(t=>t.id==id);if(tech==null)return "未登记科技";
            if(Model.State.technologies.Contains(id))return "已解锁："+tech.name;
            string text="科技："+tech.name;
            if(tech.region_required>Model.State.region)text+="\n需开放第"+tech.region_required+"区";
            var parents=tech.parents.Where(p=>!Model.State.technologies.Contains(p)).ToArray();if(parents.Length>0)text+="\n前置科技："+string.Join("、",parents.Select(TechName));
            var all=tech.acquired_all.Where(i=>!Model.State.acquired.Contains(i)).ToArray();if(all.Length>0)text+="\n需实际取得："+string.Join("、",all.Select(i=>Model.Config.Item(i).name));
            if(tech.acquired_any.Length>0&&!tech.acquired_any.Any(Model.State.acquired.Contains))text+="\n再取得任一种："+string.Join("、",tech.acquired_any.Select(i=>Model.Config.Item(i).name));
            return text;
        }
        private Vector2 CatalogPoint=>new Vector2(Screen.width*.5f,Screen.height*.8f);
        private void OpenTechnology(string id)
        {
            BeginContext(TechName(id),CatalogPoint);SourceLine(TechnologyConditions(id));var tech=Model.Config.technologies.First(t=>t.id==id);
            foreach(string parent in tech.parents){string key=parent;ContextAction("前置："+TechName(key),()=>OpenTechnology(key));}
            foreach(string item in tech.acquired_all.Concat(tech.acquired_any).Distinct()){string key=item;ContextAction("材料："+Model.Config.Item(key).name,()=>OpenItemSources(key));}
            EndContext();
        }
        private void OpenFacilityRequirements(PlanFacility facility)
        {
            BeginContext(facility.name+" · 建造条件",CatalogPoint);SourceLine("占地 "+facility.size[0]+"×"+facility.size[1]+"；建设人手 "+facility.builders+"\n建材："+Amounts(facility.build));
            if(facility.region_required>Model.State.region)SourceLine("需开放第"+facility.region_required+"区");SourceLine(TechnologyConditions(facility.technology));ContextAction("查看科技条件",()=>OpenTechnology(facility.technology));
            foreach(var amount in facility.build){string item=amount.item;ContextAction("建材来源："+Model.Config.Item(item).name,()=>OpenItemSources(item));}EndContext();
        }
        private void OpenItemSources(string item)
        {
            var def=Model.Config.Item(item);BeginContext(def.name+" · 获取途径",CatalogPoint);
            string Count(long ticks)=>(ticks/(double)PlanStock.Scale).ToString("0.####");
            SourceLine("查询时可用库存：\n仓库 "+Count(Model.Stored(item))+" / 背包 "+Count(Model.State.backpack.Count(item))+"\n地面 "+Count(Model.State.ground.Sum(g=>g.stock.Count(item)))+" / 设施输出 "+Count(Model.State.sites.Where(s=>s.type!="warehouse").Sum(s=>s.output.Count(item))));
            var pile=Model.State.ground.Where(g=>g.stock.Count(item)>0&&Model.Owned(g.x,g.y)).OrderBy(g=>Math.Abs(g.x-Model.State.player.x)+Math.Abs(g.y-Model.State.player.y)).FirstOrDefault();
            if(pile!=null){int id=pile.id;ContextAction("定位地面库存",()=>LocateCatalogPile(id));}
            foreach(var site in Model.State.sites.Where(s=>s.type!="warehouse"&&s.output.Count(item)>0).Take(6)){int id=site.id;ContextAction("定位产物："+Model.Config.Facility(site.type).name+" #"+id,()=>LocateSite(id));}
            if(def.canBuy)SourceLine("可在库存页采购；单价 "+def.buy.ToString("0.##")+"钱，仍受资金与仓储容量限制。");
            var regions=Model.Config.regions.Where(r=>r.fixed_items.Any(a=>a.item==item)).Select(r=>r.id.ToString()).ToArray();if(regions.Length>0)SourceLine("第"+string.Join("、",regions)+"区初始物资包含该物品；已被取走的物资不会因此补发。");
            var recipes=Model.Config.recipes.Where(r=>r.outputs.Any(a=>a.item==item)).OrderByDescending(Model.RecipeAvailable).ThenBy(r=>r.region_required).ToArray();
            foreach(var recipe in recipes){var r=recipe;ContextAction((Model.RecipeAvailable(r)?"配方：":"未解锁配方：")+r.name,()=>OpenRecipeCard(r));}
            if(recipes.Length==0)SourceLine("配方目录没有直接制造方法。检查地面物资、旧件内容或可用采购渠道。");
            SourceLine("这里列出配方目录。混合垃圾、旧件与封装物的实际内容以具体物品为准。");EndContext();
        }
        private void LocateCatalogPile(int id)
        {
            var pile=Model.State.ground.FirstOrDefault(g=>g.id==id&&g.stock.stacks.Count>0);if(pile==null){Notice="物品已被取走。";return;}
            CancelMapGesture();HideManagement();mapOverview=false;mapCamera.transform.position=new Vector3(pile.x,pile.y,-10);mapCamera.orthographicSize=12;OpenWorldContext(pile.x,pile.y,CatalogPoint,false);
        }
        private void OpenRecipeCard(PlanRecipe r)
        {
            BeginContext(r.name+" · 制作方法",CatalogPoint);
            string inputs=r.id=="lift_test"?"平台内已总装的唯一升降舱":r.inputs.Length==0?"无物料投入":Amounts(r.inputs);
            string outputs=r.id=="build_lift"?"平台内唯一载具槽":r.id=="lift_test"?"同一升降舱试车通过":r.facility=="survey"?"标记对应矿脉，无物品产出":r.outputs.Length==0?"无物品产出":Amounts(r.outputs);
            SourceLine("设施："+Model.Config.Facility(r.facility).name+"\n投入："+inputs+"\n产出："+outputs);
            SourceLine("阶段用时："+string.Join(" → ",r.stages.Select(s=>(s.kind=="human"?"人工":s.kind=="machine"?"机器":s.kind=="lit_growth"?"光照生长":"自然")+s.seconds.ToString("0.##")+"秒"))+"\n机器负载："+r.power.ToString("0.##")+"；实际速度受工人、供电等影响。");
            if(r.tools.Length>0)SourceLine("工具损耗："+string.Join("、",r.tools.Select(a=>ToolFamily(a.item)+"×"+a.quantity.ToString("0.####"))));
            if((r.addons??new PlanAmount[0]).Length>0)SourceLine("先安装附件："+Amounts(r.addons));
            if(r.requires_grow_lamp)SourceLine("需要绑定有电的太阳灯。");if(r.quality_required=="qualified_fresh_source_only")SourceLine("原水须来自合格淡水水源。");
            if(r.facility=="mine")SourceLine("需建在对应的已勘探矿点，并选择与矿脉相符的配方。");
            if(r.id=="lift_ascent")SourceLine("试车通过后让主角到操作侧确认登舱；不能直接选择此配方启动返程。");
            if(r.actual_composition||r.preserve_payload)SourceLine("实际产物还取决于投入实例的成分或封装内容。");
            if(!string.IsNullOrEmpty(r.requires_state)||r.requires_unrecognized_composition)SourceLine("投入物品还须符合该工序的实例状态或成分条件。");
            if(r.region_required>Model.State.region)SourceLine("需开放第"+r.region_required+"区");
            foreach(string tech in r.technologies){string id=tech;SourceLine(TechnologyConditions(id));ContextAction("查看科技："+TechName(id),()=>OpenTechnology(id));}
            foreach(string item in (r.id=="lift_test"?new string[0]:r.inputs.Select(a=>a.item)).Concat((r.addons??new PlanAmount[0]).Select(a=>a.item)).Distinct()){string id=item;ContextAction("获取："+Model.Config.Item(id).name,()=>OpenItemSources(id));}
            var facility=Model.State.sites.FirstOrDefault(s=>s.type==r.facility);
            if(facility!=null){int id=facility.id;ContextAction("定位已有制作设施",()=>LocateSite(id));}
            ContextAction("查看设施建造条件",()=>OpenFacilityRequirements(Model.Config.Facility(r.facility)));EndContext();
        }
        private void OpenSiteMaterials(PlanSite site)
        {
            BeginContext("缺料与制作方法",CatalogPoint);if(string.IsNullOrEmpty(site.recipe)){SourceLine("请先在详细设置中选择配方。");EndContext();return;}
            var recipe=Model.Config.Recipe(site.recipe);var missing=Model.InspectFacility(site).missing;
            SourceLine(missing.Length==0?"本批次没有普通原料缺额，可查阅配方与后续投入。":"当前缺额："+Amounts(missing));ContextAction("当前配方："+recipe.name,()=>OpenRecipeCard(recipe));
            foreach(string item in missing.Select(a=>a.item).Concat(recipe.inputs.Select(a=>a.item)).Distinct()){string id=item;ContextAction("获取："+Model.Config.Item(id).name,()=>OpenItemSources(id));}EndContext();
        }
        private void PickCatalogItem(string item)
        {
            var p=Model.State.player;var g=Model.State.ground.Where(a=>a.stock.Count(item)>0).OrderBy(a=>Math.Abs(a.x-p.x)+Math.Abs(a.y-p.y)).FirstOrDefault();
            if(g==null)throw new InvalidOperationException("物品已被取走。");Model.Pick(g.id,item,Math.Min(PlanStock.Scale,g.stock.Count(item)));
        }
    }
}
