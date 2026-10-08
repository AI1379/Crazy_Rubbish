using System;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    [DefaultExecutionOrder(-100)]
    public sealed class PrototypeGame : MonoBehaviour
    {
        public PrototypeBalance balance;
        public PrototypeAppearance appearance;
        public PrototypeSimulation Model { get; private set; }
        public string Notice { get; private set; } = "先拾取手锯，再把木料和连接件送进仓库。";
        public bool LastActionSucceeded { get; private set; } = true;
        public bool SmokeMode { get; private set; }
        private string sessionBalance;

        private void Awake()
        {
            if (balance == null || appearance == null || appearance.font == null)
                throw new InvalidOperationException("原型缺少配置或字体，请先运行 Setup。");
            SmokeMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-prototypeSmoke") >= 0;
            sessionBalance = JsonUtility.ToJson(balance.values);
            ResetGame();
        }

        public void ResetGame()
        {
            // A fresh copy prevents play sessions and tests from editing the configuration asset.
            var settings = JsonUtility.FromJson<PrototypeSettings>(sessionBalance);
            Model = new PrototypeSimulation(settings, SmokeMode ? 4271 : Environment.TickCount);
            Notice = "先拾取手锯，再把木料和连接件送进仓库。";
            LastActionSucceeded = true;
        }

        private void Update()
        {
            if (SmokeMode) return;
            double delta = Math.Min(Time.unscaledDeltaTime, 0.25f);
            if (Input.GetKeyDown(KeyCode.Space)) Model.SetPaused(!Model.Paused);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Model.SetSpeed(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Model.SetSpeed(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Model.SetSpeed(4);
            double x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            double y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            Model.MovePlayer(x, y, delta);
            if (Input.GetKeyDown(KeyCode.E)) Report(Model.PickUp(
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? int.MaxValue : 1));
            if (Input.GetKeyDown(KeyCode.Q)) Report(Model.Deposit());
            if (Input.GetKeyDown(KeyCode.F)) Report(Model.ToggleWork());
            Model.Advance(delta);
        }

        public void Report(ActionResult result)
        { Notice = result.Message; LastActionSucceeded = result.Success; }

        public void SellAll()
        {
            int quantity = Model.Warehouse.Count(ItemId.Plank) + Model.Warehouse.Count(ItemId.WoodHandle);
            if (quantity == 0) { Report(new ActionResult(false, "仓库没有可出售的产品。")); return; }
            decimal previous = Model.Money;
            Model.Sell(ItemId.Plank, int.MaxValue);
            Model.Sell(ItemId.WoodHandle, int.MaxValue);
            Report(new ActionResult(true, $"出售 {quantity} 件产品，获得 {Model.Money - previous:0.00} 金钱。"));
        }

        public static string ItemName(ItemId item)
        {
            switch (item)
            {
                case ItemId.WasteWood: return "废木料";
                case ItemId.MixedPlastic: return "混杂塑料";
                case ItemId.RecoveredFastener: return "连接件";
                case ItemId.WoodHandSaw: return "木柄手锯";
                case ItemId.Plank: return "木板";
                case ItemId.WoodHandle: return "木柄";
                case ItemId.WoodChips: return "木料碎屑";
                case ItemId.SteelPart: return "钢铁零件";
                case ItemId.CopperPart: return "铜零件";
                case ItemId.RecoveredWire: return "回收电线";
                case ItemId.ThermalModule: return "热动力组件";
                case ItemId.GeneratorModule: return "发电组件";
                case ItemId.Screwdriver: return "螺丝刀";
                case ItemId.Wrench: return "扳手";
                case ItemId.Hammer: return "锤子";
                case ItemId.Scissors: return "剪刀";
                case ItemId.SewingKit: return "针线工具";
                case ItemId.Food: return "食物";
                case ItemId.DrinkingWater: return "饮用水";
                case ItemId.Soap: return "肥皂";
                case ItemId.WasteTextile: return "废纺织物";
                case ItemId.OldClothes: return "旧衣服";
                case ItemId.DamagedMotor: return "损坏电机";
                case ItemId.SteelMaterial: return "钢铁材料";
                case ItemId.CopperMaterial: return "铜材料";
                case ItemId.PowerAsh: return "发电灰渣";
                default: return item.ToString();
            }
        }

        public static string StateName(WorkState state)
        {
            switch (state)
            {
                case WorkState.NotBuilt: return "等待搭建";
                case WorkState.Idle: return "已停工 · 进度保留";
                case WorkState.Paused: return "游戏暂停";
                case WorkState.NeedsOperator: return "等待操作人员到岗";
                case WorkState.NeedsTool: return "缺少对应工具";
                case WorkState.NeedsMaterials: return "缺少原料 · 请搬进入库";
                case WorkState.OutputBlocked: return "输出堵塞 · 仓库或内部容量已满";
                default: return "正在手工锯切";
            }
        }
    }
}
