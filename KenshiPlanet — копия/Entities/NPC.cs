using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KenshiPlanet.World;
using KenshiPlanet.Economy;

namespace KenshiPlanet.Entities
{
    public class NPC
    {
        // --- Базовые свойства ---
        public int     Id        { get; set; }
        public string  Name      { get; set; } = string.Empty;
        public Vector2 Position  { get; set; }
        public int     FactionId { get; set; }
        public NPCState State    { get; set; } = NPCState.Idle;
        public float Health      { get; set; } = 100.0f;
        public float MaxHealth   { get; set; } = 100.0f;
        public float Speed       { get; set; } = 200.0f;
        public float DefenseRating { get; set; } = 3.0f; // броня/защита

        // --- Цели ИИ ---
        public Vector2? TargetPosition { get; set; }
        public NPC?     TargetEntity   { get; set; }

        // --- Патруль ---
        public Vector2 PatrolCenter { get; set; }
        public float   PatrolRadius { get; set; } = 500.0f;

        // --- Потребности ---
        public float Hunger    { get; set; } = 0.0f;
        public float Energy    { get; set; } = 100.0f;
        public float Happiness { get; set; } = 50.0f;

        // --- Работа ---
        public NPCJob     Job             { get; set; } = NPCJob.Unemployed;
        public ResourceNode? WorkTarget   { get; set; }
        public Settlement?   HomeSettlement { get; set; }
        public Settlement?   TradeTarget  { get; set; }   // для торговца
        public float Wage { get; set; } = 5.0f;

        // --- Инвентарь ---
        public Dictionary<ResourceType, float> Inventory { get; set; } = new();
        public float CarryCapacity { get; set; } = 500.0f;

        // --- Деньги ---
        public float Money { get; set; } = 50.0f;

        // --- Боевые параметры ---
        public float AttackDamage   { get; set; } = 10.0f;
        public float AttackRange    { get; set; } = 40.0f;
        private float _attackCooldown = 0.0f;

        // --- Оптимизация ---
        public bool IsSimplified { get; set; } = false;

        // --- Таймеры ---
        private float _idleTimer    = 0.0f;
        private float _workTimer    = 0.0f;
        private float _tradeTimer   = 0.0f;

        private static readonly Random _rng = new Random();

        public NPC(int id, string name, Vector2 position, int factionId)
        {
            Id        = id;
            Name      = name;
            Position  = position;
            FactionId = factionId;
            PatrolCenter = position;
            Money     = 20.0f + (float)_rng.NextDouble() * 80.0f;
            
            // Лидер фракции получает больше HP и защиты
            if (Job == NPCJob.Leader)
            {
                Health = 200.0f;
                MaxHealth = 200.0f;
                DefenseRating = 15.0f;
            }
        }

        // ============================
        //  АПДЕЙТ (полный)
        // ============================
        public void Update(double deltaTime)
        {
            if (IsSimplified)
            {
                UpdateSimplified(deltaTime);
                return;
            }

            UpdateNeeds(deltaTime);

            if (_attackCooldown > 0) _attackCooldown -= (float)deltaTime;

            switch (State)
            {
                case NPCState.Idle:          UpdateIdle(deltaTime);          break;
                case NPCState.Patrol:         UpdatePatrol(deltaTime);        break;
                case NPCState.MoveToPosition: UpdateMoveToPosition(deltaTime); break;
                case NPCState.Follow:         UpdateFollow(deltaTime);        break;
                case NPCState.Sleeping:       UpdateSleeping(deltaTime);      break;
                case NPCState.Working:        UpdateWorking(deltaTime);       break;
                case NPCState.Trading:        UpdateTrading(deltaTime);       break;
                case NPCState.Eating:         UpdateEating(deltaTime);        break;
            }
        }

        // Упрощённый апдейт (далеко от камеры)
        public void UpdateSimplified(double deltaTime)
        {
            UpdateNeeds(deltaTime);
            
            // Статическая работа без движения
            if (Job == NPCJob.Miner && WorkTarget != null && WorkTarget.IsActive)
            {
                float extracted = WorkTarget.Extract((float)deltaTime * 0.1f);
                AddToInventory(WorkTarget.Type, extracted);
            }
        }

