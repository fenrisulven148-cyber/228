using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using KenshiPlanet.Core;

namespace KenshiPlanet.Economy
{
    public static class ResourceGlobals
    {
        public static readonly Random GlobalRng = new Random();
    }

    public class ResourceNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Vector2 Position { get; set; }
        public ResourceType Type { get; set; }
        public float Amount { get; set; } = 10000.0f;
        public float MaxAmount { get; set; } = 10000.0f;
        public int OwnerFactionId { get; set; } = -1;
        public bool IsActive { get; set; } = true;

        public float ExtractionRate { get; set; } = 10.0f;
        public int WorkersAssigned { get; set; } = 0;
        public int MaxWorkers { get; set; } = 20;

        public ResourceNode(int id, string name, Vector2 position, ResourceType type)
        {
            Id = id; Name = name; Position = position; Type = type;
        }

        public float Extract(float deltaTime)
        {
            if (!IsActive || Amount <= 0 || WorkersAssigned <= 0) return 0.0f;
            float extracted = Math.Min(ExtractionRate * WorkersAssigned * deltaTime, Amount);
            Amount -= extracted;
            if (Amount <= 0) IsActive = false;
            return extracted;
        }

        public void Render()
        {
            Color nodeColor = Type switch
            {
                ResourceType.Iron   => Color.Gray,
                ResourceType.Copper => Color.Orange,
                ResourceType.Gold   => Color.Gold,
                ResourceType.Food   => Color.Green,
                ResourceType.Wood   => Color.Brown,
                ResourceType.Stone  => Color.DarkGray,
                _                   => Color.White
            };

            float size = 15.0f + (Amount / MaxAmount) * 10.0f;
            Raylib.DrawCircleV(Position, size, nodeColor);
            Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, (int)size, Color.Black);

            if (!IsActive)
                FontManager.DrawText("X", (int)Position.X - 5, (int)Position.Y - 8, 16, Color.Red);

            if (WorkersAssigned > 0)
                FontManager.DrawText($"{WorkersAssigned}", (int)Position.X - 5, (int)Position.Y - 20, 12, Color.White);
        }

        public void Replenish(float amount)
        {
            Amount = Math.Min(Amount + amount, MaxAmount);
            if (Amount > 0) IsActive = true;
        }
    }

    public enum ResourceType
    {
        Iron, Copper, Gold, Food, Wood, Stone, Weapons, Tools, Luxury
    }

    public class Market
    {
        public int SettlementId { get; set; }
        public Dictionary<ResourceType, float> Prices { get; set; } = new();
        public Dictionary<ResourceType, float> Supply { get; set; } = new();
        public Dictionary<ResourceType, float> Demand { get; set; } = new();

        private static readonly Dictionary<ResourceType, string> _ruNames = new()
        {
            { ResourceType.Iron,    "Железо"  },
            { ResourceType.Copper,  "Медь"    },
            { ResourceType.Gold,    "Золото"  },
            { ResourceType.Food,    "Еда"     },
            { ResourceType.Wood,    "Дерево"  },
            { ResourceType.Stone,   "Камень"  },
            { ResourceType.Weapons, "Оружие"  },
            { ResourceType.Tools,   "Инстр."  },
            { ResourceType.Luxury,  "Роскошь" }
        };

        private readonly Dictionary<ResourceType, float> _basePrices = new()
        {
            { ResourceType.Iron,    50.0f   },
            { ResourceType.Copper,  75.0f   },
            { ResourceType.Gold,    500.0f  },
            { ResourceType.Food,    10.0f   },
            { ResourceType.Wood,    25.0f   },
            { ResourceType.Stone,   30.0f   },
            { ResourceType.Weapons, 200.0f  },
            { ResourceType.Tools,   100.0f  },
            { ResourceType.Luxury,  1000.0f }
        };

        public Market(int settlementId)
        {
            SettlementId = settlementId;
            InitializePrices();
        }

        private void InitializePrices()
        {
            foreach (var kvp in _basePrices)
            {
                Prices[kvp.Key] = kvp.Value;
                Supply[kvp.Key] = 100.0f;
                Demand[kvp.Key] = 100.0f;
            }
        }

        public void UpdatePrices(float deltaTime)
        {
            foreach (var resource in _basePrices.Keys)
            {
                float ratio = Supply[resource] / (Demand[resource] + 1);
                float targetPrice = _basePrices[resource] * (1.0f / ratio);
                Prices[resource] += (targetPrice - Prices[resource]) * 0.01f * deltaTime;
                Prices[resource] = Math.Max(Prices[resource], _basePrices[resource] * 0.1f);
                Prices[resource] = Math.Min(Prices[resource], _basePrices[resource] * 10.0f);
            }
        }

        public float GetPrice(ResourceType resource) =>
            Prices.TryGetValue(resource, out var price) ? price : 0.0f;

        public void AddSupply(ResourceType resource, float amount)
        { if (Supply.ContainsKey(resource)) Supply[resource] += amount; }

        public void AddDemand(ResourceType resource, float amount)
        { if (Demand.ContainsKey(resource)) Demand[resource] += amount; }

        public void Render(Vector2 position)
        {
            int yOffset = 0;
            foreach (var kvp in Prices)
            {
                string name = _ruNames.TryGetValue(kvp.Key, out var n) ? n : kvp.Key.ToString();
                string text = $"{name}: {kvp.Value:F1}г";
                FontManager.DrawText(text, (int)position.X, (int)position.Y + yOffset, 12, Color.White);
                yOffset += 15;
            }
        }
    }
}
