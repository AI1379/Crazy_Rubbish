using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed class PrototypeSaveEntry
    {
        public string Path, Description;
        public DateTime SavedAt;
        public bool Valid;
    }

    public sealed partial class PrototypeGame
    {
        public PrototypeSaveEntry[] ListSavedGames()
        {
            if(!Directory.Exists(SaveRoot))return Array.Empty<PrototypeSaveEntry>();
            return Directory.GetFiles(SaveRoot,"*.json",SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).Select(path=>
                {
                    var entry=new PrototypeSaveEntry{Path=path,SavedAt=File.GetLastWriteTime(path)};
                    try
                    {
                        var snapshot=JsonUtility.FromJson<SimulationSnapshot>(File.ReadAllText(path));
                        entry.Valid=snapshot!=null&&snapshot.schema>=2&&snapshot.schema<=5&&snapshot.slot!=null&&System.Text.RegularExpressions.Regex.IsMatch(snapshot.slot,@"^[a-f0-9]{32}$")&&snapshot.community?.failed!=true;
                        string type=System.IO.Path.GetFileName(path)=="manual.json"?"手动":"自动";
                        entry.Description=$"{type} · {entry.SavedAt:MM-dd HH:mm:ss} · 游戏 {(int)snapshot.seconds/60:00}:{(int)snapshot.seconds%60:00}\n现金 {snapshot.cash/100m:0.00} · 居民 {snapshot.residents?.Count??0} · 教程 {Math.Min(snapshot.tutorial?.step??0,TutorialProgress.LastStep-1)}/12";
                        if(!entry.Valid)entry.Description="不可用存档 · "+entry.SavedAt.ToString("MM-dd HH:mm:ss");
                    }
                    catch{entry.Valid=false;entry.Description="无法读取 · "+entry.SavedAt.ToString("MM-dd HH:mm:ss");}
                    return entry;
                }).ToArray();
        }

        private void RestoreSaveFile(string path)
        {
            string fullPath=System.IO.Path.GetFullPath(path);
            string rootPath=System.IO.Path.GetFullPath(SaveRoot).TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
            if(!fullPath.StartsWith(rootPath,StringComparison.OrdinalIgnoreCase)||!string.Equals(System.IO.Path.GetExtension(fullPath),".json",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请选择本游戏存档目录中的存档。");
            var snapshot=JsonUtility.FromJson<SimulationSnapshot>(File.ReadAllText(fullPath));
            if(snapshot?.slot==null||!System.Text.RegularExpressions.Regex.IsMatch(snapshot.slot,@"^[a-f0-9]{32}$"))throw new InvalidOperationException("存档槽标识无效。");
            var restored=PrototypeSimulation.Restore(snapshot);
            if(restored.Failed)throw new InvalidOperationException("请选择经营失败之前的存档。");
            Model=restored;saveSlot=snapshot.slot;autoIndex=snapshot.autoIndex;
            walkPath=null;SessionStarted=true;MenuOpen=false;ResultVisible=false;resultSeen=Model.DemoCompleted;
            interactionRevision=Model.InteractionRevision;GetComponent<PrototypeHud>().ResetForSession();
        }

        public void LoadSavedGame(string path)
        {
            try{RestoreSaveFile(path);Report(new ActionResult(true,"已恢复所选存档，进度、工资与在途物资保留。"));}
            catch(Exception error){Report(new ActionResult(false,"读取失败："+error.Message));}
        }
    }
}
