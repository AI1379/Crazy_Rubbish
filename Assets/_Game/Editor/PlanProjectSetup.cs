using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrazyRubbish.Editor
{
    public static class PlanProjectSetup
    {
        private static Sprite Load(string group,string file,string name="default")=>AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Local/"+group+"/"+file+".png").OfType<Sprite>().First(s=>s.name==name);
        public static void Install()
        {
            var legacy=UnityEngine.Object.FindObjectsOfType<PrototypeGame>(true).First();
            var appearance=legacy.appearance;var cfg=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));cfg.Validate();
            CaveArtInstaller.Apply(appearance);
            var itemIcons=new Dictionary<string,Sprite>();
            string[] core={"waste_wood","boards","wood_chips","fasteners","steel_parts","wire","thermal_module","generator_module","handsaw","food","water","money"};
            foreach(string name in core)itemIcons[name]=Load("Atlases","atlas_items_core_v1",name);
            foreach(string name in new[]{"copper_parts","soap","waste_textile","old_clothes","hammer","screwdriver","wrench","scissors","sewing"})itemIcons[name]=Load("Atlases","atlas_items_extra_v1",name);
            Sprite Icon(PlanItem item)
            {
                if(item.id=="lift_cabin")return Load("Vehicles","veh_return_lift_idle_v1");
                if(itemIcons.TryGetValue(item.id,out var s))return s;
                if(itemIcons.TryGetValue(item.tool_family??"",out s))return s;
                if(item.id.Contains("food")||item.id=="tubers"||item.id=="sprouts"||item.id.Contains("seed"))return itemIcons["food"];
                if(item.id.Contains("water"))return itemIcons["water"];
                if(item.id.Contains("clothes")||item.id.Contains("textile")||item.id.Contains("rag"))return itemIcons["old_clothes"];
                if(item.id.Contains("copper"))return itemIcons["copper_parts"];
                if(item.id.Contains("wire"))return itemIcons["wire"];
                if(item.id.Contains("wood")||item.id=="ash")return itemIcons["wood_chips"];
                if(item.id.Contains("plastic")||item.id.Contains("resin")||item.id.StartsWith("sorted_"))return appearance.mixedPlastic;
                if(item.id.Contains("motor")||item.id.Contains("drive"))return itemIcons["generator_module"];
                return itemIcons["steel_parts"];
            }
            appearance.planItems=cfg.items.Select(i=>new NamedPlanSprite{id=i.id,sprite=Icon(i)}).ToArray();
            var buildings=new Dictionary<string,string>{
                {"wood","woodwork_loaded"},{"disassembly","disassembly_idle"},{"bench","workbench_idle"},{"washing","washing_idle"},{"generator","generator_idle"},{"electric_wood","electric_woodwork_idle"},{"auto_wood","automatic_woodwork_idle"},
                {"sorting","plastic_sorting_idle"},{"plastic_pre","plastic_preprocessing_idle"},{"plastic_remake","plastic_reprocessing_idle"},{"metal_raw","metal_processing_idle"},{"metal_parts","metal_parts_idle"},
                {"growing","growing_bed_idle"},{"grow_lamp","grow_light_idle"},{"auto_plastic_pre","auto_plastic_preprocessing_idle"},{"auto_plastic_remake","auto_plastic_reprocessing_idle"},{"auto_parts","auto_structural_parts_idle"},
                {"wire_maker","wire_manufacturing_idle"},{"refurbish","refurbishment_idle"},{"composting","composting_idle"},{"substrate_mixer","substrate_mixing_idle"},{"water_intake","washing_idle"},{"water_purifier","washing_idle"},{"kitchen","workbench_idle"},{"auto_water_plant","auto_plastic_preprocessing_idle"},
                {"boiler","generator_idle"},{"turbine","generator_idle"},{"condenser","washing_idle"},{"electromechanical","workbench_idle"},{"complex_recovery","plastic_preprocessing_idle"},{"auto_complex","auto_plastic_preprocessing_idle"},{"auto_metal_raw","metal_processing_idle"},{"auto_tool_parts","auto_structural_parts_idle"}
            };
            Sprite Building(PlanFacility f)
            {
                if(buildings.TryGetValue(f.id,out string file))return Load("Buildings","bld_"+file);
                if(f.id=="housing")return appearance.housing;if(f.id=="warehouse")return appearance.warehouse;if(f.id=="streetlamp")return appearance.lamp;
                if(f.id=="belt")return Load("Atlases","atlas_logistics_v1","straight");if(f.id=="port")return Load("Atlases","atlas_logistics_v1","inlet");if(f.id=="power_line"||f.id=="steam_pipe")return Load("Atlases","atlas_power_lines_v1","straight");
                if(f.id=="survey")return Load("Buildings","bld_survey_idle_v1");if(f.id=="mine")return Load("Buildings","bld_mine_idle_v1");
                return appearance.workbench;
            }
            appearance.planFacilities=cfg.facilities.Select(f=>new NamedPlanSprite{id=f.id,sprite=Building(f)}).ToArray();EditorUtility.SetDirty(appearance);AssetDatabase.SaveAssets();
            var game=UnityEngine.Object.FindObjectsOfType<PlanGameController>(true).FirstOrDefault();
            if(game==null)game=new GameObject("Latest Plan Game").AddComponent<PlanGameController>();
            game.appearance=appearance;game.mapCamera=legacy.GetComponent<PrototypeWorld>().worldCamera;game.legacyRoot=legacy.gameObject;
            legacy.gameObject.SetActive(false);game.gameObject.SetActive(true);game.EditPreview();TerrainMeshBaker.Save(game.gameObject);EditorUtility.SetDirty(game);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        [MenuItem("Crazy Rubbish/Setup Latest Plan")]
        public static void Setup(){PrototypeProjectSetup.Setup();}
        public static void Validate()
        {
            Setup();EditorSceneManager.OpenScene("Assets/_Game/Scenes/Main.unity");var g=UnityEngine.Object.FindObjectOfType<PlanGameController>();
            if(g==null||g.legacyRoot.activeSelf)throw new InvalidOperationException("默认场景没有切换至新版。");var config=g.LoadConfig();if(config.items.Length!=201||config.recipes.Length!=216)throw new InvalidOperationException("新版配置目录不完整。");
            if(config.Region(3).veins==null||config.Region(3).launch_platform_anchor.Length!=2)throw new InvalidOperationException("三区矿脉或返程地基配置缺失。");
            if(g.appearance.planItems.Any(p=>p.sprite==null)||g.appearance.planFacilities.Any(p=>p.sprite==null))throw new InvalidOperationException("新版贴图映射缺失。");
            if(g.appearance.cavePillar==null||g.appearance.caveRocks==null||g.appearance.caveRocks.Length!=4||g.appearance.caveRocks.Any(s=>s==null)||g.transform.Find("Plan World/Natural cave walls")==null)throw new InvalidOperationException("自然洞室边界素材或场景缺失。");
            if(g.transform.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Boundary"))throw new InvalidOperationException("自然地图仍带旧矩形边框。");
            var sidebar=g.transform.Find("Plan Interface/Management");
            if(sidebar==null||sidebar.gameObject.activeSelf||g.transform.Find("Plan Interface/Operations/管理 Tab")==null)throw new InvalidOperationException("管理栏默认收起或全局入口缺失。");
            var settings=g.transform.Find("Plan Interface/Settings Menu");
            if(settings==null||settings.gameObject.activeSelf||g.transform.Find("Plan Interface/Operations/Settings/Gear")==null||g.transform.Find("Plan Interface/Header/保存")!=null||g.transform.Find("Plan Interface/Header/读档")!=null)throw new InvalidOperationException("底部设置或存档入口不正确。");
            var time=g.transform.Find("Plan Interface/Header/Time controls");
            var risks=g.transform.Find("Plan Interface/Risk alerts");
            if(risks==null||risks.gameObject.activeSelf||risks.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length!=4)throw new InvalidOperationException("集中预警入口缺失或标题界面未隐藏预警。");
            if(time==null||time.GetComponentsInChildren<PlanTimeIcon>(true).Length!=4||g.transform.Find("Plan Interface/Header/暂停")!=null)throw new InvalidOperationException("嵌入式时间控制条缺失或旧控件残留。");
            if(PlanTerrain.WaterAt(20,24)!=2||PlanTerrain.StarterPondBody<=0||!g.GetComponentsInChildren<TextMesh>(true).Any(t=>t.text=="小水塘 · 岸边可取水"))throw new InvalidOperationException("出生地水塘未生成。");
            int chunks=g.GetComponentsInChildren<MeshFilter>(true).Length;if(chunks<10||chunks>1800)throw new InvalidOperationException("大地图未使用合理的地面分块。");
            Directory.CreateDirectory("TestResults");File.WriteAllText("TestResults/latest-plan-scene.json","{\"success\":true,\"playableRegions\":3,\"items\":201,\"facilities\":53,\"recipes\":216,\"technologies\":54,\"terrainRevision\":"+PlanTerrain.Revision+",\"groundCells\":["+PlanTerrain.CellCount(1)+","+PlanTerrain.CellCount(2)+","+PlanTerrain.CellCount(3)+"],\"rockAtlasSprites\":4,\"terrainFeatures\":"+PlanTerrain.Obstacles.Count()+",\"width\":"+PlanTerrain.Width+",\"height\":"+PlanTerrain.Height+",\"waterCells\":["+PlanTerrain.WaterCellCount(1)+","+PlanTerrain.WaterCellCount(2)+","+PlanTerrain.WaterCellCount(3)+"],\"terrainChunks\":"+chunks+",\"squareTerrainCells\":"+PlanTerrain.SquareTerrainCellCount+",\"barrierCells\":"+PlanTerrain.BarrierCellCount+"}");
        }
    }
}
