using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        // Only invoked by the explicit smoke-test argument; ordinary games never load this file.
        private IEnumerator SmokeBalanceCheckpoint(string path,string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("经营续档："+message);}
            var snapshot=JsonUtility.FromJson<PlanState>(File.ReadAllText(path));
            Check(snapshot!=null&&snapshot.seconds>3600&&snapshot.region==2,"需要实际经营一小时以上、已开二区的记录");
            Check(string.IsNullOrEmpty(snapshot.ending),"记录已结束");
            Model=new PlanSimulation(LoadConfig(),1,snapshot);Model.SetPaused(true);MenuOpen=false;session=true;slot=Guid.NewGuid().ToString("N");
            buildType=null;movingSite=0;moveX=-1;selectedSite=0;BuildPresentation();RefreshPresentation(true);
            long cash=Model.State.cash;int people=Model.Alive.Count();double elapsed=Model.State.seconds;
            Check(Model.State.sites.Any(s=>s.type=="growing")&&Model.State.sites.Any(s=>s.type=="water_purifier"),"食水产线丢失");
            string before=JsonUtility.ToJson(Model.Capture());Check(TrySave(),"实际经营记录无法保存");string disk=ManualPath;
            Check(TryLoadFile(disk),"实际经营记录无法读取");
            Check(Model.State.cash==cash&&Model.Alive.Count()==people&&Math.Abs(Model.State.seconds-elapsed)<.001,"资金人口或时间变化");
            string after=JsonUtility.ToJson(Model.Capture());
            File.WriteAllText(Path.Combine(SaveRoot,"balance-before.capture"),before);
            File.WriteAllText(Path.Combine(SaveRoot,"balance-after.capture"),after);
            // Compare both serialized forms: Unity writes null inline records as empty records.
            CheckBalanceFields(JsonUtility.FromJson<PlanState>(before),JsonUtility.FromJson<PlanState>(after),"state");
            mapCamera.transform.position=new Vector3(17,13,-10);mapCamera.orthographicSize=17;RefreshPresentation(true);yield return null;
            if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-balance-checkpoint.png"));
            Model.SetPaused(false);Model.Advance(5);Model.SetPaused(true);
            Check(Model.State.seconds>elapsed&&string.IsNullOrEmpty(Model.State.ending)&&Model.Alive.Count()==people,"续档后无法继续经营");
            Debug.Log("Real economy checkpoint roundtrip passed at "+elapsed+" seconds, cash "+cash+", workers "+people);
        }
        private static void CheckBalanceFields(object expected,object actual,string path)
        {
            if(expected==null||actual==null){if(expected!=actual)throw new InvalidOperationException("经营续档字段缺失："+path);return;}
            Type type=expected.GetType();if(type!=actual.GetType())throw new InvalidOperationException("经营续档字段类型："+path);
            // JsonUtility can round a double by one ULP. Quantities, money and identifiers are integers and stay exact.
            if(type==typeof(double)||type==typeof(float))
            {
                double a=Convert.ToDouble(expected),b=Convert.ToDouble(actual);
                if(double.IsNaN(a)||double.IsNaN(b)||Math.Abs(a-b)>Math.Max(1,Math.Max(Math.Abs(a),Math.Abs(b)))*1e-10)
                    throw new InvalidOperationException("经营续档浮点差异："+path);
                return;
            }
            if(type.IsPrimitive||type==typeof(string)||type.IsEnum){if(!expected.Equals(actual))throw new InvalidOperationException("经营续档字段变化："+path);return;}
            if(expected is IEnumerable)
            {
                var a=((IEnumerable)expected).Cast<object>().ToArray();var b=((IEnumerable)actual).Cast<object>().ToArray();
                if(a.Length!=b.Length)throw new InvalidOperationException("经营续档列表长度："+path);
                for(int i=0;i<a.Length;i++)CheckBalanceFields(a[i],b[i],path+"["+i+"]");return;
            }
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public))CheckBalanceFields(field.GetValue(expected),field.GetValue(actual),path+"."+field.Name);
        }
    }
}
