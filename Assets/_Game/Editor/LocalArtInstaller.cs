using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace CrazyRubbish.Editor
{
    public static class LocalArtInstaller
    {
        private const string Root = "Assets/_Game/Art/Local/";
        [Serializable] private class Manifest { public string version; public FileEntry[] files; }
        [Serializable] private class FileEntry { public string path, sha256; public Slice[] sprites; }
        [Serializable] private class Slice { public string name; public int x, y, width, height; public float pivotX, pivotY; }

        public static void EnsureInstalled(PrototypeAppearance appearance)
        {
            if (appearance == null) return;
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "ImportManifest.json"));
            if (appearance.localArtVersion != manifest.version) Apply(appearance);
        }

        public static void Validate()
        {
            PrototypePresentationEditor.ValidateSavedPresentation();
            var appearance = AssetDatabase.LoadAssetAtPath<PrototypeAppearance>(PlanProjectSetup.AppearancePath);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "ImportManifest.json"));
            int slices = 0;
            foreach (var file in manifest.files)
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file.path))).Replace("-", "").ToLowerInvariant();
                    if (hash != file.sha256) throw new InvalidOperationException("Source image changed: " + file.path);
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.path);
                if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
                    throw new InvalidOperationException("Pixel import settings are wrong: " + file.path);
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(file.path).OfType<Sprite>().ToArray();
                if (sprites.Length != file.sprites.Length || file.sprites.Any(s => !sprites.Any(p => p.name == s.name && p.rect == new Rect(s.x,s.y,s.width,s.height))))
                    throw new InvalidOperationException("Missing or misplaced sprite slices: " + file.path);
                slices += sprites.Length;
            }
            if (appearance.localArtVersion != manifest.version || appearance.itemIcons.Any(s => s == null))
                throw new InvalidOperationException("Missing appearance or inventory references.");
            foreach (PlanCharacterSprites bank in new[] { appearance.male, appearance.female }.Concat(appearance.residents))
                foreach (var pose in new[] { bank.idle, bank.carry, bank.pickup, bank.saw })
                    if (pose == null || pose.Length != 4 || pose.Any(s => s == null)) throw new InvalidOperationException("Missing actor direction/pose.");
            var plan = UnityEngine.Object.FindObjectOfType<PlanGameController>();
            var legacy = UnityEngine.Object.FindObjectsOfType<PrototypeGame>(true).Single();
            if (plan == null || plan.appearance != appearance || legacy.appearance == appearance || legacy.gameObject.activeSelf)
                throw new InvalidOperationException("New and legacy scene/artwork are not isolated.");
            if (appearance.planItems == null || appearance.planFacilities == null || appearance.planItems.Any(p => p.sprite == null) || appearance.planFacilities.Any(p => p.sprite == null))
                throw new InvalidOperationException("Current game artwork mappings are incomplete.");
            string report = "{\"success\":true,\"pngCount\":" + manifest.files.Length + ",\"spriteCount\":" + slices + ",\"itemIcons\":" + appearance.itemIcons.Length + ",\"characterBanks\":6}";
            Directory.CreateDirectory("TestResults"); File.WriteAllText("TestResults/local-art.json", report);
            Debug.Log("LOCAL_ART_VALIDATION_PASS: " + report);
        }

        public static void Apply(PrototypeAppearance appearance)
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "ImportManifest.json"));
            foreach (var file in manifest.files)
            {
                var importer = AssetImporter.GetAtPath(file.path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing imported texture: " + file.path);
                string stamp = manifest.version + ":" + file.sha256;
                if (importer.userData == stamp) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 64;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                var factories = new SpriteDataProviderFactories(); factories.Init();
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var existing = provider.GetSpriteRects().ToDictionary(s => s.name, s => s.spriteID);
                var rects = file.sprites.Select(s => new SpriteRect {
                    name = s.name, rect = new Rect(s.x, s.y, s.width, s.height),
                    alignment = SpriteAlignment.Custom, pivot = new Vector2(s.pivotX, s.pivotY),
                    spriteID = existing.TryGetValue(s.name, out GUID id) ? id : GUID.Generate()
                }).ToArray();
                provider.SetSpriteRects(rects);
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
                provider.Apply();
                importer.userData = stamp;
                importer.SaveAndReimport();
            }
            Sprite Load(string group, string file, string slice = "default")
            {
                string path = Root + group + "/" + file + ".png";
                var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == slice);
                if (sprite == null) throw new InvalidOperationException("Missing sprite: " + path + " / " + slice);
                return sprite;
            }
            Sprite Env(string file) => Load("Environment", file);
            Sprite Building(string file) => Load("Buildings", file);
            Sprite Core(string name) => Load("Atlases", "atlas_items_core_v1", name);
            Sprite Extra(string name) => Load("Atlases", "atlas_items_extra_v1", name);
            PlanCharacterSprites Character(string idlePrefix, string interaction)
            {
                string[] directions = { "down", "up", "left", "right" };
                return new PlanCharacterSprites {
                    idle = directions.Select(d => Load("Characters", idlePrefix + d)).ToArray(),
                    carry = directions.Select(d => Load("Characters", interaction, "carry_" + d)).ToArray(),
                    pickup = directions.Select(d => Load("Characters", interaction, "pickup_" + d)).ToArray(),
                    saw = directions.Select(d => Load("Characters", interaction, "saw_" + d)).ToArray()
                };
            }
            appearance.male = Character("chr_hero_", "chr_hero_male_interactions");
            appearance.female = Character("chr_hero_female_", "chr_hero_female_interactions");
            appearance.residents = Enumerable.Range(1, 4).Select(i => Character(i == 1 ? "chr_worker_" : $"chr_worker_{i:00}_", $"chr_worker_{i:00}_interactions")).ToArray();
            appearance.worker = appearance.male.Get(0);
            appearance.wasteWood = Env("env_waste_wood_low");
            appearance.woodHeapHigh = Env("env_waste_wood_high");
            appearance.mixedPlastic = Env("env_waste_mixed_low");
            appearance.mixedHeapHigh = Env("env_waste_mixed_high");
            appearance.metalHeap = Env("env_waste_metal_low");
            appearance.metalHeapHigh = Env("env_waste_metal_high");
            appearance.warehouse = Building("bld_old_warehouse_south_idle");
            appearance.housing = Building("bld_old_housing_south_unpowered");
            appearance.hole = Env("env_hole_east");
            appearance.lamp = Building("bld_streetlamp_unpowered");
            appearance.generator = Building("bld_generator_idle");
            appearance.workbench = Building("bld_woodwork_loaded");
            appearance.assemblyTable = Building("bld_workbench_idle");
            appearance.dismantleTable = Building("bld_disassembly_idle");
            appearance.fastener = Core("fasteners");
            appearance.saw = Core("handsaw");
            appearance.plank = appearance.woodHandle = Core("boards");
            appearance.gameCover = Load("UI", "GameCover");
            Sprite[] Tiles(string file) => Enumerable.Range(0, 16).Select(i => Load("Environment", file, "tile_" + i)).ToArray();
            appearance.bedrockTiles = Tiles("env_cave_bedrock_v1");
            appearance.gravelTiles = Tiles("env_cave_gravel_v1");
            appearance.compactedTiles = Tiles("env_cave_compacted_v1");
            appearance.tile = appearance.bedrockTiles[0];
            var icons = new Dictionary<ItemId, Sprite> {
                {ItemId.WasteWood,Core("waste_wood")}, {ItemId.SortedWood,Core("waste_wood")},
                {ItemId.MixedPlastic,appearance.mixedPlastic}, {ItemId.RecoveredFastener,Core("fasteners")},
                {ItemId.WoodHandSaw,Core("handsaw")}, {ItemId.Plank,Core("boards")}, {ItemId.WoodHandle,Core("boards")},
                {ItemId.WoodChips,Core("wood_chips")}, {ItemId.SteelPart,Core("steel_parts")},
                {ItemId.CopperPart,Extra("copper_parts")}, {ItemId.RecoveredWire,Core("wire")},
                {ItemId.ThermalModule,Core("thermal_module")}, {ItemId.GeneratorModule,Core("generator_module")},
                {ItemId.Food,Core("food")}, {ItemId.DrinkingWater,Core("water")},
                {ItemId.Soap,Extra("soap")}, {ItemId.WasteTextile,Extra("waste_textile")}, {ItemId.OldClothes,Extra("old_clothes")},
                {ItemId.Screwdriver,Extra("screwdriver")}, {ItemId.Wrench,Extra("wrench")}, {ItemId.Hammer,Extra("hammer")},
                {ItemId.Scissors,Extra("scissors")}, {ItemId.SewingKit,Extra("sewing")}
            };
            icons[ItemId.PowerAsh] = Core("wood_chips");
            icons[ItemId.SawBlade] = Core("handsaw");
            icons[ItemId.MachineBlade] = Core("handsaw");
            // The art pack has representative material/module icons; retain actual item names and rules.
            foreach (ItemId item in Enum.GetValues(typeof(ItemId)))
            {
                if (icons.ContainsKey(item)) continue;
                string name = item.ToString();
                Sprite sprite = Core("steel_parts");
                if (name.Contains("Copper")) sprite = Extra("copper_parts");
                else if (name.Contains("Wire")) sprite = Core("wire");
                else if (name.Contains("Motor")) sprite = Core("generator_module");
                else if (name.Contains("Heat") || name.Contains("Furnace") || name == "PowerAsh") sprite = Core("thermal_module");
                else if (name.Contains("Lamp") || name.Contains("Light")) sprite = Building("bld_grow_light_idle");
                else if (name.Contains("Wood") || name.Contains("Broom")) sprite = Core("boards");
                else if (name.Contains("Cutter") || name.Contains("Blade")) sprite = Building("bld_electric_woodwork_idle");
                else if (name.Contains("Washer")) sprite = Building("bld_washing_idle");
                else if (name.Contains("Former") || name.Contains("Mould")) sprite = Building("bld_plastic_reprocessing_idle");
                else if (name.Contains("MetalTable")) sprite = Building("bld_metal_parts_idle");
                icons[item] = sprite;
            }
            appearance.itemIcons = Enum.GetValues(typeof(ItemId)).Cast<ItemId>().Select(i => icons[i]).ToArray();
            appearance.localArtVersion = manifest.version;
            EditorUtility.SetDirty(appearance);
            AssetDatabase.SaveAssets();
            Debug.Log($"Local art installed: {manifest.files.Length} unchanged PNGs, {manifest.files.Sum(f => f.sprites.Length)} sprites.");
        }
    }
}
