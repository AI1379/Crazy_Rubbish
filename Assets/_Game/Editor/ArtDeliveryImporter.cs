using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace CrazyRubbish.Editor
{
    /// <summary>Import the supplied originals without modifying their PNG bytes.</summary>
    public static class ArtDeliveryImporter
    {
        internal const string Root = "Assets/_Game/Art/Delivery_20261013";
        private const string ManifestPath = Root + "/ImportManifest.json";
        private static long manifestTimestamp;
        private static Manifest manifest;
        private static Dictionary<string, TextureDefinition> texturesByPath;

        [Serializable]
        internal sealed class Manifest
        {
            public int textureCount, spriteCount, atlasSliceCount;
            public TextureDefinition[] textures;
        }

        [Serializable]
        internal sealed class TextureDefinition
        {
            public string path, sha256;
            public int width, height;
            public bool multiple, repeat, hasAlpha;
            public float pixelsPerUnit;
            public SpriteDefinition[] sprites;
        }

        [Serializable]
        internal sealed class SpriteDefinition
        {
            public string name, spriteId;
            public int x, y, width, height;
            public float pivotX, pivotY;
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public bool success;
            public int textures, sprites, atlasSlices;
            public string[] spriteReferences;
        }

        private static Manifest ReadManifest()
        {
            long timestamp = File.GetLastWriteTimeUtc(ManifestPath).Ticks;
            if (manifest != null && timestamp == manifestTimestamp) return manifest;
            if (!File.Exists(ManifestPath)) throw new FileNotFoundException("Run Tools/ImportArtDelivery.py first.", ManifestPath);
            manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.textures == null || manifest.textures.Length != manifest.textureCount)
                throw new InvalidDataException("The art delivery import manifest is incomplete.");
            texturesByPath = manifest.textures.ToDictionary(texture => texture.path, StringComparer.Ordinal);
            manifestTimestamp = timestamp;
            return manifest;
        }

        internal static TextureDefinition FindTexture(string path)
        {
            ReadManifest();
            return texturesByPath.TryGetValue(path, out var texture) ? texture : null;
        }

        internal static void Configure(TextureImporter importer, TextureDefinition texture)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = texture.multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = texture.pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.alphaSource = texture.hasAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = texture.hasAlpha;
            importer.wrapMode = texture.repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.isReadable = false;
            importer.sRGBTexture = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(texture.sprites[0].pivotX, texture.sprites[0].pivotY);
            importer.SetTextureSettings(settings);

            // Clear platform overrides so source resolution and lossless import apply everywhere.
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                importer.ClearPlatformTextureSettings(platform);
            if (!texture.multiple) return;

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("No Sprite Editor data provider for " + texture.path);
            provider.InitSpriteEditorDataProvider();
            var rects = texture.sprites.Select(sprite => new SpriteRect
            {
                name = sprite.name,
                rect = new Rect(sprite.x, sprite.y, sprite.width, sprite.height),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2(sprite.pivotX, sprite.pivotY),
                spriteID = new GUID(sprite.spriteId)
            }).ToArray();
            provider.SetSpriteRects(rects);
            var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameProvider == null) throw new InvalidOperationException("No stable Sprite ID provider for " + texture.path);
            nameProvider.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
        }

        [MenuItem("Crazy Rubbish/Art/Apply October 13 Delivery Import Settings")]
        public static void ImportAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var texture in ReadManifest().textures)
            {
                var importer = AssetImporter.GetAtPath(texture.path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing texture importer: " + texture.path);
                Configure(importer, texture);
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Crazy Rubbish/Art/Validate October 13 Delivery")]
        public static void Validate()
        {
            var source = ReadManifest();
            int total = 0, slices = 0;
            var references = new List<string>();
            foreach (var definition in source.textures)
            {
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(definition.path))).Replace("-", "").ToLowerInvariant();
                    if (hash != definition.sha256) throw new InvalidDataException("Original PNG bytes changed: " + definition.path);
                }
                var importer = AssetImporter.GetAtPath(definition.path) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite ||
                    importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    importer.npotScale != TextureImporterNPOTScale.None || importer.isReadable ||
                    importer.spriteImportMode != (definition.multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single) ||
                    Mathf.Abs(importer.spritePixelsPerUnit - definition.pixelsPerUnit) > 0.001f)
                    throw new InvalidDataException("Unexpected sprite import settings: " + definition.path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(definition.path);
                if (texture == null || texture.width != definition.width || texture.height != definition.height)
                    throw new InvalidDataException("Original texture resolution was not preserved: " + definition.path);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(definition.path).OfType<Sprite>().ToArray();
                if (sprites.Length != definition.sprites.Length)
                    throw new InvalidDataException("Wrong sprite count: " + definition.path);
                foreach (var expected in definition.sprites)
                {
                    var sprite = sprites.SingleOrDefault(value => value.name == expected.name);
                    if (sprite == null || sprite.rect != new Rect(expected.x, expected.y, expected.width, expected.height) ||
                        Vector2.Distance(sprite.pivot, new Vector2(expected.pivotX * expected.width, expected.pivotY * expected.height)) > 0.01f)
                        throw new InvalidDataException("Sprite rectangle or pivot differs: " + definition.path + " / " + expected.name);
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId) || fileId == 0)
                        throw new InvalidDataException("Sprite has no persistent reference: " + expected.name);
                    references.Add(definition.path + " / " + expected.name + " / " + guid + " / " + fileId);
                }
                total += sprites.Length;
                if (definition.multiple) slices += sprites.Length;
            }
            if (source.textureCount != 71 || total != source.spriteCount || slices != source.atlasSliceCount)
                throw new InvalidDataException("The delivery totals do not match the supplied manifest.");
            var report = new ValidationReport
            {
                success = true, textures = source.textureCount, sprites = total, atlasSlices = slices,
                spriteReferences = references.ToArray()
            };
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/art-delivery.json", JsonUtility.ToJson(report, true));
            Debug.Log("Art delivery verified: " + source.textureCount + " PNGs, " + total + " sprites, " + slices + " atlas slices; original bytes and resolutions preserved.");
        }
    }

    // First imports also receive the manifest settings on another developer's checkout.
    // Later ordinary reimports retain manual Sprite Editor calibration.
    public sealed class ArtDeliveryTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtDeliveryImporter.Root + "/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !assetImporter.importSettingsMissing) return;
            var definition = ArtDeliveryImporter.FindTexture(assetPath);
            if (definition == null) throw new InvalidDataException("PNG absent from the delivery manifest: " + assetPath);
            ArtDeliveryImporter.Configure((TextureImporter)assetImporter, definition);
        }
    }
}
