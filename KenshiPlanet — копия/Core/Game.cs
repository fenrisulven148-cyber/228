
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KenshiPlanet.Economy;
using KenshiPlanet.Entities;
using KenshiPlanet.Factions;
using KenshiPlanet.Rendering;
using KenshiPlanet.World;
using KenshiPlanet.UI;

namespace KenshiPlanet.Core
{
    public class Game : IDisposable
    {
        private const double TICK_RATE = 1.0 / 60.0;
        private double _tickAccumulator = 0.0;
        private double _lastFrameTime   = 0.0;

        public WorldManager World  { get; private set; }
        public Camera2D     Camera => CameraManager.Instance.Camera;
        public Player       Player { get; private set; }

        // Система добычи ресурсов
        private MiningSession _miningSession = new();
        private UI.ResourceInteractionUI _resourceUI = new();

        private bool _showGlobalMap   = false;
        private bool _showFactionInfo = false;
        private bool _showLawsPanel   = false;
        private int? _selectedFactionId = null;
        private float _infoPanelAlpha = 0.0f;
        private const float INFO_ANIMATION_SPEED = 0.05f;

        private NPC?        _nearestNPC        = null;
        private Market?     _nearestMarket     = null;
        private Settlement? _nearestSettlement = null;
        private const float INTERACT_RADIUS = 150.0f;

        private float _cameraZoom = 0.3f;
        private const float MIN_ZOOM = 0.02f;
        private const float MAX_ZOOM = 2.0f;

        private float _timeScale = 1.0f;

        public Game()
        {
            InitializeFont();
            TestLocalization();
            World  = new WorldManager();
            Player = new Player { Position = Vector2.Zero, Money = 500.0f };
            Player.AddItem(ResourceType.Food, 10.0f);
            
            // Подписываемся на событие кнопки добычи
            _resourceUI.OnMineButtonClick += OnMineButtonClicked;
            
            // Передаем ссылку на мир в UI ресурсов
            _resourceUI.SetWorld(World);
        }

        // ----------------------------------------------------------------
        //  ИНИЦИАЛИЗАЦИЯ ШРИФТА
        // ----------------------------------------------------------------
        private void InitializeFont()
        {
            // Строим полный массив Unicode-точек:
            // 1) Базовый ASCII (пробел + все печатаемые символы 32-126)
            // 2) Кириллица А-я (1040-1103)
            // 3) Ё и ё (1025, 1105)
            // 4) Типографика (тире, кавычки, многоточие и т.п.)
            var codepoints = new System.Collections.Generic.List<int>();

            // ASCII 32-126
            for (int i = 32; i <= 126; i++)
                codepoints.Add(i);

            // Кириллица А-Я, а-я
            for (int i = 1040; i <= 1103; i++)
                codepoints.Add(i);

            // Ё (1025) и ё (1105)
            codepoints.Add(1025);
            codepoints.Add(1105);

            // Типографические знаки: –, —, «, », „, ", ", •, …, ′
            codepoints.AddRange(new[] { 8211, 8212, 8216, 8217, 8220, 8221, 8222, 8226, 8230, 8242 });

            // Служебные символы интерфейса: ✓, ✗, ⚔, ★, ⚠
            codepoints.AddRange(new[] { 10003, 10007, 9876, 9733, 9888 });

            int[] pts = codepoints.ToArray();

            // Порядок поиска шрифтовых файлов
            string[] fontPaths = {
                "assets/fonts/arial.ttf",
                "assets/fonts/roboto.ttf",
                "assets/fonts/opensans.ttf",
                "C:/Windows/Fonts/arial.ttf",
                "C:/Windows/Fonts/arialuni.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"
            };

            Font font = Raylib.GetFontDefault();
            bool fontLoaded = false;

            foreach (var path in fontPaths)
            {
                if (!System.IO.File.Exists(path))
                {
                    Console.WriteLine($"[Шрифт] Не найден: {path}");
                    continue;
                }

                Console.WriteLine($"[Шрифт] Загрузка: {path}");
                // ВАЖНО: последний аргумент — pts.Length, а НЕ 0!
                font = Raylib.LoadFontEx(path, 32, pts, pts.Length);

                if (font.Texture.Id != 0)
                {
                    Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
                    Console.WriteLine($"[Шрифт] Загружен: {path}  (глифов: {pts.Length})");
                    fontLoaded = true;
                    break;
                }
            }

            if (!fontLoaded)
            {
                Console.WriteLine("[Шрифт] ОШИБКА: не удалось загрузить TTF с кириллицей.");
                Console.WriteLine("[Шрифт] Положите arial.ttf в assets/fonts/ или установите DejaVuSans.");
                font = Raylib.GetFontDefault();
            }

            // Сохраняем в глобальный FontManager — все остальные файлы берут шрифт оттуда
            FontManager.Font = font;

            Console.WriteLine("[Шрифт] Инициализация завершена.");
        }

