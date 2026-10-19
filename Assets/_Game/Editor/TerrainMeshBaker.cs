using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Editor
{
    // Persist chunk geometry for a useful saved scene preview; runtime regenerates the same deterministic map.
    public static class TerrainMeshBaker
    {
        private const string Folder="Assets/_Game/Art/TerrainGenerated";
        private static string AssetPath(string name)=>Folder+"/"+Regex.Replace(name,"[^A-Za-z0-9_-]","_")+".asset";
        public static void Save(GameObject game)
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var materialMap=new Dictionary<Material,Material>();var obsolete=new List<Object>();
            foreach(var filter in game.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;if(mesh==null)continue;string path=AssetPath(mesh.name);mesh.hideFlags=HideFlags.None;var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null){AssetDatabase.CreateAsset(mesh,path);existing=mesh;}else if(existing!=mesh){EditorUtility.CopySerialized(mesh,existing);obsolete.Add(mesh);}filter.sharedMesh=existing;EditorUtility.SetDirty(existing);
                var renderer=filter.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;if(!materialMap.TryGetValue(material,out var saved))
                {string materialPath=AssetPath(material.name);material.hideFlags=HideFlags.None;saved=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(saved==null){AssetDatabase.CreateAsset(material,materialPath);saved=material;}else if(saved!=material){EditorUtility.CopySerialized(material,saved);obsolete.Add(material);}materialMap[material]=saved;EditorUtility.SetDirty(saved);}renderer.sharedMaterial=saved;
            }
            foreach(var image in game.GetComponentsInChildren<RawImage>(true))if(image.texture is Texture2D texture&&texture.name=="Cave minimap")
            {string path=AssetPath("Cave minimap");texture.hideFlags=HideFlags.None;var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(saved==null)AssetDatabase.CreateAsset(texture,path);else if(saved!=texture){EditorUtility.CopySerialized(texture,saved);image.texture=saved;obsolete.Add(texture);EditorUtility.SetDirty(saved);}}
            foreach(var item in obsolete)Object.DestroyImmediate(item);AssetDatabase.SaveAssets();
        }
    }
}
