using System;

namespace CrazyRubbish.Core
{
    [Serializable]
    public sealed class PrototypeSettings
    {
        public int mapWidth = 20;
        public int mapHeight = 14;
        public int carryCapacity = 12;
        public int warehouseCapacity = 40;
        public int outputCapacity = 4;
        public int pileCapacity = 5;
        public float moveSpeed = 4.5f;
        public float interactionRadius = 1.7f;
        public int benchWoodCost = 4;
        public int benchFastenerCost = 2;
        public int recipeWoodInput = 2;
        public int plankOutput = 1;
        public int handleOutput = 2;
        public float plankSeconds = 4f;
        public float handleSeconds = 5f;
        public int plankPrice = 5;
        public int handlePrice = 3;
        public float pressureRadius = 5f;
        public int pressureTilesForMaximum = 12;
        public float minimumEfficiency = 0.35f;
        public float firstInputSeconds = 22f;
        public float inputGrowthSeconds = 90f;
        public int initialWaveUnits = 3;
        public bool automaticInput = true;

        public void Validate()
        {
            if (mapWidth < 18 || mapHeight < 12 || mapWidth > 128 || mapHeight > 128)
                throw new ArgumentException("原型地图尺寸需在 18×12 至 128×128 之间。");
            int[] positive = { carryCapacity, warehouseCapacity, outputCapacity, pileCapacity,
                benchWoodCost, benchFastenerCost, recipeWoodInput, plankOutput, handleOutput,
                plankPrice, handlePrice, pressureTilesForMaximum, initialWaveUnits };
            foreach (int value in positive)
                if (value <= 0 || value > 100000) throw new ArgumentException("实验数量必须为正数。");
            float[] times = { moveSpeed, interactionRadius, plankSeconds, handleSeconds,
                pressureRadius, firstInputSeconds, inputGrowthSeconds };
            foreach (float value in times)
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                    throw new ArgumentException("实验时间、速度与范围必须为有限正数。");
            if (float.IsNaN(minimumEfficiency) || minimumEfficiency <= 0 || minimumEfficiency > 1)
                throw new ArgumentException("最低效率需在 0 至 1 之间。");
            if (Math.Max(plankOutput, handleOutput) > outputCapacity)
                throw new ArgumentException("设备内部容量不能小于单批产量。");
            if ((long)benchWoodCost + benchFastenerCost > warehouseCapacity)
                throw new ArgumentException("仓库容量不能小于建造所需材料总量。");
        }
    }
}
