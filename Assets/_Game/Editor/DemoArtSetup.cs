using System;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Editor
{
    public static class DemoArtSetup
    {
        private const string Root = ArtDeliveryImporter.Root + "/";
        private const string Extras = "补充贴图_20261012/";

        private static Sprite Load(string path, string name = null)
        {
            Sprite result = name == null ? AssetDatabase.LoadAssetAtPath<Sprite>(Root + path) :
                AssetDatabase.LoadAllAssetsAtPath(Root + path).OfType<Sprite>().SingleOrDefault(sprite => sprite.name == name);
            if (result == null) throw new InvalidOperationException("Missing delivery sprite: " + path + " / " + name);
            return result;
        }

        private static Sprite Machine(string name) => Load("机器/生产设备/bld_" + name + ".png");
        private static Sprite Scene(string name) => Load("场景/首区基础/" + name + ".png");
        private static Sprite Core(string name) => Load(Extras + "atlas_items_core_v1.png", name);
        private static Sprite Extra(string name) => Load(Extras + "atlas_items_extra_v1.png", name);

        private static PrototypeAppearance.CharacterSprites Character(string folder, string idlePrefix, string actionId)
        {
            string[] directions = { "up", "down", "left", "right" };
            Sprite[] Pose(string action) => directions.Select(direction => Load("人物/交互姿势/chr_" + actionId + "_interactions.png", "chr_" + actionId + "_" + action + "_" + direction)).ToArray();
            return new PrototypeAppearance.CharacterSprites
            {
                idle = directions.Select(direction => Load("人物/" + folder + "/" + idlePrefix + direction + ".png")).ToArray(),
                carry = Pose("carry"), pickup = Pose("pickup"), saw = Pose("saw")
            };
        }

        public static void Apply(PrototypeAppearance appearance)
        {
            appearance.characters = new[]
            {
                Character("男主", "chr_hero_", "hero_male"),
                Character("女主", "chr_hero_female_", "hero_female"),
                Character("居民/worker_01", "chr_worker_", "worker_01"),
                Character("居民/worker_02", "chr_worker_02_", "worker_02"),
                Character("居民/worker_03", "chr_worker_03_", "worker_03"),
                Character("居民/worker_04", "chr_worker_04_", "worker_04")
            };
            appearance.worker = appearance.characters[0].idle[1];
            appearance.tile = Load(Extras + "env_ground_plain_v1.png");
            appearance.warehouse = Scene("bld_old_warehouse_south_idle");
            appearance.housing = Scene("bld_old_housing_south_unpowered");
            appearance.hole = Scene("env_hole_east");
            appearance.streetlamp = Scene("bld_streetlamp_unpowered");
            appearance.woodwork = Machine("woodwork_loaded");
            appearance.workbench = Machine("workbench_idle");
            appearance.generator = Machine("generator_idle");
            appearance.dismantle = Machine("disassembly_idle");
            appearance.electricWoodwork=Machine("electric_woodwork_idle");appearance.automaticWoodwork=Machine("automatic_woodwork_idle");
            appearance.conveyor=Load(Extras+"atlas_logistics_v1.png","直带_向北");appearance.conveyorCrossing=Load(Extras+"atlas_logistics_v1.png","交叉_两路分离");
            appearance.inputPort=Load(Extras+"atlas_logistics_v1.png","输入接口");appearance.outputPort=Load(Extras+"atlas_logistics_v1.png","输出接口");
            appearance.wasteWoodLow = Scene("env_waste_wood_low");
            appearance.wasteWoodHigh = Scene("env_waste_wood_high");
            appearance.wasteMetalLow = Scene("env_waste_metal_low");
            appearance.wasteMetalHigh = Scene("env_waste_metal_high");
            appearance.wasteMixedLow = Scene("env_waste_mixed_low");
            appearance.wasteMixedHigh = Scene("env_waste_mixed_high");
            appearance.wasteWood = Core("废木料");
            appearance.mixedPlastic = appearance.wasteMixedLow;
            appearance.fastener = Core("回收紧固件");
            appearance.saw = Core("手锯");
            appearance.plank = Core("木板");
            appearance.woodHandle = appearance.plank;

            appearance.itemIcons = new Sprite[Enum.GetValues(typeof(ItemId)).Length];
            void Bind(Sprite image, params ItemId[] items) { foreach (ItemId item in items) appearance.itemIcons[(int)item] = image; }
            Bind(appearance.wasteWood, ItemId.WasteWood, ItemId.SortedWood);
            Bind(appearance.mixedPlastic, ItemId.MixedPlastic, ItemId.PowerAsh);
            Bind(appearance.fastener, ItemId.RecoveredFastener);
            Bind(appearance.saw, ItemId.WoodHandSaw, ItemId.SawBlade, ItemId.MachineBlade, ItemId.BrokenWoodTool);
            Bind(appearance.plank, ItemId.Plank, ItemId.WoodHandle, ItemId.BroomHead);
            Bind(Core("木屑"), ItemId.WoodChips);
            Bind(Core("钢铁零件"), ItemId.SteelPart, ItemId.AluminumPart, ItemId.Fixture, ItemId.HandleMould, ItemId.BoxMould, ItemId.MouldBox);
            Bind(appearance.wasteMetalLow, ItemId.SteelMaterial, ItemId.CopperMaterial, ItemId.AluminumMaterial, ItemId.ToolSteelWaste);
            Bind(Extra("铜零件"), ItemId.CopperPart);
            Bind(Core("可用电线"), ItemId.RecoveredWire, ItemId.WireBundle);
            Bind(Core("回收热动力组件"), ItemId.ThermalModule, ItemId.HeatingElement, ItemId.FurnaceModule, ItemId.FurnaceLining);
            Bind(Core("发电组件"), ItemId.GeneratorModule, ItemId.Motor, ItemId.DamagedMotor);
            Bind(Core("未开封食物"), ItemId.Food, ItemId.Container, ItemId.OldContainer);
            Bind(Core("饮用水"), ItemId.DrinkingWater);
            Bind(Extra("肥皂"), ItemId.Soap);
            Bind(Extra("废纺织物"), ItemId.WasteTextile);
            Bind(Extra("旧衣服"), ItemId.OldClothes);
            Bind(Extra("锤"), ItemId.Hammer, ItemId.HammerHead);
            Bind(Extra("螺丝刀"), ItemId.Screwdriver, ItemId.ScrewdriverRod);
            Bind(Extra("扳手"), ItemId.Wrench);
            Bind(Extra("剪刀"), ItemId.Scissors);
            Bind(Extra("针线工具"), ItemId.SewingKit);
            Bind(Machine("electric_woodwork_idle"), ItemId.OldCutter);
            Bind(Machine("washing_idle"), ItemId.OldWasher);
            Bind(Machine("plastic_reprocessing_idle"), ItemId.OldFormer);
            Bind(Machine("grow_light_idle"), ItemId.GrowingLight, ItemId.OldGrowingLamp, ItemId.LightingModule, ItemId.OldLamp);
            Bind(Machine("plastic_preprocessing_idle"), ItemId.FeedingModule, ItemId.OldFeeder);
            Bind(Machine("metal_processing_idle"), ItemId.OldFurnace);
            Bind(Machine("metal_parts_idle"), ItemId.OldMetalTable);
            Bind(Machine("refurbishment_idle"), ItemId.OldControlBox);
            BindCatalogFallbacks(appearance, Core, Extra, Machine);
            if (appearance.itemIcons.Any(sprite => sprite == null)) throw new InvalidOperationException("An item has no delivery icon.");
        }

        // v1 items beyond the first-region slice have no dedicated art yet; reuse the closest delivered icon.
        private static void BindCatalogFallbacks(PrototypeAppearance a, Func<string, Sprite> core, Func<string, Sprite> extra, Func<string, Sprite> machine)
        {
            for (int i = 0; i < a.itemIcons.Length; i++)
            {
                if (a.itemIcons[i] != null) continue;
                var item = (ItemId)i; string key = DesignCatalog.ItemKeys[i], category = DesignCatalog.ItemCategories[i];
                string family = ItemCatalog.ToolFamily(item);
                if (category == "工具" || category == "损坏工具" || key.StartsWith("broken_"))
                    a.itemIcons[i] = key.Contains("hammer") ? extra("锤") : key.Contains("screwdriver") ? extra("螺丝刀") : key.Contains("wrench") ? extra("扳手") :
                        key.Contains("scissors") ? extra("剪刀") : key.Contains("sewing") ? extra("针线工具") : a.saw;
                else if (category == "废旧设备") a.itemIcons[i] = machine("refurbishment_idle");
                else if (category == "压缩包") a.itemIcons[i] = a.wasteMixedHigh;
                else if (key.Contains("ore") || key.Contains("concentrate") || key.Contains("billet") || key.Contains("scrap") || key == "slag" || key == "mine_tailings") a.itemIcons[i] = a.wasteMetalLow;
                else if (key.Contains("pp") || key.Contains("hdpe") || key.Contains("pet") || key.Contains("plastic") || key.Contains("resin") || key.Contains("glass") || key == "cullet" || key == "storage_box") a.itemIcons[i] = a.mixedPlastic;
                else if (key.Contains("textile") || key.Contains("clothes") || key.Contains("rag") || key.Contains("fabric") || key == "patch") a.itemIcons[i] = extra(key.Contains("clothes") ? "旧衣服" : "废纺织物");
                else if (key == "food" || key == "seed" || key == "sprouts") a.itemIcons[i] = core("未开封食物");
                else if (key.Contains("water") || key.Contains("effluent")) a.itemIcons[i] = core("饮用水");
                else if (key.Contains("paper") || key.Contains("carton") || key.Contains("board")) a.itemIcons[i] = a.plank;
                else if (key.Contains("part") || key.Contains("mold") || key.Contains("die") || key.Contains("insulator") || key == "coil") a.itemIcons[i] = core("钢铁零件");
                else if (key.Contains("module") || key.Contains("controller") || key.Contains("drive") || key.Contains("dryer") || key.Contains("melter") || key.StartsWith("lift_") || key == "reclaimed_structure") a.itemIcons[i] = core("发电组件");
                else a.itemIcons[i] = a.wasteMixedLow;
            }
        }

        [MenuItem("Crazy Rubbish/Art/Apply Delivery to Demo Scene")]
        public static void ApplyToScene()
        {
            PrototypeProjectSetup.Setup();
            ValidateScene();
        }

        public static void ValidateScene()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Main.unity", OpenSceneMode.Single);
            var game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
            var world = game.GetComponent<PrototypeWorld>();
            if (!world.HasCurrentLayout || game.appearance.characters.Length != 6)
                throw new InvalidOperationException("The saved demo art layout is incomplete.");
            foreach (var renderer in world.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sprite != game.appearance.marker && !AssetDatabase.GetAssetPath(renderer.sprite).StartsWith(Root, StringComparison.Ordinal))
                    throw new InvalidOperationException("World still uses an old sprite: " + renderer.name);
            foreach (var image in game.GetComponent<PrototypeHud>().VisualRoot.GetComponentsInChildren<Image>(true).Where(image => image.name == "Icon"))
                if (!image.preserveAspect || !AssetDatabase.GetAssetPath(image.sprite).StartsWith(Root, StringComparison.Ordinal))
                    throw new InvalidOperationException("Inventory still uses an old or stretched icon.");
            Debug.Log("Demo art verified: saved world and inventory use the October 13 delivery.");
        }
    }
}