        // ============================
        //  ПОТРЕБНОСТИ
        // ============================
        private void UpdateNeeds(double deltaTime)
        {
            float dt = (float)deltaTime;

            Hunger += (Job != NPCJob.Unemployed ? 0.5f : 0.3f) * dt;
            if (Hunger >= 100.0f)
            {
                Health -= 0.8f * dt;
                Hunger = 100.0f;
            }

            // Еда из инвентаря если голоден
            if (Hunger > 70.0f && Inventory.TryGetValue(ResourceType.Food, out var food) && food > 0.5f)
            {
                float eat = Math.Min(food, 10.0f * dt);
                Inventory[ResourceType.Food] -= eat;
                Hunger -= eat * 5.0f;
            }

            Energy -= (State == NPCState.Sleeping ? -3.0f : 0.1f) * dt;
            Energy = Math.Clamp(Energy, 0.0f, 100.0f);

            if (Energy <= 0.0f) { State = NPCState.Sleeping; }
            if (Energy >= 90.0f && State == NPCState.Sleeping) State = NPCState.Idle;

            Happiness = Math.Clamp(
                Happiness + (Hunger < 50 && Energy > 30 ? 0.02f : -0.05f) * dt,
                0.0f, 100.0f);
        }

        // ============================
        //  СОСТОЯНИЯ ИИ
        // ============================
        private void UpdateIdle(double deltaTime)
        {
            _idleTimer += (float)deltaTime;
            if (_idleTimer < 2.0f) return;
            _idleTimer = 0.0f;

            switch (Job)
            {
                case NPCJob.Miner:
                    if (WorkTarget != null && WorkTarget.IsActive)
                    {
                        TargetPosition = WorkTarget.Position;
                        State = NPCState.MoveToPosition;
                    }
                    break;

                case NPCJob.Merchant:
                    if (TradeTarget != null)
                    {
                        TargetPosition = TradeTarget.Position;
                        State = NPCState.MoveToPosition;
                    }
                    else
                    {
                        State = NPCState.Patrol;
                    }
                    break;

                case NPCJob.Soldier:
                case NPCJob.Guard:
                    State = NPCState.Patrol;
                    break;

                case NPCJob.Blacksmith:
                    State = NPCState.Working;
                    break;

                default:
                    if (_rng.NextDouble() < 0.3)
                        State = NPCState.Patrol;
                    break;
            }
        }

        private void UpdatePatrol(double deltaTime)
        {
            if (!TargetPosition.HasValue || Vector2.Distance(Position, TargetPosition.Value) < 30.0f)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2);
                float dist  = (float)(_rng.NextDouble() * PatrolRadius);
                TargetPosition = new Vector2(
                    PatrolCenter.X + (float)Math.Cos(angle) * dist,
                    PatrolCenter.Y + (float)Math.Sin(angle) * dist
                );
            }
            MoveTowards(TargetPosition.Value, deltaTime);

