using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KenshiPlanet.Factions;
using KenshiPlanet.Utils;
using KenshiPlanet.Core;

namespace KenshiPlanet.World
{
    /// <summary>
    /// Управляет территориями фракций.
    /// Получает готовые полигоны от WorldManager (территории уже построены через алгоритм Ллойда).
    /// Отвечает за рендер карты (стиль SAMP-RP гетто) и пространственные запросы.
    /// </summary>
    public class TerritoryManager
    {
        private readonly Dictionary<int, Faction>       _factions;
        private readonly Dictionary<int, List<Vector2>> _territories;
        private readonly List<TerritoryBorder>          _borders = new();

        public MapProjection? Projection  { get; private set; }
        public Dictionary<int, List<Vector2>> Territories => _territories;

        // ================================================================
        //  КОНСТРУКТОР
        // ================================================================
        public TerritoryManager(
            Dictionary<int, Faction>       factions,
            Dictionary<int, List<Vector2>> territories)
        {
            _factions    = factions;
            _territories = territories;

            float minX = -WorldManager.WORLD_RADIUS;
            float maxX =  WorldManager.WORLD_RADIUS;
            float minY = -WorldManager.WORLD_RADIUS;
            float maxY =  WorldManager.WORLD_RADIUS;

            Projection = new MapProjection(minX, maxX, minY, maxY);
            BuildBorders();
        }

        // ================================================================
        //  ПОСТРОЕНИЕ ГРАНИЦ между соседними фракциями
        // ================================================================
        private void BuildBorders()
        {
            _borders.Clear();
            var ids = _territories.Keys.ToList();
            const float EPS = 12f;

            for (int a = 0; a < ids.Count; a++)
            {
                for (int b = a + 1; b < ids.Count; b++)
                {
                    int idA = ids[a], idB = ids[b];
                    if (!_factions.TryGetValue(idA, out var fA)) continue;
                    if (!_factions.TryGetValue(idB, out var fB)) continue;

                    var edge = FindSharedEdge(_territories[idA], _territories[idB], EPS);
                    if (edge.HasValue)
                        _borders.Add(new TerritoryBorder(fA, fB, edge.Value.s, edge.Value.e));
                }
            }
        }

        private static (Vector2 s, Vector2 e)? FindSharedEdge(
            List<Vector2> pA, List<Vector2> pB, float eps)
        {
            for (int i = 0; i < pA.Count; i++)
            {
                Vector2 a1 = pA[i], a2 = pA[(i + 1) % pA.Count];
                for (int j = 0; j < pB.Count; j++)
                {
                    Vector2 b1 = pB[j], b2 = pB[(j + 1) % pB.Count];
                    bool fwd = Vector2.Distance(a1, b1) < eps && Vector2.Distance(a2, b2) < eps;
                    bool rev = Vector2.Distance(a1, b2) < eps && Vector2.Distance(a2, b1) < eps;
                    if (fwd || rev) return (a1, a2);
                }
            }
            return null;
        }

        // ================================================================
        //  ПРОСТРАНСТВЕННЫЕ ЗАПРОСЫ
        // ================================================================
        public List<Vector2>? GetFactionPolygon(int factionId) =>
            _territories.TryGetValue(factionId, out var p) ? p : null;

        public bool IsPointInPolygon(Vector2 point, List<Vector2> polygon) =>
            WorldManager.IsPointInPolygon(point, polygon);

        public bool IsPointInFactionTerritory(Vector2 point, int factionId)
        {
            var poly = GetFactionPolygon(factionId);
            return poly != null && IsPointInPolygon(point, poly);
        }

        public bool IsSettlementInTerritory(Settlement settlement, Faction faction)
        {
            var poly = GetFactionPolygon(faction.Id);
            return poly != null && poly.Count >= 3 && IsPointInPolygon(settlement.Position, poly);
        }

