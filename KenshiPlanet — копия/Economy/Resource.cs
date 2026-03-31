using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using KenshiPlanet.Core;

namespace KenshiPlanet.Economy
{
    /// <summary>
    /// Типы ресурсов для глобальной стратегии
    /// </summary>
    public enum ResourceType
    {
        // Руды
        Iron,
        Copper,
        Gold,
        Silver,
        Lead,
        Zinc,
        Tin,
        Aluminum,
        Uranium,
        
        // Строительные материалы
        Stone,
        Coal,
        Salt,
        
        // Биологические ресурсы
        Wood,
        Food,
        
        // Промышленные товары
        Weapons,
        Tools,
        Luxury,
        Steel,
        Electronics
    }

    public static class ResourceGlobals
    {
        public static readonly Random GlobalRng = new Random();
        
        // Цвета для каждого типа ресурса
        public static readonly Dictionary<ResourceType, Color> ResourceColors = new()
        {
            { ResourceType.Iron,       new Color(128, 128, 128, 255) },     // Серый
            { ResourceType.Copper,     new Color(184, 115, 51, 255) },      // Медный
            { ResourceType.Gold,       new Color(255, 215, 0, 255) },       // Золотой
            { ResourceType.Silver,     new Color(192, 192, 192, 255) },     // Серебряный
            { ResourceType.Lead,       new Color(100, 100, 110, 255) },     // Свинцовый
            { ResourceType.Zinc,       new Color(200, 200, 210, 255) },     // Цинковый
            { ResourceType.Tin,        new Color(210, 180, 140, 255) },     // Оловянный
            { ResourceType.Aluminum,   new Color(220, 220, 230, 255) },     // Алюминиевый
            { ResourceType.Uranium,    new Color(0, 255, 0, 255) },         // Урановый (светящийся)
            { ResourceType.Stone,      new Color(80, 80, 80, 255) },        // Каменный
            { ResourceType.Coal,       new Color(30, 30, 30, 255) },        // Уголь
            { ResourceType.Salt,       new Color(255, 255, 255, 255) },     // Соль
            { ResourceType.Wood,       new Color(101, 67, 33, 255) },       // Древесина
            { ResourceType.Food,       new Color(34, 139, 34, 255) },       // Еда
            { ResourceType.Weapons,    new Color(139, 26, 26, 255) },       // Оружие
            { ResourceType.Tools,      new Color(70, 130, 180, 255) },      // Инструменты
            { ResourceType.Luxury,     new Color(218, 112, 214, 255) },     // Роскошь
            { ResourceType.Steel,      new Color(64, 64, 64, 255) },        // Сталь
            { ResourceType.Electronics,new Color(0, 100, 200, 255) }        // Электроника
        };

        // Базовые цены ресурсов
        public static readonly Dictionary<ResourceType, float> BasePrices = new()
        {
            { ResourceType.Iron,       50.0f   },
            { ResourceType.Copper,     75.0f   },
            { ResourceType.Gold,       500.0f  },
            { ResourceType.Silver,     250.0f  },
            { ResourceType.Lead,       40.0f   },
            { ResourceType.Zinc,       60.0f   },
            { ResourceType.Tin,        80.0f   },
            { ResourceType.Aluminum,   90.0f   },
            { ResourceType.Uranium,    1000.0f },
            { ResourceType.Stone,      30.0f   },
            { ResourceType.Coal,       45.0f   },
            { ResourceType.Salt,       20.0f   },
            { ResourceType.Wood,       25.0f   },
            { ResourceType.Food,       10.0f   },
            { ResourceType.Weapons,    200.0f  },
            { ResourceType.Tools,      100.0f  },
            { ResourceType.Luxury,     1000.0f },
            { ResourceType.Steel,      150.0f  },
            { ResourceType.Electronics,300.0f  }
        };

        // Русские названия
        public static readonly Dictionary<ResourceType, string> RussianNames = new()
        {
            { ResourceType.Iron,       "Железо"    },
            { ResourceType.Copper,     "Медь"      },
            { ResourceType.Gold,       "Золото"    },
            { ResourceType.Silver,     "Серебро"   },
            { ResourceType.Lead,       "Свинец"    },
            { ResourceType.Zinc,       "Цинк"      },
            { ResourceType.Tin,        "Олово"     },
            { ResourceType.Aluminum,   "Алюминий"  },
            { ResourceType.Uranium,    "Уран"      },
            { ResourceType.Stone,      "Камень"    },
            { ResourceType.Coal,       "Уголь"     },
            { ResourceType.Salt,       "Соль"      },
            { ResourceType.Wood,       "Древесина" },
            { ResourceType.Food,       "Еда"       },
            { ResourceType.Weapons,    "Оружие"    },
            { ResourceType.Tools,      "Инстр."    },
            { ResourceType.Luxury,     "Роскошь"   },
            { ResourceType.Steel,      "Сталь"     },
            { ResourceType.Electronics,"Электрон." }
        };

        // Категории ресурсов
        public static readonly Dictionary<ResourceType, string> ResourceCategories = new()
        {
            { ResourceType.Iron,       "Ore"      },
            { ResourceType.Copper,     "Ore"      },
            { ResourceType.Gold,       "Ore"      },
            { ResourceType.Silver,     "Ore"      },
            { ResourceType.Lead,       "Ore"      },
            { ResourceType.Zinc,       "Ore"      },
            { ResourceType.Tin,        "Ore"      },
            { ResourceType.Aluminum,   "Ore"      },
            { ResourceType.Uranium,    "Ore"      },
            { ResourceType.Stone,      "Material" },
            { ResourceType.Coal,       "Material" },
            { ResourceType.Salt,       "Material" },
            { ResourceType.Wood,       "Biomass"  },
            { ResourceType.Food,       "Biomass"  },
            { ResourceType.Weapons,    "Product"  },
            { ResourceType.Tools,      "Product"  },
            { ResourceType.Luxury,     "Product"  },
            { ResourceType.Steel,      "Product"  },
            { ResourceType.Electronics,"Product"  }
        };
    }

