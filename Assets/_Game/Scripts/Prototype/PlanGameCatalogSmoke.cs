using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private IEnumerator SmokeCatalog(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("生产查阅："+message);}
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            var saved=Model;Model=new PlanSimulation(LoadConfig());Model.SetPaused(true);MenuOpen=false;buildSearch="";showLockedBuildings=true;BuildPresentation();ShowManagement(1);yield return null;
            var input=buildSearchField;EventSystem.current.SetSelectedGameObject(input.gameObject);input.ActivateInputField();yield return null;
            Check(EditingText,"搜索输入没有获得键盘焦点");input.text="  发电  ";yield return null;
            Check(input==buildSearchField&&EditingText&&input.text=="  发电  ","筛选重建了输入框或丢失焦点");
            Check(MatchingBuildings().Any(d=>d.id=="generator"),"中文搜索没有找到发电机");Shot("-build-search");
            int techCount=Model.State.technologies.Count;var generator=Model.Config.Facility("generator");var locked=buildResults.GetComponentsInChildren<Button>().First(b=>b.name=="未解锁："+generator.name);locked.onClick.Invoke();Check(ContextOpen&&buildType==null&&Model.State.technologies.Count==techCount,"查看未解锁设施越过科技限制");
            CloseContext();input.text="GENERATOR";Check(MatchingBuildings().Any(d=>d.id=="generator"),"英文搜索未忽略大小写");input.text="没有这种设施XYZ";Check(MatchingBuildings().Length==0&&buildResults.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("没有匹配")),"无结果没有提示");
            input.DeactivateInputField();EventSystem.current.SetSelectedGameObject(null);Check(!EditingText,"结束输入后仍吞掉快捷键");
            showLockedBuildings=false;input.text="GENERATOR";RefreshBuildResults();Check(MatchingBuildings().Length==0,"未解锁发电机提前出现在可建目录");
            foreach(var item in Model.Config.items)Model.Discover(item.id);RefreshInterface();Check(input==buildSearchField&&input.text=="GENERATOR"&&buildResults.GetComponentsInChildren<Button>().Any(b=>b.name==generator.name+" 2×2"),"解锁后没有实时刷新建设结果或丢失搜索");
            input.text="";RefreshBuildResults();Check(MatchingBuildings().All(d=>d.region_required<=Model.State.region&&Model.State.technologies.Contains(d.technology)),"已解锁筛选混入其他区域设施");
            input.text=Model.Config.Item(generator.build[0].item).name;Check(MatchingBuildings().Any(d=>d.id=="generator"),"不能按建材搜索设施");
            string before=JsonUtility.ToJson(Model.Capture());OpenItemSources("boards");yield return null;Shot("-item-sources");
            var recipe=Model.Config.Recipe("wood_boards");contextBody.GetComponentsInChildren<Button>().First(b=>b.name=="配方："+recipe.name).onClick.Invoke();Check(contextMenu.Find("Title").GetComponent<Text>().text.Contains("制作方法")&&contextBody.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("阶段用时")),"物品无法跳转制作方法");yield return null;Shot("-recipe-card");
            contextBody.GetComponentsInChildren<Button>().First(b=>b.name=="获取："+Model.Config.Item("waste_wood").name).onClick.Invoke();Check(contextMenu.Find("Title").GetComponent<Text>().text.Contains("获取途径"),"配方无法继续查原料来源");Check(JsonUtility.ToJson(Model.Capture())==before,"浏览配方修改了经营台账");
            var tech=Model.Config.technologies.First(t=>t.region_required==3);OpenTechnology(tech.id);Check(contextBody.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("需开放第3区")),"科技说明缺少区域条件");
            OpenRecipeCard(Model.Config.recipes.First(r=>(r.addons??new PlanAmount[0]).Length>0));Check(contextBody.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("先安装附件")),"自动化配方未说明附件");
            OpenRecipeCard(Model.Config.Recipe("lift_test"));Check(contextBody.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("同一升降舱试车通过")),"试车被描述为普通舱体消耗");
            foreach(var amount in Model.Config.Facility("wood").build)Model.State.warehouse.Add(amount.item,amount.Ticks);var wood=Model.Build("wood",8,8);Model.SelectRecipe(wood,"wood_boards");
            OpenSiteMaterials(wood);Check(contextBody.GetComponentsInChildren<Button>().Any(b=>b.name=="获取："+Model.Config.Item("waste_wood").name),"设施缺料没有查阅入口");
            CloseContext();Model.State.ground.Clear();Model.State.warehouse.Add("food",PlanStock.Scale);ShowManagement(3);long cash=Model.State.cash;panelContent.GetComponentsInChildren<Button>().First(b=>b.name=="拾1").onClick.Invoke();Check(Notice=="物品已被取走。"&&Model.State.cash==cash,"物品消失后拾取提示不正确");
            Model=saved;buildSearch="";showLockedBuildings=false;buildType=null;moveX=-1;BuildPresentation();RefreshPresentation(true);
        }
    }
}
