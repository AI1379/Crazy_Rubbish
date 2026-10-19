using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    // Solar grow lamps and the central thermal station (boiler + turbine + condenser).
    public sealed partial class PrototypeSimulation
    {
        public const double ThermalNetOutput=15,ThermalStartupSeconds=20,ThermalLoopWater=8,ThermalPipeLength=12;
        private IEnumerable<FacilityInstance> Built(string key)=>facilities.Where(f=>f.Built&&FacilityKey(f.Kind)==key);
        private static int FootprintGap(FacilityInstance a,FacilityInstance b)
        {
            var sa=Footprint(a);var sb=Footprint(b);
            int dx=Math.Max(0,Math.Max(a.X-(b.X+sb.width-1),b.X-(a.X+sa.width-1))),dy=Math.Max(0,Math.Max(a.Y-(b.Y+sb.height-1),b.Y-(a.Y+sa.height-1)));
            return Math.Max(dx,dy);
        }
        // Each lamp lights one growing bed within two cells; a bed uses its lowest-numbered lamp.
        public FacilityInstance GrowLampServing(FacilityInstance lamp)
        {
            var taken=new HashSet<int>();
            foreach(var l in Built("grow_lamp").OrderBy(l=>l.Id))
            {
                var bed=Built("growing").Where(g=>!taken.Contains(g.Id)&&FootprintGap(l,g)<=2).OrderBy(g=>FootprintGap(l,g)).ThenBy(g=>g.Id).FirstOrDefault();
                if(bed!=null)taken.Add(bed.Id);
                if(l.Id==lamp.Id)return bed;
            }
            return null;
        }
        private double GrowLampRatio(FacilityInstance bed)
        {
            var lamp=Built("grow_lamp").FirstOrDefault(l=>GrowLampServing(l)?.Id==bed.Id);
            return lamp==null?0:lamp.StepPowerRatio;
        }
        private bool LampDemand(FacilityInstance lamp)
        {
            var bed=GrowLampServing(lamp);if(bed==null||!bed.ProcessActive||bed.ProcessCompleted)return false;
            var r=DesignCatalog.Recipe(bed.ActiveProcess);return r!=null&&r.Stages[bed.ProcessStage].Lit;
        }

        // ---- thermal station -----------------------------------------------------------------
        private (FacilityInstance boiler,FacilityInstance condenser) ThermalParts(FacilityInstance turbine)
        {
            var used=new HashSet<int>();
            foreach(var t in Built("turbine").OrderBy(t=>t.Id))
            {
                var boiler=Built("boiler").Where(b=>!used.Contains(b.Id)&&FootprintGap(t,b)<=ThermalPipeLength).OrderBy(b=>FootprintGap(t,b)).ThenBy(b=>b.Id).FirstOrDefault();
                var condenser=Built("condenser").Where(c=>!used.Contains(c.Id)&&FootprintGap(t,c)<=ThermalPipeLength).OrderBy(c=>FootprintGap(t,c)).ThenBy(c=>c.Id).FirstOrDefault();
                if(boiler!=null)used.Add(boiler.Id);if(condenser!=null)used.Add(condenser.Id);
                if(t.Id==turbine.Id)return(boiler,condenser);
            }
            return(null,null);
        }
        private static bool Operational(FacilityInstance f)=>f!=null&&!f.MachineTripped&&f.Durability>0;
        private double BoilerFuelSeconds(FacilityInstance b)=>b.fuelRemaining+b.ProcessInput.Available(ItemId.WasteWood)*60+b.ProcessInput.Available(ItemId.WoodChips)*30;
        private bool ThermalReady(FacilityInstance turbine,out FacilityInstance boiler,out FacilityInstance condenser)
        {
            (boiler,condenser)=ThermalParts(turbine);
            if(!Operational(turbine)||!Operational(boiler)||!Operational(condenser)||BoilerFuelSeconds(boiler)<=0)return false;
            double water=boiler.WaterLoop+boiler.ProcessInput.Available(ItemId.DrinkingWater)+boiler.ProcessInput.Available(ItemId.IndustrialWater);
            // Startup fills the loop; normal evaporation must not immediately require another full fill.
            return turbine.ThermalRunning?water>1e-9:water>=ThermalLoopWater-1e-9;
        }
        private double ThermalPowerRequest(FacilityInstance turbine)
        {
            if(turbine.ThermalRunning||!turbine.ProcessRun||!ThermalReady(turbine,out _,out _))return 0;
            return 1; // start-up draws one unit from the public grid for 20 machine-seconds
        }
        private double ThermalOutput(FacilityInstance turbine)=>turbine.ThermalRunning&&ThermalReady(turbine,out _,out _)?ThermalNetOutput:0;
        private void TickThermal(double seconds)
        {
            foreach(var t in Built("turbine").ToArray())
            {
                if(!ThermalReady(t,out var boiler,out var condenser)||!t.ProcessRun){t.ThermalRunning=false;t.ThermalStartup=0;continue;}
                // Fill the closed water loop once, then top up 0.1 litre per minute while running.
                // Stock is in whole litres. Refill only when a litre fits in the eight-litre loop.
                while(boiler.WaterLoop<=ThermalLoopWater-1+1e-9)
                {
                    var water=boiler.ProcessInput.Available(ItemId.IndustrialWater)>0?ItemId.IndustrialWater:ItemId.DrinkingWater;
                    if(!boiler.ProcessInput.TryRemove(water,1))break;boiler.WaterLoop+=1;
                }
                if(!t.ThermalRunning)
                {
                    t.ThermalStartup+=seconds*t.StepPowerRatio;
                    if(t.ThermalStartup>=ThermalStartupSeconds-1e-6){t.ThermalRunning=true;t.ThermalStartup=ThermalStartupSeconds;}
                    continue;
                }
                var network=powerNetworks.FirstOrDefault(n=>n.FacilityIds.Contains(t.Id));
                if(network==null||network.Requested<=0)continue; // completely idle: no fuel burned
                double burn=seconds;
                while(burn>1e-9)
                {
                    if(boiler.fuelRemaining<=0)
                    {
                        if(boiler.ProcessInput.TryRemove(ItemId.WoodChips,1)){boiler.fuelRemaining=30;boiler.activeFuelHeatRate=0.5/30;}
                        else if(boiler.ProcessInput.TryRemove(ItemId.WasteWood,1)){boiler.fuelRemaining=60;boiler.activeFuelHeatRate=1.0/60;}
                        else{t.ThermalRunning=false;t.ThermalStartup=0;break;}
                    }
                    double used=Math.Min(burn,boiler.fuelRemaining);boiler.fuelRemaining-=used;burn-=used;boiler.equivalentHeat+=used*boiler.activeFuelHeatRate;
                    if(boiler.equivalentHeat>=20-1e-6&&boiler.ProcessOutput.TryAdd(ItemId.PowerAsh,1))boiler.equivalentHeat-=20;
                }
                boiler.WaterLoop=Math.Max(0,boiler.WaterLoop-seconds*0.1/60);
                HasBeenPowered=true;
                foreach(var part in new[]{t,boiler,condenser})
                {part.Durability=Math.Max(0,part.Durability-seconds*DesignBaseline.MachineDecay);if(part.Durability<=1e-8){part.Durability=0;part.MachineTripped=true;t.ThermalRunning=false;t.ThermalStartup=0;}}
            }
            foreach(var f in facilities.Where(f=>f.Built&&(FacilityKey(f.Kind)=="boiler"||FacilityKey(f.Kind)=="condenser"||FacilityKey(f.Kind)=="turbine")))
            {
                double rate=CrewEfficiency(f,true,1,out int present);
                if(present>0&&!RiotActive){double repair=Math.Min(DesignBaseline.MachineMaximum-f.Durability,seconds*DesignBaseline.MachineRepair*rate);f.Durability+=repair;f.RepairedDurability+=repair;}
                if(f.MachineTripped&&f.Durability>=DesignBaseline.MachineRestart-1e-8)f.MachineTripped=false;
            }
        }
        private string ThermalStatus(FacilityInstance f)
        {
            string key=FacilityKey(f.Kind);
            if(key=="boiler")return $"锅炉 · 燃料 {BoilerFuelSeconds(f):0}秒 · 循环水 {f.WaterLoop:0.#}/8升 · 耐久 {f.Durability:0.#}";
            if(key=="condenser")return $"冷凝 · 站内负载1 · 耐久 {f.Durability:0.#}";
            var parts=ThermalParts(f);
            if(parts.boiler==null||parts.condenser==null)return "汽轮机 · 12格内需各有一台锅炉和冷凝设备";
            if(!f.ProcessRun)return "汽轮机 · 已停机";
            if(f.ThermalRunning)return $"集中火电运行 · 净输出 {ThermalNetOutput:0} · 耐久 {f.Durability:0.#}";
            return ThermalReady(f,out _,out _)?$"启动中 {f.ThermalStartup:0.#}/20秒 · 需外部供电1":"缺燃料、循环水或维护，无法启动";
        }
        private DeliveryTask ThermalDelivery(Resident actor)
        {
            foreach(var b in Built("boiler"))
            {
                var side=ServiceCell(b);
                if(BoilerFuelSeconds(b)<60&&b.ProcessInput.FreeTicks>=20)
                    foreach(var fuel in new[]{ItemId.WoodChips,ItemId.WasteWood}){var task=Supply(actor,b.ProcessInput,ProcessInputKey(b.Id),fuel,side.x,side.y,Math.Min(8,b.ProcessInput.AvailableSlots(fuel)));if(task!=null)return task;}
                if(b.WaterLoop+b.ProcessInput.Count(ItemId.DrinkingWater)+b.ProcessInput.Count(ItemId.IndustrialWater)<ThermalLoopWater+1)
                    foreach(var water in new[]{ItemId.IndustrialWater,ItemId.DrinkingWater})
                    {
                        if(water==ItemId.DrinkingWater&&Warehouse.Available(water)<=DrinkingReserve)continue;
                        var task=Supply(actor,b.ProcessInput,ProcessInputKey(b.Id),water,side.x,side.y,4);if(task!=null)return task;
                    }
            }
            return null;
        }
        public ActionResult ToggleThermal(int id)
        {
            var f=GetFacility(id);if(f==null||!f.Built||FacilityKey(f.Kind)!="turbine")return Fail("请选择汽轮发电机。");
            f.ProcessRun=!f.ProcessRun;if(!f.ProcessRun){f.ThermalRunning=false;f.ThermalStartup=0;}
            RecomputePower();return Ok(f.ProcessRun?"已请求启动：需外部电网供电1，持续20秒。":"集中火电已停机，未用燃料保留。");
        }
    }
}