        public int GetFactionAtPosition(Vector2 pos)
        {
            foreach (var kv in _territories)
                if (IsPointInPolygon(pos, kv.Value))
                    return kv.Key;

            int   best     = -1;
            float bestDist = float.MaxValue;
            foreach (var f in _factions.Values)
            {
                float d = Vector2.DistanceSquared(pos, f.CapitalPosition);
                if (d < bestDist) { bestDist = d; best = f.Id; }
            }
            return best;
        }

        public bool IsTerritoryBorderCrossing(
            Vector2 from, Vector2 to, out int fromFaction, out int toFaction)
        {
            fromFaction = GetFactionAtPosition(from);
            toFaction   = GetFactionAtPosition(to);
            return fromFaction != toFaction;
        }

        // ================================================================
        //  РЕНДЕР В ИГРОВОМ ВИДЕ (границы территорий рядом с игроком)
        // ================================================================
        public void Render(Vector2 cameraPos, float radius)
        {
            foreach (var b in _borders)
                if (Vector2.Distance(cameraPos, b.Midpoint) < radius * 2)
                    b.Render();
        }

        // ================================================================
        //  ГЛОБАЛЬНАЯ КАРТА
        // ================================================================
        public void RenderTerritories(int sw, int sh, Vector2 playerPos)
        {
            if (Projection == null) return;

            Raylib.DrawRectangle(0, 0, sw, sh, new Color(8, 12, 20, 255));

            // ── 1. Заливка полигонов ──────────────────────────────────────
            int validCount = 0, invalidCount = 0;
            foreach (var kv in _territories)
            {
                if (!_factions.TryGetValue(kv.Key, out var faction)) continue;
                var poly = kv.Value;
                if (poly.Count < 3) { invalidCount++; continue; }
                validCount++;
                var screen = ConvertToScreen(poly, sw, sh);
                PolygonFiller.FillConvexPolygon(screen.ToArray(), new Color(
                    (byte)faction.Color.R, (byte)faction.Color.G, (byte)faction.Color.B, (byte)200));
            }

            // ── 2. Белые границы поверх заливки ──────────────────────────
            foreach (var kv in _territories)
            {
                if (kv.Value.Count < 3) continue;
                var screen = ConvertToScreen(kv.Value, sw, sh);
                for (int i = 0; i < screen.Length; i++)
                    Raylib.DrawLineEx(screen[i], screen[(i + 1) % screen.Length], 2f, Color.White);
            }

            // ── 3. Отладка: статистика ───────────────────────────────────
            FontManager.DrawText(
                $"{Localization.Get("Territories")}: {validCount} {Localization.Get("Valid")}, {invalidCount} {Localization.Get("Invalid")}",
                10, sh - 30, 14, invalidCount > 0 ? Color.Red : Color.Lime);

            // ── 4. Поселения ─────────────────────────────────────────────
            foreach (var faction in _factions.Values)
            {
                foreach (var s in faction.Settlements)
                {
                    Vector2 sp = Projection.WorldToScreen(s.Position, sw, sh);
                    switch (s.Type)
                    {
                        case SettlementType.Capital:
                            Raylib.DrawCircle((int)sp.X, (int)sp.Y, 8, faction.Color);
                            Raylib.DrawCircleLines((int)sp.X, (int)sp.Y, 9, Color.White);
                            DrawStar((int)sp.X, (int)sp.Y, 13, Color.White);
                            FontManager.DrawText($"* {faction.Name}", (int)sp.X + 12, (int)sp.Y - 8, 12, Color.White);
                            break;
                        case SettlementType.City:
                            Raylib.DrawCircle((int)sp.X, (int)sp.Y, 4, Color.White);
                            Raylib.DrawCircleLines((int)sp.X, (int)sp.Y, 5,
                                new Color((byte)faction.Color.R, (byte)faction.Color.G, (byte)faction.Color.B, (byte)220));
                            break;
                        case SettlementType.Village:
                            Raylib.DrawCircle((int)sp.X, (int)sp.Y, 2, new Color(180, 180, 180, 160));
                            break;
                    }
                }
            }

            // ── 5. Названия фракций в центре территории ──────────────────
            foreach (var kv in _territories)
            {
                if (!_factions.TryGetValue(kv.Key, out var faction)) continue;
                if (kv.Value.Count < 3) continue;

                Vector2 cen = GetSimpleCentroid(kv.Value);
                Vector2 cs  = Projection.WorldToScreen(cen, sw, sh);
                float nameW = FontManager.MeasureText(faction.Name, 13);
                int nameX   = (int)(cs.X - nameW / 2);

                // Тень
                FontManager.DrawText(faction.Name, nameX + 1, (int)cs.Y - 5, 13, Color.Black);
                // Основной текст
                FontManager.DrawText(faction.Name, nameX, (int)cs.Y - 6, 13, Color.White);

                string stat = $"{Localization.Get("CitiesShort")}:{faction.TotalCities}  {Localization.Get("VillagesShort")}:{faction.TotalVillages}";
                float statW = FontManager.MeasureText(stat, 10);
                FontManager.DrawText(stat, (int)(cs.X - statW / 2), (int)cs.Y + 8, 10,
                    new Color((byte)faction.Color.R, (byte)faction.Color.G, (byte)faction.Color.B, (byte)230));
            }

            // ── 6. Позиция игрока ─────────────────────────────────────────
            Vector2 ps = Projection.WorldToScreen(playerPos, sw, sh);
            Raylib.DrawCircle((int)ps.X, (int)ps.Y, 7, Color.White);
            Raylib.DrawCircleLines((int)ps.X, (int)ps.Y, 8, Color.SkyBlue);
            Raylib.DrawLine((int)ps.X - 10, (int)ps.Y, (int)ps.X + 10, (int)ps.Y, Color.SkyBlue);
            Raylib.DrawLine((int)ps.X, (int)ps.Y - 10, (int)ps.X, (int)ps.Y + 10, Color.SkyBlue);
            FontManager.DrawText("ВЫ", (int)ps.X + 10, (int)ps.Y - 7, 12, Color.SkyBlue);

            // ── 7. Легенда ────────────────────────────────────────────────
            RenderMapLegend(sw, sh);
        }

