
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using KenshiPlanet.Economy;
using KenshiPlanet.Core;
using KenshiPlanet.World;

namespace KenshiPlanet.Entities
{
    public enum PlayerState { Idle, Moving, Combat, Sleeping, Trading }

    public class Player
    {
        public Vector2 Position { get; set; } = Vector2.Zero;
        public float Speed { get; set; } = 300.0f;
        public float Health { get; set; } = 100.0f;
        public float MaxHealth { get; set; } = 100.0f;
        public float Hunger { get; set; } = 0.0f;
        public float Energy { get; set; } = 100.0f;
        public float Money { get; set; } = 500.0f;
        public PlayerState State { get; set; } = PlayerState.Idle;

        public Dictionary<ResourceType, float> Inventory { get; } = new();
        public Dictionary<int, float> Reputation { get; } = new();
        public Dictionary<int, bool> Wanted { get; } = new();
        public int CurrentFactionTerritory { get; set; } = -1;

        public float AttackDamage { get; set; } = 20.0f;
        public float DefenseRating { get; set; } = 5.0f;
        public float AttackRange { get; set; } = 60.0f;
        private float _attackCooldown = 0.0f;
        private const float ATTACK_SPEED = 1.5f;

        // IsNight синхронизируется из WorldManager.IsNight в Game.UpdateLogic
        public bool IsNight { get; set; } = false;

        public void Update(double deltaTime, Vector2 moveInput)
        {
            float dt = (float)deltaTime;

            if (moveInput != Vector2.Zero)
            {
                Vector2 dir = Vector2.Normalize(moveInput);
                Position += dir * Speed * dt;
                State = PlayerState.Moving;
            }
            else if (State != PlayerState.Combat && State != PlayerState.Trading && State != PlayerState.Sleeping)
            {
                State = PlayerState.Idle;
            }

            float hungerRate = State == PlayerState.Moving ? 0.4f : 0.2f;
            Hunger += hungerRate * dt;
            if (Hunger >= 100.0f) { Health -= 0.5f * dt; Hunger = 100.0f; }

            if (State == PlayerState.Moving)        Energy -= 0.08f * dt;
            else if (State == PlayerState.Sleeping) Energy += 2.0f  * dt;
            else                                    Energy += 0.05f * dt;

            Energy = Math.Clamp(Energy, 0.0f, 100.0f);

            if (Energy <= 0.0f) { State = PlayerState.Sleeping; Speed = 0f; }
            else if (State == PlayerState.Sleeping && Energy >= 80.0f) { State = PlayerState.Idle; Speed = 300.0f; }

            if (Hunger < 50.0f && Energy > 30.0f && Health < MaxHealth)
                Health = Math.Min(Health + 0.3f * dt, MaxHealth);

            if (_attackCooldown > 0) _attackCooldown -= dt;
        }

        public bool CanAttack() => _attackCooldown <= 0 && State != PlayerState.Sleeping;

        public void TriggerAttack()
        {
            _attackCooldown = 1.0f / ATTACK_SPEED;
            State = PlayerState.Combat;
        }

        public bool TryAttackNPC(NPC npc, Dictionary<int, Raylib_cs.Color>? factionColors)
        {
            if (!CanAttack()) return false;
            if (Vector2.Distance(Position, npc.Position) > AttackRange) return false;
            npc.Health -= Math.Max(0, AttackDamage - npc.DefenseRating);
            TriggerAttack();
            return true;
        }

        public void ModifyReputation(int factionId, float delta)
        {
            if (!Reputation.ContainsKey(factionId)) Reputation[factionId] = 0.0f;
            Reputation[factionId] = Math.Clamp(Reputation[factionId] + delta, -100.0f, 100.0f);
        }

        public float GetReputation(int factionId) =>
            Reputation.TryGetValue(factionId, out var r) ? r : 0.0f;

        public bool IsWanted(int factionId) =>
            Wanted.TryGetValue(factionId, out var w) && w;

        public void SetWanted(int factionId, bool wanted)
        {
            Wanted[factionId] = wanted;
            if (wanted) ModifyReputation(factionId, -30.0f);
        }

        public void AddItem(ResourceType type, float amount)
        {
            if (!Inventory.ContainsKey(type)) Inventory[type] = 0;
            Inventory[type] += amount;
        }

        public bool RemoveItem(ResourceType type, float amount)
        {
            if (!Inventory.TryGetValue(type, out var cur) || cur < amount) return false;
            Inventory[type] -= amount;
            return true;
        }

        public float GetItemAmount(ResourceType type) =>
            Inventory.TryGetValue(type, out var a) ? a : 0.0f;

        public bool BuyItem(Market market, ResourceType type, float amount)
        {
            float cost = market.GetPrice(type) * amount;
            if (Money < cost) return false;
            Money -= cost;
            AddItem(type, amount);
            market.AddDemand(type, amount);
            return true;
        }

        public bool SellItem(Market market, ResourceType type, float amount)
        {
            if (!RemoveItem(type, amount)) return false;
            Money += market.GetPrice(type) * amount * 0.9f;
            market.AddSupply(type, amount);
            return true;
        }

