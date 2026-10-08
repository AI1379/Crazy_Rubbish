using System;

namespace CrazyRubbish.Core
{
    [Serializable]
    public sealed class PrototypeSettings
    {
        public int mapWidth = 30, mapHeight = 24;
        public int carryCapacity = 8, warehouseCapacity = 80, outputCapacity = 8, pileCapacity = 5;
        public float moveSpeed = 4.5f, residentMoveSpeed = 0.8f, interactionRadius = 1.7f;
        public int benchWoodCost = 4, benchFastenerCost = 2;
        public int recipeWoodInput = 4, handleWoodInput = 2, plankOutput = 3, handleOutput = 2, chipsOutput = 1;
        public float plankSeconds = 40f, handleSeconds = 30f;
        public int plankPrice = 3, handlePrice = 2;
        public float pressureRadius = 4f, minimumEfficiency = 0.25f;
        public float firstInputSeconds = 60f;
        public bool automaticInput = true;
        public int initialWood = 80, initialFasteners = 6;
        public int firstResidents = 5, housingCapacity = 6;
        public float wageSeconds = 600f, wagePerResident = 10f;
        public float woodFuelSeconds = 120f, chipsFuelSeconds = 60f;
        public float generatorOutput = 4f;

        public void Validate()
        {
            if (mapWidth < 30 || mapHeight < 24 || mapWidth > 128 || mapHeight > 128)
                throw new ArgumentException("起步白盒地图至少30×24，以容纳324格可建地及独立垃圾山。");
            int[] values = { carryCapacity, warehouseCapacity, outputCapacity, pileCapacity, benchWoodCost,
                benchFastenerCost, recipeWoodInput, handleWoodInput, plankOutput, handleOutput, chipsOutput,
                plankPrice, handlePrice, initialWood, initialFasteners, firstResidents, housingCapacity };
            foreach (int value in values) if (value <= 0 || value > 100000) throw new ArgumentException("配置数量必须为正数。");
            float[] times = { moveSpeed, residentMoveSpeed, interactionRadius, plankSeconds, handleSeconds, pressureRadius,
                firstInputSeconds, wageSeconds, wagePerResident, woodFuelSeconds, chipsFuelSeconds, generatorOutput };
            foreach (float value in times)
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) throw new ArgumentException("时间和速度必须为有限正数。");
            if (float.IsNaN(minimumEfficiency) || minimumEfficiency <= 0 || minimumEfficiency > 1)
                throw new ArgumentException("人工最低倍率必须在0到1之间。");
            if (plankOutput + chipsOutput > outputCapacity || handleOutput * 0.5 + chipsOutput > outputCapacity)
                throw new ArgumentException("设备缓冲不能小于一批全部产物。");
            if (benchWoodCost + benchFastenerCost * 0.5 > warehouseCapacity)
                throw new ArgumentException("仓库容量不能小于建设投入。");
            if (firstResidents > housingCapacity - 1 || firstResidents > 5)
                throw new ArgumentException("首批人数需满足住房容量及最多5人规则。");
        }
    }
}