        private void RenderMapLegend(int sw, int sh)
        {
            int lx = sw - 220, ly = sh - 130;
            Raylib.DrawRectangle(lx - 8, ly - 8, 220, 120, new Color(0, 0, 0, 180));
            Raylib.DrawRectangleLines(lx - 8, ly - 8, 220, 120, Color.Gray);

            FontManager.DrawText("ЛЕГЕНДА", lx, ly, 13, Color.Yellow); ly += 18;

            Raylib.DrawCircle(lx + 8, ly + 5, 7, Color.White);
            DrawStar(lx + 8, ly + 5, 11, Color.White);
            FontManager.DrawText("  Столица", lx + 2, ly, 12, Color.White); ly += 18;

            Raylib.DrawCircle(lx + 8, ly + 5, 4, Color.White);
            Raylib.DrawCircleLines(lx + 8, ly + 5, 5, Color.LightGray);
            FontManager.DrawText("  Город", lx + 2, ly, 12, Color.LightGray); ly += 18;

            Raylib.DrawCircle(lx + 8, ly + 5, 2, new Color(180, 180, 180, 200));
            FontManager.DrawText("  Деревня", lx + 2, ly, 12, new Color(180, 180, 180, 200)); ly += 18;

            Raylib.DrawCircle(lx + 8, ly + 5, 5, Color.SkyBlue);
            FontManager.DrawText("  Вы", lx + 2, ly, 12, Color.SkyBlue);
        }

