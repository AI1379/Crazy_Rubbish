using System;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeWorld
    {
        private string hoverKey;
        private double hoverSince;
        private SpriteRenderer[] overlayLines = Array.Empty<SpriteRenderer>();
        public const double HoverDelay = 0.4;

        private static bool Covers(SpriteRenderer renderer, Vector2 point)
        {
            if (renderer == null) return false;
            Bounds bounds = renderer.bounds;
            return point.x >= bounds.min.x && point.x <= bounds.max.x && point.y >= bounds.min.y && point.y <= bounds.max.y;
        }

        public int FacilityAt(Vector2 point)
        {
            if (Covers(bench, point)) return 1;
            if (Covers(dismantle, point)) return 2;
            if (Covers(table, point)) return 3;
            if (Covers(generator, point)) return 4;
            return 0;
        }

        private bool CoversObject(string name, Vector2 point) => Covers(visualRoot.Find(name)?.GetComponent<SpriteRenderer>(), point);

        public void HandleMapClick(Vector2 point, bool moveOnly)
        {
            if (game.MenuOpen || game.ResultVisible) return;
            var hud = game.GetComponent<PrototypeHud>();
            int facility = FacilityAt(point);
            if (!moveOnly)
            {
                if (facility > 0) { hud.ShowFacility(facility); return; }
                if (CoversObject("Warehouse", point)) { hud.OpenManagement(2); return; }
                if (CoversObject("OldHousing", point) || game.Model.Residents.Any(r => Vector2.Distance(new Vector2((float)r.X, (float)r.Y + 0.5f), point) < 0.65f))
                { hud.OpenManagement(1); return; }
                if (hud.SelectedFacility != 0) hud.ClosePanels();
            }
            int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
            switch (facility)
            {
                case 1: x = PrototypeSimulation.BenchX; y = PrototypeSimulation.BenchY; break;
                case 2: x = PrototypeSimulation.DismantleX; y = PrototypeSimulation.DismantleY; break;
                case 3: x = PrototypeSimulation.TableX; y = PrototypeSimulation.TableY; break;
                case 4: x = PrototypeSimulation.GeneratorX; y = PrototypeSimulation.GeneratorY; break;
            }
            if (CoversObject("Warehouse", point)) { x = PrototypeSimulation.WarehouseX; y = PrototypeSimulation.WarehouseY; }
            game.GoTo(x, y);
        }

        // Unscaled time keeps inspection available while paused. A new target restarts the delay.
        public void UpdateHover(Vector2? point, double now)
        {
            var hud = game.GetComponent<PrototypeHud>();
            if (point.HasValue && hud.SelectedFacility > 0 && FacilityAt(point.Value) == hud.SelectedFacility)
            { hoverKey = null; hud.ShowInspection(null, null, Vector2.zero); return; }
            if (!point.HasValue || game.MenuOpen || game.ResultVisible || !Describe(point.Value, out string key, out string title, out string body))
            { hoverKey = null; hud.ShowInspection(null, null, Vector2.zero); return; }
            if (hoverKey != key)
            { hoverKey = key; hoverSince = now; hud.ShowInspection(null, null, Vector2.zero); }
            if (now - hoverSince < HoverDelay) return;
            hud.ShowInspection(title, body, worldCamera.WorldToScreenPoint(point.Value));
        }

        public void InspectAt(Vector2? point)
        {
            var hud = game.GetComponent<PrototypeHud>();
            if (!point.HasValue || game.MenuOpen || game.ResultVisible || !Describe(point.Value, out _, out string title, out string body))
            { hud.ShowInspection(null, null, Vector2.zero); return; }
            hud.ShowInspection(title, body, worldCamera.WorldToScreenPoint(point.Value));
        }

        private bool Describe(Vector2 point, out string key, out string title, out string body)
        {
            var m = game.Model;
            key = title = body = null;
            if (m == null) return false;
            var resident = m.Residents.FirstOrDefault(r => Vector2.Distance(new Vector2((float)r.X, (float)r.Y + 0.5f), point) < 0.6f);
            if (resident != null)
            {
                key = "resident-" + resident.Id; title = "居民 " + resident.Id.ToString("00");
                body = $"{resident.Activity}\n钱包 {resident.Cash:0.00} · 左键查看人员"; return true;
            }
            if (Vector2.Distance(new Vector2((float)m.PlayerX, (float)m.PlayerY + 0.5f), point) < 0.5f)
            {
                key = "player"; title = "主角"; body = $"背包 {m.Carry.Used:0.#} / {m.Carry.Capacity}\n按 I 查看物资"; return true;
            }
            int facility = FacilityAt(point);
            if (facility > 0)
            {
                key = "facility-" + facility;
                switch (facility)
                {
                    case 1: title = "木工台"; body = m.BenchBuilt ? $"{PrototypeGame.StateName(m.State)}\n木料 {m.WoodInputStock.Count(ItemId.WasteWood)} · 进度 {m.Progress:P0}" : "待建设 · 木料 4 + 紧固件 2"; break;
                    case 2: title = "拆解地点"; body = m.DismantleBuilt ? PrototypeGame.StateName(m.DismantleState) + "\n" + ItemCatalog.Name(m.SelectedDevice) : "待建设 · 木料 4 + 紧固件 2"; break;
                    case 3: title = "工作台"; body = m.TableBuilt ? $"{ItemCatalog.Name(m.AssemblyProduct)} · {m.AssemblyProgress:P0}" : "待建设 · 木板 4 + 钢铁零件 2"; break;
                    default: title = "发电机"; body = m.GeneratorBuilt ? $"电力 {m.AvailablePower:0.#} · 燃料 {m.FuelCount}" : "待组装 · 左键查看所需材料"; break;
                }
                body += "\n左键查看 · 右键前往"; return true;
            }
            if (CoversObject("Warehouse", point))
            { key = "warehouse"; title = "仓库"; body = $"容量 {m.Warehouse.Used:0.#} / {m.Warehouse.Capacity}\n左键查看物资 · 到场按 Q 入库"; return true; }
            if (CoversObject("OldHousing", point))
            { key = "housing"; title = "旧住房"; body = $"已招募居民 {m.Residents.Count} 人\n左键查看人员与生活供应"; return true; }
            if (CoversObject("Hole", point))
            { key = "hole"; title = "洞"; body = "深不见底，去向不明。"; return true; }
            if (CoversObject("CommunityLamp", point))
            { key = "lamp"; title = "社区路灯"; body = "社区的照明设施。"; return true; }
            int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
            var content = m.Ground.Where(p => y == p.Y && x >= p.X && x < p.X + p.Width).GroupBy(p => p.Item).OrderBy(g => g.Key).ToArray();
            if (content.Length > 0)
            {
                key = $"ground-{x}-{y}"; title = content.Length == 1 ? ItemCatalog.Name(content[0].Key) : "混合物资";
                body = string.Join("\n", content.Take(3).Select(g => ItemCatalog.Name(g.Key) + " ×" + g.Sum(p => p.Amount) +
                    (g.Sum(p => p.Reserved) > 0 ? " · 已预约 " + g.Sum(p => p.Reserved) : "") +
                    ((ItemCatalog.IsTool(g.Key) || ItemCatalog.IsDevice(g.Key) || ItemCatalog.IsOpenable(g.Key)) && g.Any(p => !p.Usable) ? " · 含损坏物品" : "")));
                if (content.Length > 3) body += $"\n另有 {content.Length - 3} 类物品";
                body += "\n走近按 E 拾取 · Shift+E 整堆"; return true;
            }
            if (game.GetComponent<PrototypeHud>().ZonesVisible)
            {
                if (x >= 10 && x <= 27 && y >= 6 && y <= 23)
                { key = "building-area"; title = "建设区"; body = "18 × 18 格 · 从建造面板安排设施"; return true; }
                if (x >= 0 && x <= 7 && y >= 6 && y <= 11)
                { key = "receiving-area"; title = "来料区"; body = "8 × 6 格 · 在这里收集回收物资"; return true; }
            }
            return false;
        }

        private void RefreshMapOverlays()
        {
            var hud = game.GetComponent<PrototypeHud>();
            bool zones = hud != null && hud.ZonesVisible && !game.MenuOpen && !game.ResultVisible;
            int selected = hud != null ? hud.SelectedFacility : 0;
            foreach (var line in overlayLines)
            {
                bool station = selected == 1 && line.name.StartsWith("Woodwork Pad") || selected == 2 && line.name.StartsWith("Dismantle Pad") ||
                    selected == 3 && line.name.StartsWith("Workbench Pad") || selected == 4 && line.name.StartsWith("Generator Pad");
                line.enabled = zones || station && !game.MenuOpen && !game.ResultVisible;
            }
        }
    }
}