            // Добытчик дошёл до ресурса — начинаем работу
            if (Job == NPCJob.Miner && WorkTarget != null
                && Vector2.Distance(Position, WorkTarget.Position) < 50.0f)
            {
                State = NPCState.Working;
            }
        }

        private void UpdateMoveToPosition(double deltaTime)
        {
            if (!TargetPosition.HasValue) { State = NPCState.Idle; return; }

            if (Vector2.Distance(Position, TargetPosition.Value) < 20.0f)
            {
                // Прибыли
                switch (Job)
                {
                    case NPCJob.Miner:
                        if (WorkTarget != null && Vector2.Distance(Position, WorkTarget.Position) < 50.0f)
                            State = NPCState.Working;
                        else
                            State = NPCState.Idle;
                        break;

                    case NPCJob.Merchant:
                        State = NPCState.Trading;
                        break;

                    default:
                        State = NPCState.Idle;
                        break;
                }
                TargetPosition = null;
            }
            else
            {
                MoveTowards(TargetPosition.Value, deltaTime);
            }
        }

        private void UpdateFollow(double deltaTime)
        {
            if (TargetEntity != null)
            {
                TargetPosition = TargetEntity.Position;
                MoveTowards(TargetPosition.Value, deltaTime);
            }
        }

        private void UpdateSleeping(double deltaTime)
        {
            // Потребности обновляет UpdateNeeds
        }

        private void UpdateWorking(double deltaTime)
        {
            _workTimer += (float)deltaTime;

            if (Job == NPCJob.Miner && WorkTarget != null)
            {
                if (!WorkTarget.IsActive) { Job = NPCJob.Unemployed; State = NPCState.Idle; return; }

                if (_workTimer >= 1.0f)
                {
                    _workTimer = 0.0f;
                    float extracted = WorkTarget.Extract(1.0f);
                    if (extracted > 0)
                    {
                        AddToInventory(WorkTarget.Type, extracted);
                        // Инвентарь полный — продать / принести домой
                        if (GetInventoryWeight() > CarryCapacity * 0.8f)
                        {
                            State = NPCState.Idle;
                            if (HomeSettlement != null)
                            {
                                TargetPosition = HomeSettlement.Position;
                                State = NPCState.MoveToPosition;
                            }
                        }
                    }
                    else
                    {
                        // Ресурс исчерпан
                        WorkTarget.WorkersAssigned--;
                        WorkTarget = null;
                        Job = NPCJob.Unemployed;
                        State = NPCState.Idle;
                    }
                }
            }
            else if (Job == NPCJob.Blacksmith)
            {
                // Кузнец обрабатывает материалы в оружие
                if (_workTimer >= 5.0f)
                {
                    _workTimer = 0.0f;
                    float mats = GetInventoryAmount(ResourceType.Iron);
                    if (mats >= 5.0f)
                    {
                        Inventory[ResourceType.Iron] -= 5.0f;
                        AddToInventory(ResourceType.Weapons, 1.0f);
                    }
                    else
                    {
                        State = NPCState.Idle; // нет материала
                    }
                }
            }
            else
            {
                State = NPCState.Idle;
            }
        }

        private void UpdateTrading(double deltaTime)
        {
            _tradeTimer += (float)deltaTime;
            if (_tradeTimer < 3.0f) return; // задержка торговли
            _tradeTimer = 0.0f;

            // Продаём всё что несём в HomeSettlement
            foreach (var kvp in Inventory.ToList())
            {
                if (kvp.Value > 0.1f)
                {
                    // Получаем деньги за товар (упрощённо)
                    float price = GetBasePrice(kvp.Key) * 0.7f; // продаём дешевле
                    Money += price * kvp.Value;
                    Inventory[kvp.Key] = 0;
                }
            }

            State = NPCState.Idle;
            TradeTarget = null; // Следующая цель будет выбрана в Idle
        }

        private void UpdateEating(double deltaTime)
        {
            // Потребности обновляет UpdateNeeds
        }

        // ============================
        //  ПОИСК РАБОТЫ
        // ============================
        public void FindJob(List<ResourceNode> resources, List<Settlement> settlements)
        {
            if (Job != NPCJob.Unemployed) return;

            // 1. Ищем ближайший ресурс с вакансией
            ResourceNode? best = null;
            float bestDist = 150000.0f; // максимальная дальность поиска

            foreach (var r in resources)
            {
                if (!r.IsActive || r.WorkersAssigned >= r.MaxWorkers) continue;
                float d = Vector2.Distance(Position, r.Position);
                if (d < bestDist) { bestDist = d; best = r; }
            }

            if (best != null)
            {
                WorkTarget = best;
                Job = best.Type == ResourceType.Iron || best.Type == ResourceType.Copper
                    ? NPCJob.Miner
                    : NPCJob.Farmer;
                best.WorkersAssigned++;
                TargetPosition = best.Position;
                State = NPCState.MoveToPosition;
                return;
            }

            // 2. Если нет ресурсов — случайная работа
            Job = (NPCJob)(_rng.Next(1, 8));
            State = NPCState.Idle;
        }

        // ============================
        //  БОЕВЫЕ МЕТОДЫ
        // ============================
        public bool CanAttack() => _attackCooldown <= 0 && Health > 0;

        public void AttackTarget(NPC target)
        {
            if (!CanAttack()) return;
            float dist = Vector2.Distance(Position, target.Position);
            if (dist > AttackRange) return;

            float dmg = Math.Max(0, AttackDamage - target.DefenseRating);
            target.Health -= dmg;
            _attackCooldown = 1.0f;
            State = NPCState.Combat;
        }

        // ============================
        //  ИНВЕНТАРЬ
        // ============================
        private void AddToInventory(ResourceType type, float amount)
        {
            if (!Inventory.ContainsKey(type)) Inventory[type] = 0;
            Inventory[type] += amount;
        }

        private float GetInventoryAmount(ResourceType type) =>
            Inventory.TryGetValue(type, out var v) ? v : 0.0f;

        private float GetInventoryWeight() =>
            Inventory.Values.Sum();

        private static readonly Dictionary<ResourceType, float> _basePrices = new()
        {
            { ResourceType.Iron,    50.0f },
            { ResourceType.Copper,  75.0f },
            { ResourceType.Gold,   500.0f },
            { ResourceType.Food,    10.0f },
            { ResourceType.Wood,    25.0f },
            { ResourceType.Stone,   30.0f },
            { ResourceType.Weapons,200.0f },
            { ResourceType.Tools,  100.0f },
            { ResourceType.Luxury, 1000.0f }
        };

        private static float GetBasePrice(ResourceType type) =>
            _basePrices.TryGetValue(type, out var p) ? p : 10.0f;

        // ============================
        //  ДВИЖЕНИЕ
        // ============================
        private void MoveTowards(Vector2 target, double deltaTime)
        {
            Vector2 diff = target - Position;
            if (diff == Vector2.Zero) return;
            Position += Vector2.Normalize(diff) * Speed * (float)deltaTime;
        }

        // ============================
        //  РЕНДЕР
        // ============================
        public void Render(Color factionColor)
        {
            int px = (int)Position.X;
            int py = (int)Position.Y;

            // Цвет по профессии
            Color bodyColor = Job switch
            {
                NPCJob.Soldier    => new Color(200, 50, 50, 255),
                NPCJob.Guard      => new Color(50, 50, 200, 255),
                NPCJob.Merchant   => new Color(200, 180, 50, 255),
                NPCJob.Blacksmith => new Color(100, 100, 100, 255),
                NPCJob.Farmer     => new Color(50, 180, 50, 255),
                NPCJob.Miner      => new Color(150, 100, 50, 255),
                NPCJob.Leader     => new Color(255, 215, 0, 255),
                _                 => factionColor
            };

            // Тело
            Raylib.DrawCircleV(Position, 8.0f, bodyColor);
            Raylib.DrawCircleLines(px, py, 8, Color.Black);

            // Символ профессии
            string symbol = Job switch
            {
                NPCJob.Soldier    => "S",
                NPCJob.Guard      => "G",
                NPCJob.Merchant   => "M",
                NPCJob.Blacksmith => "B",
                NPCJob.Farmer     => "F",
                NPCJob.Miner      => "i",
                NPCJob.Leader     => "L",
                _                 => "."
            };
            Raylib.DrawText(symbol, px - 4, py - 6, 12, Color.White);

            // Индикатор состояния
            Color stateColor = State switch
            {
                NPCState.Combat      => Color.Red,
                NPCState.Working     => Color.Yellow,
                NPCState.Trading     => Color.Gold,
                NPCState.Sleeping    => Color.DarkBlue,
                NPCState.MoveToPosition => Color.SkyBlue,
                _                    => Color.DarkGray
            };
            Raylib.DrawCircleV(Position + new Vector2(6, -6), 3.0f, stateColor);

            // Полоса HP (показывать только если ранен)
            if (Health < MaxHealth)
            {
                int bw = 20;
                Raylib.DrawRectangle(px - bw / 2, py - 14, bw, 3, Color.Red);
                Raylib.DrawRectangle(px - bw / 2, py - 14, (int)(bw * Health / MaxHealth), 3, Color.Green);
            }
        }

        public override string ToString() =>
            $"{Name} [{Job}] HP:{Health:F0} Hunger:{Hunger:F0} Money:{Money:F0}g";
    }

    // ============================
    //  ПЕРЕЧИСЛЕНИЯ
    // ============================
    public enum NPCState
    {
        Idle,
        Patrol,
        MoveToPosition,
        Follow,
        Combat,
        Flee,
        Sleeping,
        Working,
        Trading,
        Eating
    }

    public enum NPCJob
    {
        Unemployed,
        Miner,
        Farmer,
        Merchant,
        Soldier,
        Guard,
        Worker,
        Doctor,
        Blacksmith,  // ← NEW: кузнец, перерабатывает ресурсы в оружие
        Leader       // ← NEW: лидер фракции
    }
}