        // ================================================================
        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ РЕНДЕРА
        // ================================================================
        private Vector2[] ConvertToScreen(List<Vector2> poly, int sw, int sh)
        {
            var result = new Vector2[poly.Count];
            for (int i = 0; i < poly.Count; i++)
            {
                result[i] = Projection!.WorldToScreen(poly[i], sw, sh);
                result[i].X = Math.Clamp(result[i].X, 0, sw);
                result[i].Y = Math.Clamp(result[i].Y, 0, sh);
            }
            return result;
        }

        private static void DrawFilledPolygon(Vector2[] pts, Color color)
        {
            if (pts.Length < 3) return;
            for (int i = 1; i < pts.Length - 1; i++)
                Raylib.DrawTriangle(pts[0], pts[i], pts[i + 1], color);
        }

        private static void DrawStar(int x, int y, int r, Color color)
        {
            int h = (int)(r * 0.7f);
            Raylib.DrawLine(x - h, y - h, x + h, y + h, color);
            Raylib.DrawLine(x + h, y - h, x - h, y + h, color);
        }

        private static Vector2 GetSimpleCentroid(List<Vector2> poly)
        {
            float sx = 0, sy = 0;
            foreach (var v in poly) { sx += v.X; sy += v.Y; }
            return new Vector2(sx / poly.Count, sy / poly.Count);
        }
    }

    // ================================================================
    //  ГРАНИЦА ТЕРРИТОРИЙ (физическая, рядом с игроком)
    // ================================================================
    public class TerritoryBorder
    {
        public Faction  Faction1  { get; }
        public Faction  Faction2  { get; }
        public Vector2  EdgeStart { get; }
        public Vector2  EdgeEnd   { get; }
        public Vector2  Midpoint  => (EdgeStart + EdgeEnd) * 0.5f;
        public List<BorderCheckpoint> Checkpoints { get; } = new();

        public TerritoryBorder(Faction f1, Faction f2, Vector2 start, Vector2 end)
        {
            Faction1  = f1; Faction2  = f2;
            EdgeStart = start; EdgeEnd = end;
            Checkpoints.Add(new BorderCheckpoint(Midpoint, f1, f2));
        }

        public void Render()
        {
            Raylib.DrawLineV(EdgeStart, EdgeEnd, Color.Gray);
            foreach (var cp in Checkpoints) cp.Render();
        }

        public void RenderGlobal(MapProjection projection, int sw, int sh)
        {
            Vector2 s = projection.WorldToScreen(EdgeStart, sw, sh);
            Vector2 e = projection.WorldToScreen(EdgeEnd,   sw, sh);
            Raylib.DrawLineV(s, e, Color.White);
        }
    }

    // ================================================================
    //  КПП НА ГРАНИЦЕ
    // ================================================================
    public class BorderCheckpoint
    {
        public Vector2 Position { get; }
        public Faction Faction1 { get; }
        public Faction Faction2 { get; }
        public float   Health   { get; set; } = 1000.0f;

        public BorderCheckpoint(Vector2 pos, Faction f1, Faction f2)
        {
            Position = pos; Faction1 = f1; Faction2 = f2;
        }

        public void Render()
        {
            Raylib.DrawRectangle((int)Position.X - 30, (int)Position.Y - 30, 60, 60, Color.Brown);
            Raylib.DrawRectangleLines((int)Position.X - 30, (int)Position.Y - 30, 60, 60, Color.Black);
            Raylib.DrawCircle((int)Position.X - 20, (int)Position.Y - 20, 10, Faction1.Color);
            Raylib.DrawCircle((int)Position.X + 20, (int)Position.Y + 20, 10, Faction2.Color);
            // КПП — кириллица через FontManager
            FontManager.DrawText("КПП", (int)Position.X - 15, (int)Position.Y - 5, 12, Color.White);
        }
    }

    // ================================================================
    //  MATHF HELPER
    // ================================================================
    public static class Mathf
    {
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
    }
}
