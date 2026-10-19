using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        public const double RiverMaximumLiters=120,RiverRechargePerMinute=60;
        public static bool IsWaterIntake(string type)=>type=="water_intake"||type=="auto_water_plant";
        private void RestoreWaterSources()
        {foreach(var source in State.springs.Where(s=>s.kind!="surface"&&s.region==1))source.body=PlanTerrain.StarterPondBody;}
        public PlanSpringState WaterSource(PlanSite site)
        {return site.springBody>0?State.springs.FirstOrDefault(s=>s.body==site.springBody):State.springs.FirstOrDefault(s=>s.region==site.springRegion&&s.site==site.id);}
        private PlanSpringState LegacyAnchor(PlanSite site)
        {
            return State.springs.FirstOrDefault(s=>s.kind!="surface"&&
            (site.type=="water_intake"?Config.Region(s.region).freshwater_source.manual_anchor:Config.Region(s.region).freshwater_source.auto_anchor)[0]==site.x&&
            (site.type=="water_intake"?Config.Region(s.region).freshwater_source.manual_anchor:Config.Region(s.region).freshwater_source.auto_anchor)[1]==site.y);
        }
        private (int x,int y,int body)? WaterContact(PlanSite site)
        {
            var size=Size(site);
            for(int y=site.y-1;y<=site.y+size.height;y++)for(int x=site.x-1;x<=site.x+size.width;x++)
            {
                // Only an actual shared bank edge counts; diagonals do not provide water access.
                bool edge=(x==site.x-1||x==site.x+size.width)&&y>=site.y&&y<site.y+size.height||
                    (y==site.y-1||y==site.y+size.height)&&x>=site.x&&x<site.x+size.width;
                int body=PlanTerrain.WaterBodyAt(x,y),region=PlanTerrain.ChamberAt(x,y);
                if(edge&&body>0&&region>0&&region<=State.region)return(x,y,body);
            }
            return null;
        }
        public bool CanPlaceWaterIntake(PlanSite candidate)
        {
            var legacy=LegacyAnchor(candidate);var contact=WaterContact(candidate);
            if(legacy==null&&contact==null)return false;
            var source=legacy??State.springs.FirstOrDefault(s=>s.body==contact.Value.body);
            return source==null||source.kind=="surface"||source.site==0||source.site==candidate.id;
        }
        private void BindWaterSource(PlanSite site)
        {
            var legacy=LegacyAnchor(site);var contact=WaterContact(site);
            Require(legacy!=null||contact!=null,"采水设施须放在水塘或河流岸边，至少一边与水面相邻。");
            var source=legacy??State.springs.FirstOrDefault(s=>s.body==contact.Value.body);
            if(source==null)
            {
                source=new PlanSpringState{kind="surface",body=contact.Value.body,region=PlanTerrain.ChamberAt(site.x,site.y),remaining=RiverMaximumLiters};State.springs.Add(source);
            }
            Require(source.kind=="surface"||source.site==0,"这处小水源已有取水设施，请先拆除原设备再更换。");
            if(source.kind!="surface")source.site=site.id;
            site.springRegion=source.region;site.springBody=source.body;
        }
        private double WaterMaximum(PlanSpringState source)=>source.kind=="surface"?RiverMaximumLiters:Config.Region(source.region).freshwater_source.maximum_liters;
        private double WaterRecharge(PlanSpringState source)=>source.kind=="surface"?RiverRechargePerMinute:Config.Region(source.region).freshwater_source.recharge_liters_per_minute;
        public string WaterSourceDescription(PlanSite site)
        {
            var source=WaterSource(site);if(source==null)return "尚未连接水源";
            return (source.kind=="surface"?"河湖共用取水预算":"小水源取水预算")+" "+Math.Max(0,source.remaining-source.reserved).ToString("0.#")+" / "+WaterMaximum(source).ToString("0.#")+" 升，补水 "+WaterRecharge(source).ToString("0.#")+" 升/分";
        }
    }
}