    /// <summary>
    /// Узел ресурса - процедурно сгенерированная точка добычи
    /// </summary>
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
        
        // Для деревьев - размер/возраст
        public float Size { get; set; } = 1.0f;
        
        // Время последней добычи этим игроком
        public float LastMinedTime { get; set; } = -1000f;
        
        // Рабочие, назначенные на этот узел
        public int WorkersAssigned { get; set; } = 0;
        
        // Максимальное количество рабочих
        public int MaxWorkers => Type == ResourceType.Wood ? 3 : 5;

        public ResourceNode(int id, string name, Vector2 position, ResourceType type, float amount = 10000f)
        {
            Id = id; 
            Name = name; 
            Position = position; 
            Type = type;
            Amount = amount;
            MaxAmount = amount;
            
            // Размер для деревьев зависит от типа
            if (type == ResourceType.Wood)
                Size = 0.8f + (float)(ResourceGlobals.GlobalRng.NextDouble() * 0.4);
        }

        /// <summary>
        /// Добыча ресурса за указанное время
        /// </summary>
        public float Extract(float deltaTime)
        {
            if (!IsActive || Amount <= 0) return 0f;
            
            // Скорость добычи: 1 единица за 10 секунд = 0.1 ед/сек
            float extractRate = 0.1f;
            float toExtract = deltaTime * extractRate;
            float extracted = Math.Min(toExtract, Amount);
            
            Amount -= extracted;
            
            if (Amount <= 0)
            {
                IsActive = false;
                WorkersAssigned = 0;
            }
            
            return extracted;
        }

        public void Render()
        {
            if (!IsActive && Amount <= 0) return;
            
            Color nodeColor = ResourceGlobals.ResourceColors.TryGetValue(Type, out var c) 
                ? c 
                : Color.White;

            // Размер зависит от типа и количества
            float baseSize = Type == ResourceType.Wood ? 20f : 15f;
            float size = baseSize * Size * (0.5f + 0.5f * (Amount / MaxAmount));
            
            // Отрисовка узла
            if (Type == ResourceType.Wood)
            {
                // Дерево - рисуем как круг с "стволом"
                Raylib.DrawCircleV(Position, size * 0.7f, nodeColor);
                Raylib.DrawRectangle(
                    (int)Position.X - (int)(size * 0.15f),
                    (int)Position.Y + (int)(size * 0.5f),
                    (int)(size * 0.3f),
                    (int)(size * 0.8f),
                    new Color(60, 40, 20, 255));
            }
            else
            {
                // Руда - кристаллическая форма
                Raylib.DrawCircleV(Position, size, nodeColor);
                Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, (int)size, Color.Black);
                
                // Блик для руд
                if (Type == ResourceType.Gold || Type == ResourceType.Silver || Type == ResourceType.Uranium)
                {
                    Raylib.DrawCircle(
                        (int)(Position.X - size * 0.3f),
                        (int)(Position.Y - size * 0.3f),
                        size * 0.2f,
                        new Color(255, 255, 255, 150));
                }
            }

            // Индикатор истощения
            if (Amount < MaxAmount * 0.1f && IsActive)
                FontManager.DrawText("!", (int)Position.X - 5, (int)Position.Y - (int)size - 15, 16, Color.Red);
        }

        public void Replenish(float amount)
        {
            Amount = Math.Min(Amount + amount, MaxAmount);
            if (Amount > 0) IsActive = true;
        }
        
        public float GetRemainingPercent() => Amount / MaxAmount;
    }

    /// <summary>
    /// Состояние добычи ресурса игроком
    /// </summary>
    public class MiningSession
    {
        public ResourceNode? TargetNode { get; set; }
        public float Progress { get; set; } = 0f;
        public float TimeSpent { get; set; } = 0f;
        public bool IsMining { get; set; } = false;
        
        // 1 единица ресурса за 10 секунд
        public const float UNITS_PER_SECOND = 0.1f;
        public const float SECONDS_PER_UNIT = 10f;
        
        public void Start(ResourceNode node)
        {
            TargetNode = node;
            Progress = 0f;
            TimeSpent = 0f;
            IsMining = true;
        }
        
        public void Update(float deltaTime)
        {
            if (!IsMining || TargetNode == null || !TargetNode.IsActive) return;
            
            TimeSpent += deltaTime;
            Progress = TimeSpent / SECONDS_PER_UNIT;
            
            if (Progress >= 1f)
            {
                Progress = 0f;
                TimeSpent = 0f;
            }
        }
        
        public void Stop()
        {
            IsMining = false;
            Progress = 0f;
            TimeSpent = 0f;
            TargetNode = null;
        }
        
        public bool CanExtract() => Progress >= 1f;
        
        public float ExtractUnit()
        {
            if (!CanExtract() || TargetNode == null) return 0f;
            
            float extracted = Math.Min(1f, TargetNode.Amount);
            TargetNode.Amount -= extracted;
            
            if (TargetNode.Amount <= 0)
            {
                TargetNode.IsActive = false;
                Stop();
            }
            else
            {
                Progress = 0f;
                TimeSpent = 0f;
            }
            
            return extracted;
        }
    }
}