        // --- Рендер в мировых координатах ---
        public void Render()
        {
            int px = (int)Position.X;
            int py = (int)Position.Y;

            Raylib.DrawEllipse(px, py + 18, 18, 6, new Color(0, 0, 0, 80));
            Raylib.DrawRectangle(px - 14, py - 22, 28, 32, Color.White);
            Raylib.DrawRectangleLines(px - 14, py - 22, 28, 32, Color.Blue);
            Raylib.DrawCircle(px, py - 28, 10, new Color(255, 220, 180, 255));
            Raylib.DrawCircleLines(px, py - 28, 10, Color.DarkBrown);

            // Метка «ВЫ» над игроком
            FontManager.DrawText("ВЫ", px - 14, py - 50, 12, Color.SkyBlue);

            int bw = 36;
            Raylib.DrawRectangle(px - bw / 2, py - 58, bw, 5, Color.DarkGray);
            Raylib.DrawRectangle(px - bw / 2, py - 58, (int)(bw * Health / MaxHealth), 5, Color.Green);

            if (CurrentFactionTerritory > 0 && IsWanted(CurrentFactionTerritory))
                FontManager.DrawText("РАЗЫСКИВАЕТСЯ", px - 50, py - 72, 12, Color.Red);

            if (_attackCooldown > 1.0f / ATTACK_SPEED - 0.15f)
                Raylib.DrawCircleLines(px, py, AttackRange, Color.Red);
        }

        // --- Рендер HUD (экранные координаты) ---
        public void RenderHUD(string factionName, bool isWanted, bool isNight, float dayProgress)
        {
            int sw = Raylib.GetScreenWidth();
            int sh = Raylib.GetScreenHeight();

            // --- Левая панель статов ---
            int panelX = 10, panelY = sh - 160, panelW = 220, panelH = 150;
            Raylib.DrawRectangle(panelX, panelY, panelW, panelH, new Color(0, 0, 0, 190));
            Raylib.DrawRectangleLines(panelX, panelY, panelW, panelH, Color.White);

            DrawBar(panelX + 8, panelY + 8,  204, 16, Health / MaxHealth,  "ЗДОРОВЬЕ", Color.Green);
            DrawBar(panelX + 8, panelY + 30, 204, 16, 1f - Hunger / 100f, "ЕДА",      Color.Orange);
            DrawBar(panelX + 8, panelY + 52, 204, 16, Energy / 100f,       "ЭНЕРГИЯ",  Color.SkyBlue);

            FontManager.DrawText($"Золото: {Money:F0}г",             panelX + 8, panelY + 76,  16, Color.Gold);
            FontManager.DrawText($"Состояние: {GetStateRu(State)}",  panelX + 8, panelY + 96,  13, Color.LightGray);
            FontManager.DrawText($"Поз: {Position.X:F0},{Position.Y:F0}", panelX + 8, panelY + 114, 11, Color.Gray);
            FontManager.DrawText($"Территория: {factionName}",       panelX + 8, panelY + 130, 11, Color.Yellow);

            // --- Wanted ---
            if (isWanted)
            {
                Raylib.DrawRectangle(sw / 2 - 130, 10, 260, 30, new Color(180, 0, 0, 200));
                FontManager.DrawText("! РАЗЫСКИВАЕТСЯ В ЭТОМ РЕГИОНЕ !", sw / 2 - 122, 18, 14, Color.White);
            }

            // --- Индикатор дня/ночи ---
            int clockX = sw - 70, clockY = 10;
            Raylib.DrawCircle(clockX, clockY + 25, 22, isNight ? Color.DarkBlue : Color.Orange);
            Raylib.DrawCircleLines(clockX, clockY + 25, 22, Color.White);
            FontManager.DrawText(isNight ? "НОЧЬ" : "ДЕНЬ", clockX - 18, clockY + 18, 11, Color.White);

            // --- Мини-инвентарь ---
            int invX = sw - 260, invY = 10;
            if (Inventory.Count > 0)
            {
                Raylib.DrawRectangle(invX, invY, 250, 14 + Inventory.Count * 17, new Color(0, 0, 0, 160));
                FontManager.DrawText("ИНВЕНТАРЬ:", invX + 4, invY + 2, 12, Color.Yellow);
                int iy = invY + 16;
                foreach (var kvp in Inventory)
                {
                    if (kvp.Value > 0.01f)
                    {
                        FontManager.DrawText($"  {GetResourceRu(kvp.Key)}: {kvp.Value:F1}", invX + 4, iy, 12, Color.White);
                        iy += 17;
                    }
                }
            }
        }

        private static void DrawBar(int x, int y, int w, int h, float frac, string label, Color color)
        {
            frac = Math.Clamp(frac, 0f, 1f);
            Raylib.DrawRectangle(x, y, w, h, Color.DarkGray);
            Raylib.DrawRectangle(x, y, (int)(w * frac), h, color);
            Raylib.DrawRectangleLines(x, y, w, h, Color.Black);
            FontManager.DrawText(label, x + 3, y + 2, 11, Color.White);
        }

        private static string GetStateRu(PlayerState state) => state switch
        {
            PlayerState.Idle     => "Стоит",
            PlayerState.Moving   => "Идёт",
            PlayerState.Combat   => "Бой",
            PlayerState.Sleeping => "Спит",
            PlayerState.Trading  => "Торгует",
            _                    => state.ToString()
        };

        private static string GetResourceRu(ResourceType type) => type switch
        {
            ResourceType.Iron    => "Железо",
            ResourceType.Copper  => "Медь",
            ResourceType.Gold    => "Золото",
            ResourceType.Food    => "Еда",
            ResourceType.Wood    => "Дерево",
            ResourceType.Stone   => "Камень",
            ResourceType.Weapons => "Оружие",
            ResourceType.Tools   => "Инструменты",
            ResourceType.Luxury  => "Предметы роскоши",
            _                    => type.ToString()
        };
    }
}

