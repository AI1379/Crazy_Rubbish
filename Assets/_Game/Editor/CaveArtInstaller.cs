using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using CrazyRubbish.Prototype;

namespace CrazyRubbish.Editor
{
    public static class CaveArtInstaller
    {
        public const string Atlas="Assets/_Game/Art/Cave/cave_rocks_atlas_v1.png";
        public const string Pillar="Assets/_Game/Art/Cave/cave_pillar_v1.png";
        public static void Apply(PrototypeAppearance appearance)
        {
            var importer=AssetImporter.GetAtPath(Atlas) as TextureImporter;if(importer==null)throw new InvalidOperationException("Missing cave edge atlas.");
            if(importer.userData!="cave-rocks-4-v1")
            {
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=128;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                importer.GetSourceTextureWidthAndHeight(out int width,out int height);int w=width/2,h=height/2;
                var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var old=provider.GetSpriteRects();string[] names={"ridge","boulders","shale","rubble"};var rects=new SpriteRect[4];
                for(int i=0;i<4;i++)rects[i]=new SpriteRect{name=names[i],rect=new Rect(i%2*w,(1-i/2)*h,w,h),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=old.FirstOrDefault(s=>s.name==names[i])?.spriteID??GUID.Generate()};
                provider.SetSpriteRects(rects);var identifiers=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();identifiers.SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));provider.Apply();importer.userData="cave-rocks-4-v1";importer.SaveAndReimport();
            }
            var sprites=AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToArray();appearance.caveRocks=new[]{"ridge","boulders","shale","rubble"}.Select(name=>sprites.First(s=>s.name==name)).ToArray();
            var pillar=AssetImporter.GetAtPath(Pillar) as TextureImporter;if(pillar==null)throw new InvalidOperationException("Missing cave pillar sprite.");
            if(pillar.userData!="cave-pillar-v1")
            {pillar.textureType=TextureImporterType.Sprite;pillar.spriteImportMode=SpriteImportMode.Single;pillar.filterMode=FilterMode.Point;pillar.mipmapEnabled=false;pillar.textureCompression=TextureImporterCompression.Uncompressed;pillar.alphaIsTransparency=true;pillar.spritePixelsPerUnit=128;pillar.userData="cave-pillar-v1";pillar.SaveAndReimport();}
            appearance.cavePillar=AssetDatabase.LoadAssetAtPath<Sprite>(Pillar);EditorUtility.SetDirty(appearance);
        }
    }
}