        private void TestLocalization()
        {
            Console.WriteLine("=== Тест локализации ===");
            Console.WriteLine($"GlobalMapTitle: {Localization.Get("GlobalMapTitle")}");
            Console.WriteLine($"Controls: {Localization.Get("Controls")}");
            Console.WriteLine($"Type: {Localization.Get("Type")}");
            Console.WriteLine($"Military: {Localization.Get("Military")}");
            Console.WriteLine($"Wealth: {Localization.Get("Wealth")}");
            Console.WriteLine("=== Тест локализации завершен ===");
        }

        public void Tick()
        {
            double now      = Raylib.GetTime();
            double rawDelta = now - _lastFrameTime;
            _lastFrameTime  = now;
            double scaledDt = rawDelta * _timeScale;

            HandleInput(rawDelta, scaledDt);

            _tickAccumulator += scaledDt;
            while (_tickAccumulator >= TICK_RATE)
            {
                UpdateLogic(TICK_RATE);
                _tickAccumulator -= TICK_RATE;
            }

            Render();
        }

        // ======================
        //  ВВОД
        // ======================
        private void HandleInput(double rawDt, double scaledDt)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.M)) _showGlobalMap   = !_showGlobalMap;
            if (Raylib.IsKeyPressed(KeyboardKey.F)) _showFactionInfo = !_showFactionInfo;
            if (Raylib.IsKeyPressed(KeyboardKey.L)) _showLawsPanel   = !_showLawsPanel;

            _timeScale = Raylib.IsKeyDown(KeyboardKey.Tab) ? 5.0f : 1.0f;

            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
                _cameraZoom = Math.Clamp(_cameraZoom * (1.0f + wheel * 0.12f), MIN_ZOOM, MAX_ZOOM);

            if (_showGlobalMap && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                HandleGlobalMapClick();
                goto updateCamera;
            }

            if (_showGlobalMap) goto updateCamera;

            Vector2 move = Vector2.Zero;
            if (Raylib.IsKeyDown(KeyboardKey.W)) move.Y -= 1;
            if (Raylib.IsKeyDown(KeyboardKey.S)) move.Y += 1;
            if (Raylib.IsKeyDown(KeyboardKey.A)) move.X -= 1;
            if (Raylib.IsKeyDown(KeyboardKey.D)) move.X += 1;
            Player.Update(scaledDt, move);

            if (Raylib.IsKeyPressed(KeyboardKey.E) && _nearestMarket != null)
            {
                if (Player.BuyItem(_nearestMarket, ResourceType.Food, 5.0f))
                    Player.Hunger = Math.Max(0, Player.Hunger - 40.0f);
            }

            // Обработка взаимодействия с ресурсами
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                // Сначала проверяем клик по NPC (враждебные)
                if (_nearestNPC != null)
                {
                    var rel = World.GetPlayerRelationToFaction(_nearestNPC.FactionId, Player);
                    if (rel == FactionRelation.Hostile || rel == FactionRelation.War)
                    {
                        Player.TryAttackNPC(_nearestNPC, World.Factions.ToDictionary(f => f.Key, f => f.Value.Color));
                        goto updateCamera;
                    }
                }
                
                // Если не кликнули по NPC, проверяем ресурсы
                CheckResourceClick();
            }

            updateCamera:
            int sw = Raylib.GetScreenWidth();
            int sh = Raylib.GetScreenHeight();
            CameraManager.Instance.UpdateCamera(Player.Position, _cameraZoom,
                new Vector2(sw / 2.0f, sh / 2.0f));
        }
        
        /// <summary>
        /// Проверяет клик по ресурсам поблизости
        /// </summary>
        private void CheckResourceClick()
        {
            // Находим ближайший активный ресурс в радиусе взаимодействия
            ResourceNode? nearestResource = null;
            float nearestDist = float.MaxValue;
            
            foreach (var resource in World.Resources.Values)
            {
                if (!resource.IsActive) continue;
                
                float dist = Vector2.Distance(Player.Position, resource.Position);
                if (dist < UI.ResourceInteractionUI.INTERACTION_DISTANCE && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestResource = resource;
                }
            }
            
            if (nearestResource != null)
            {
                _resourceUI.TrySelectNode(nearestResource, Player.Position);
            }
        }
        
        /// <summary>
        /// Обработчик нажатия кнопки "Добыть" в UI
        /// </summary>
        private void OnMineButtonClicked()
        {
            _resourceUI.StartMining(_miningSession);
        }

        private void HandleGlobalMapClick()
        {
            Vector2 mousePos = Raylib.GetMousePosition();
            int sw = Raylib.GetScreenWidth();
            int sh = Raylib.GetScreenHeight();

            int fx = 10, fy = 50;
            fy += 20;

            foreach (var faction in World.Factions.Values.OrderBy(f => f.Name))
            {
                if (mousePos.X >= fx - 5 && mousePos.X <= fx + 200 &&
                    mousePos.Y >= fy - 2 && mousePos.Y <= fy + 16)
                {
                    _selectedFactionId = _selectedFactionId == faction.Id ? (int?)null : faction.Id;
                    return;
                }

                fy += 18;
                if (fy > sh - 200) { fy = 70; fx += 220; }
            }

            var proj = World.TerritoryManager.Projection;
            if (proj != null)
            {
                int? factionId = GetFactionIdAtPosition(mousePos, proj, sw, sh);
                _selectedFactionId = factionId;
            }
        }

        private int? GetFactionIdAtPosition(Vector2 screenPos, MapProjection proj, int sw, int sh)
        {
            Vector2 worldPos = proj.ScreenToWorld(screenPos, sw, sh);
            foreach (var kv in World.TerritoryManager.Territories)
                if (IsPointInPolygon(worldPos, kv.Value)) return kv.Key;
            return null;
        }

        private bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 pi = polygon[i], pj = polygon[j];
                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                    inside = !inside;
            }
            return inside;
        }

        // ======================
        //  ЛОГИКА
        // ======================
        private void UpdateLogic(double dt)
        {
            World.Update(dt, Player.Position);
            Player.CurrentFactionTerritory = World.TerritoryManager.GetFactionAtPosition(Player.Position);
            Player.IsNight = World.IsNight; // синхронизируем из единственного источника
            UpdateInteractionTargets();

            // Обновляем сессию добычи ресурсов
            if (_miningSession.IsMining)
            {
                _miningSession.Update((float)dt);
                
                // Проверяем, можем ли мы добыть ресурс
                if (_miningSession.CanExtract())
                {
                    float extracted = _miningSession.ExtractUnit();
                    if (extracted > 0 && _miningSession.TargetNode != null)
                    {
                        // Добавляем ресурс в инвентарь игрока
                        Player.AddItem(_miningSession.TargetNode.Type, extracted);
                    }
                }
                
                // Если игрок отошел слишком далеко, прерываем добычу
                if (_miningSession.TargetNode != null)
                {
                    float distToTarget = Vector2.Distance(Player.Position, _miningSession.TargetNode.Position);
                    if (distToTarget > UI.ResourceInteractionUI.INTERACTION_DISTANCE * 1.5f)
                    {
                        _miningSession.Stop();
                    }
                }
            }
            
            // Обновляем UI ресурсов
            _resourceUI.Update(Player, dt);

            if (_selectedFactionId.HasValue)
                _infoPanelAlpha = Math.Min(_infoPanelAlpha + INFO_ANIMATION_SPEED, 1.0f);
            else
                _infoPanelAlpha = Math.Max(_infoPanelAlpha - INFO_ANIMATION_SPEED, 0.0f);

            var dead = World.NPCs.Where(kv => kv.Value.Health <= 0).Select(kv => kv.Key).ToList();
            foreach (var id in dead)
            {
                var npc = World.NPCs[id];
                World.NPCs.Remove(id);
                if (World.Factions.TryGetValue(npc.FactionId, out var f))
                {
                    f.Members.Remove(npc);
                    Player.ModifyReputation(npc.FactionId, -10.0f);
                    if (Player.GetReputation(npc.FactionId) < -50)
                        Player.SetWanted(npc.FactionId, true);
                }
            }
        }

        private void UpdateInteractionTargets()
        {
            _nearestNPC = null; _nearestMarket = null; _nearestSettlement = null;
            float dNPC = float.MaxValue, dMkt = float.MaxValue, dSet = float.MaxValue;

            foreach (var npc in World.NPCs.Values)
            {
                float d = Vector2.Distance(Player.Position, npc.Position);
                if (d < INTERACT_RADIUS && d < dNPC) { dNPC = d; _nearestNPC = npc; }
            }

            foreach (var f in World.Factions.Values)
            {
                foreach (var s in f.Settlements)
                {
                    float d = Vector2.Distance(Player.Position, s.Position);
                    if (d < s.Radius * 2 && d < dSet) { dSet = d; _nearestSettlement = s; }
                    if (d < s.Radius + 200 && d < dMkt)
                    {
                        var m = World.Markets.FirstOrDefault(x => x.SettlementId == s.Id);
                        if (m != null) { dMkt = d; _nearestMarket = m; }
                    }
                }
            }
        }

        // ======================
        //  РЕНДЕР
        // ======================
        private void Render()
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(15, 15, 20, 255));

            if (_showGlobalMap)
            {
                RenderGlobalMap();
            }
            else
            {
                Raylib.BeginMode2D(Camera);
                World.Render(Player.Position);
                Player.Render();
                RenderInteractionHighlights();
                DrawWorldGrid();
                Raylib.EndMode2D();

                // --- Ночной оверлей (поверх мира, под HUD) ---
                if (World.NightIntensity > 0f)
                {
                    int sw = Raylib.GetScreenWidth();
                    int sh = Raylib.GetScreenHeight();
                    byte alpha = (byte)(World.NightIntensity * 160); // макс. затемнение ~63%
                    Raylib.DrawRectangle(0, 0, sw, sh, new Color((byte)0, (byte)0, (byte)40, alpha));
                }

                // Рендер UI ресурсов (поверх всего, но под HUD)
                _resourceUI.Render(_miningSession, Player);

                RenderHUD();
            }

            Raylib.EndDrawing();
        }

        private void RenderInteractionHighlights()
        {
            if (_nearestNPC != null)
                Raylib.DrawCircleLines((int)_nearestNPC.Position.X, (int)_nearestNPC.Position.Y, 16, Color.Yellow);
            if (_nearestSettlement != null)
                Raylib.DrawCircleLines((int)_nearestSettlement.Position.X, (int)_nearestSettlement.Position.Y,
                    (int)_nearestSettlement.Radius + 12, Color.Yellow);
        }

        private void DrawWorldGrid()
        {
            int gs = 10000, rng = 8;
            for (int x = -rng; x <= rng; x++)
                for (int y = -rng; y <= rng; y++)
                {
                    Raylib.DrawLine(x * gs, y * gs, (x+1) * gs, y * gs, Color.DarkGray);
                    Raylib.DrawLine(x * gs, y * gs, x * gs, (y+1) * gs, Color.DarkGray);
                }
        }

        // ======================
        //  HUD
        // ======================
        private void RenderHUD()
        {
            int sw = Raylib.GetScreenWidth();
            int sh = Raylib.GetScreenHeight();

            string factionName = Localization.Get("Wilderness");
            bool   isWanted    = false;
            Faction? curFaction = null;
            if (Player.CurrentFactionTerritory > 0 && World.Factions.TryGetValue(Player.CurrentFactionTerritory, out curFaction))
            {
                factionName = curFaction.Name;
                isWanted    = Player.IsWanted(curFaction.Id);
            }

            Player.RenderHUD(factionName, isWanted, Player.IsNight, 0.0f);

            var f = FontManager.Font;
            int ix = sw - 270, iy = 10;
            Raylib.DrawTextEx(f, $"{Localization.Get("FPS")}: {Raylib.GetFPS()}", new Vector2(ix, iy), 16, 1f, Color.Lime);
            Raylib.DrawTextEx(f, $"{Localization.Get("Zoom")}: {_cameraZoom:F2}x", new Vector2(ix, iy+20), 13, 1f, Color.White);
            if (_timeScale > 1) Raylib.DrawTextEx(f, $">> {Localization.Get("FastTime")} <<", new Vector2(ix, iy+38), 13, 1f, Color.Orange);
            Raylib.DrawTextEx(f, $"{Localization.Get("NPCsActive")}: {World.ActiveNPCCount}", new Vector2(ix, iy+56), 12, 1f, Color.LightGray);
            Raylib.DrawTextEx(f, $"{Localization.Get("Factions")}: {World.Factions.Count}", new Vector2(ix, iy+70), 12, 1f, Color.LightGray);
            Raylib.DrawTextEx(f, $"{Localization.Get("Wars")}: {World.Factions.Values.Sum(fa => fa.AtWarWith.Count) / 2}", new Vector2(ix, iy+84), 12, 1f, Color.Red);

            Raylib.DrawTextEx(f, Localization.Get("Controls"), new Vector2(10, sh - 22), 12, 1f, new Color(150,150,150,200));

            if (_nearestMarket != null)
                Raylib.DrawTextEx(f, Localization.Get("BuyFood"), new Vector2(sw/2-60, sh/2+50), 16, 1f, Color.Yellow);
            if (_nearestNPC != null)
                Raylib.DrawTextEx(f, Localization.Get("Attack") + $" {_nearestNPC.Name}", new Vector2(sw/2-100, sh/2+70), 14, 1f, Color.Orange);
            if (_showFactionInfo && curFaction != null) RenderFactionPanel(curFaction, sw, sh);
            if (_showLawsPanel   && curFaction != null) RenderLawsPanel(curFaction, sw, sh);
        }

        private void RenderFactionPanel(Faction faction, int sw, int sh)
        {
            var f = FontManager.Font;
            int pw = 320, ph = 360, px = sw/2-pw/2, py = 60;
            Raylib.DrawRectangle(px, py, pw, ph, new Color(10,10,30,220));
            Raylib.DrawRectangleLines(px, py, pw, ph, faction.Color);
            int ty = py + 10;
            void T(string txt, int size, Color col) { Raylib.DrawTextEx(f, txt, new Vector2(px+10, ty), size, 1f, col); ty += size + 4; }
            T($"== {faction.Name} ==", 18, faction.Color);
            T($"{Localization.Get("Wealth")}: {faction.Wealth:F0}{Localization.Get("Gold")}", 14, Color.Gold);
            T($"{Localization.Get("Members")}: {faction.Members.Count}", 14, Color.White);
            T($"{Localization.Get("Soldiers")}: {faction.TotalSoldiers}", 14, Color.Red);
            T($"{Localization.Get("MilitaryStrength")}: {faction.MilitaryStrength:F0}", 14, Color.Orange);
            T($"{Localization.Get("Cities")}: {faction.TotalCities}  {Localization.Get("Villages")}: {faction.TotalVillages}", 13, Color.SkyBlue);
            ty += 6;
            T($"--- {Localization.Get("Resources")} ---", 13, Color.Yellow);
            foreach (var kv in faction.Resources) T($"  {kv.Key}: {kv.Value:F0}", 12, Color.White);
            ty += 6;
            T($"--- {Localization.Get("Wars")} ---", 13, Color.Red);
            if (faction.AtWarWith.Count == 0) T("  " + Localization.Get("Peace"), 12, Color.Green);
            else foreach (var eid in faction.AtWarWith)
                if (World.Factions.TryGetValue(eid, out var e)) T($"  {e.Name}", 12, Color.Red);
            ty += 6;
            T($"--- {Localization.Get("RecentEvents")} ---", 13, Color.Yellow);
            foreach (var evt in faction.EventLog.Take(5)) T(evt.Length > 42 ? evt[..42]+"…" : evt, 11, Color.LightGray);
            T($"[F] {Localization.Get("Close")}", 11, Color.Gray);
        }

        private void RenderLawsPanel(Faction faction, int sw, int sh)
        {
            var f = FontManager.Font;
            int pw = 280, ph = 260, px = 10, py = sh - ph - 10;
            Raylib.DrawRectangle(px, py, pw, ph, new Color(10,20,10,220));
            Raylib.DrawRectangleLines(px, py, pw, ph, Color.Green);
            int ty = py + 10;
            void Row(string txt, Color c) { Raylib.DrawTextEx(f, txt, new Vector2(px+8, ty), 13, 1f, c); ty += 16; }
            Row($"== {faction.Name} {Localization.Get("Laws")} ==", Color.Green);
            Row($"{Localization.Get("Tax")}: {faction.Laws.TaxRate*100:F0}%  {Localization.Get("CrimeFine")}: {faction.Laws.CrimeFine}{Localization.Get("Gold")}", Color.White);
            Row(faction.Laws.OpenBorders ? Localization.Get("BordersOpen") : $"{Localization.Get("BordersClosed")} ({faction.Laws.BorderEntryFee}{Localization.Get("Gold")})",
                faction.Laws.OpenBorders ? Color.Green : Color.Red);
            ty += 4;
            Row(Localization.Get("Activities") + ":", Color.Yellow);
            Row($"  {(faction.Laws.SlaveryLegal ? "+" : "-")} {Localization.Get("Slavery")}", faction.Laws.SlaveryLegal ? Color.Green : Color.Gray);
            Row($"  {(faction.Laws.DrugsLegal ? "+" : "-")} {Localization.Get("Drugs")}", faction.Laws.DrugsLegal ? Color.Green : Color.Gray);
            Row($"  {(faction.Laws.MercenaryWorkLegal ? "+" : "-")} {Localization.Get("MercenaryWork")}", faction.Laws.MercenaryWorkLegal ? Color.Green : Color.Gray);
            Row($"  {(faction.Laws.StreetFightingLegal ? "+" : "-")} {Localization.Get("StreetFighting")}", faction.Laws.StreetFightingLegal ? Color.Green : Color.Gray);
            Row($"  {(faction.Laws.ConscritionActive ? "!" : " ")} {Localization.Get("Conscription")}", faction.Laws.ConscritionActive ? Color.Red : Color.Gray);
            float rep = Player.GetReputation(faction.Id);
            Raylib.DrawTextEx(f, $"{Localization.Get("YourReputation")}: {rep:+0;-0;0}", new Vector2(px+8, ty), 14, 1f, rep > 30 ? Color.Green : rep < -30 ? Color.Red : Color.White);
        }

        // ======================
        //  ГЛОБАЛЬНАЯ КАРТА
        // ======================
        private void RenderGlobalMap()
        {
            var f = FontManager.Font;
            int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
            Raylib.DrawTextEx(f, Localization.Get("GlobalMapTitle") + "  " + Localization.Get("GlobalMapClose"), new Vector2(10, 10), 20, 1f, Color.Yellow);
            World.TerritoryManager.RenderTerritories(sw, sh, Player.Position);

            var proj = World.TerritoryManager.Projection;
            if (proj != null)
            {
                var ps = proj.WorldToScreen(Player.Position, sw, sh);
                Raylib.DrawCircle((int)ps.X, (int)ps.Y, 7, Color.White);
                Raylib.DrawCircleLines((int)ps.X, (int)ps.Y, 7, Color.SkyBlue);
                Raylib.DrawTextEx(f, Localization.Get("PlayerMarker"), new Vector2((int)ps.X+9, (int)ps.Y-6), 12, 1f, Color.SkyBlue);
            }

            if (_selectedFactionId.HasValue && _infoPanelAlpha > 0.01f &&
                World.Factions.TryGetValue(_selectedFactionId.Value, out var sel))
            {
                int x = sw - 350, y = sh - 250;
                Color bgCol     = new Color((byte)0,   (byte)0,   (byte)0,   (byte)(180 * _infoPanelAlpha));
                Color borderCol = new Color((byte)255, (byte)255, (byte)255, (byte)(255 * _infoPanelAlpha));
                Raylib.DrawRectangle(x - 10, y - 10, 350, 250, bgCol);
                Raylib.DrawRectangleLines(x - 10, y - 10, 350, 250, borderCol);

                Color fCol = new Color((byte)sel.Color.R, (byte)sel.Color.G, (byte)sel.Color.B, (byte)(255 * _infoPanelAlpha));
                Raylib.DrawTextEx(f, sel.Name, new Vector2(x, y), 24, 1f, fCol);
                y += 30;

                string typeText = Localization.Get("Type") + ": " + GetFactionTypeName(sel.Type);
                Color textCol   = new Color((byte)255, (byte)255, (byte)255, (byte)(255 * _infoPanelAlpha));
                Raylib.DrawTextEx(f, typeText, new Vector2(x, y), 18, 1f, textCol);
                y += 25;

                if (!string.IsNullOrEmpty(sel.Description))
                {
                    DrawMultilineText(sel.Description, x, y, 330, 16,
                        new Color((byte)200, (byte)200, (byte)200, (byte)(255 * _infoPanelAlpha)));
                    y += 60;
                }

                if (sel.UniqueBuildings.Count > 0)
                {
                    string bld = Localization.Get("UniqueBuildings") + ": " + string.Join(", ", sel.UniqueBuildings);
                    Raylib.DrawTextEx(f, bld, new Vector2(x, y), 16, 1f,
                        new Color((byte)255, (byte)215, (byte)0, (byte)(255 * _infoPanelAlpha)));
                    y += 20;
                }

                DrawModifierBar(Localization.Get("Production"), sel.ResourceProductionMultiplier, x, y, _infoPanelAlpha); y += 30;
                DrawModifierBar(Localization.Get("Combat"),     sel.CombatEffectiveness,          x, y, _infoPanelAlpha); y += 30;
                DrawModifierBar(Localization.Get("Building"),   sel.BuildingSpeed,                x, y, _infoPanelAlpha); y += 30;
                DrawModifierBar(Localization.Get("Diplomacy"),  sel.DiplomacyEffect,              x, y, _infoPanelAlpha);
            }

            int wx = sw - 230, wy = 50;
            Raylib.DrawTextEx(f, Localization.Get("ActiveWars"), new Vector2(wx, wy), 14, 1f, Color.Red); wy += 18;
            bool any = false;
            foreach (var fa in World.Factions.Values)
                foreach (var eid in fa.AtWarWith)
                {
                    if (eid <= fa.Id) continue;
                    if (World.Factions.TryGetValue(eid, out var eb))
                    {
                        Raylib.DrawTextEx(f, $"{fa.Name} {Localization.Get("WarAgainst")} {eb.Name}", new Vector2(wx, wy), 11, 1f, Color.Orange);
                        wy += 14; any = true;
                    }
                }
            if (!any) Raylib.DrawTextEx(f, "  " + Localization.Get("Peace"), new Vector2(wx, wy), 11, 1f, Color.Gray);
        }

        private void DrawMultilineText(string text, int x, int y, int maxWidth, int fontSize, Color color)
        {
            var f = FontManager.Font;
            string[] words = text.Split(' ');
            string currentLine = "";
            int currentY = y;

            foreach (string word in words)
            {
                string testLine = currentLine == "" ? word : currentLine + " " + word;
                float w = Raylib.MeasureTextEx(f, testLine, fontSize, 1).X;

                if (w > maxWidth)
                {
                    if (currentLine != "")
                    {
                        Raylib.DrawTextEx(f, currentLine, new Vector2(x, currentY), fontSize, 1f, color);
                        currentY += fontSize + 2;
                    }
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (currentLine != "")
                Raylib.DrawTextEx(f, currentLine, new Vector2(x, currentY), fontSize, 1f, color);
        }

        private void DrawModifierBar(string label, float value, int x, int y, float alpha)
        {
            var f = FontManager.Font;
            Color textColor = new Color((byte)255, (byte)255, (byte)255, (byte)(255 * alpha));
            Raylib.DrawTextEx(f, $"{label}: {value:F2}x", new Vector2(x, y), 16, 1f, textColor);
            Color barColor = value < 0.9f ? Color.Red : (value > 1.1f ? Color.Green : Color.Yellow);
            barColor = new Color((byte)barColor.R, (byte)barColor.G, (byte)barColor.B, (byte)(255 * alpha));
            Color lineColor = new Color((byte)255, (byte)255, (byte)255, (byte)(255 * alpha));
            int barWidth = (int)(value * 80);
            Raylib.DrawRectangle(x + 150, y + 2, barWidth, 15, barColor);
            Raylib.DrawRectangleLines(x + 150, y + 2, 80, 15, lineColor);
        }

        private string GetFactionTypeName(FactionType type) => type switch
        {
            FactionType.Military     => Localization.Get("Military"),
            FactionType.Economic     => Localization.Get("Economic"),
            FactionType.Technological=> Localization.Get("Technological"),
            FactionType.Magical      => Localization.Get("Magical"),
            FactionType.Agricultural => Localization.Get("Agricultural"),
            FactionType.Nomadic      => Localization.Get("Nomadic"),
            FactionType.Religious    => Localization.Get("Religious"),
            FactionType.Industrial   => Localization.Get("Industrial"),
            FactionType.Stealth      => Localization.Get("Stealth"),
            FactionType.Diplomatic   => Localization.Get("Diplomatic"),
            _                        => Localization.Get("Universal")
        };

        public void Dispose() => World.Dispose();
    }
}

