
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KenshiPlanet.Economy;
using KenshiPlanet.Entities;
using KenshiPlanet.Factions;

namespace KenshiPlanet.World
{
    public class WorldManager : IDisposable
    {
        // --- Чанки ---
        private const int CHUNK_SIZE   = 5000;
        private readonly Dictionary<long, Chunk> _loadedChunks = new();
        private readonly int _viewDistance = 5;

        // --- Зоны симуляции ---
        public const float ACTIVE_ZONE_RADIUS     = 15000.0f;
        public const float SIMPLIFIED_ZONE_RADIUS = 60000.0f;

        // --- Масштаб мира ---
        public const float WORLD_RADIUS   = 200000.0f;
        public const float FACTION_RADIUS = 100000.0f;

        // -------------------------------------------------------
        // Параметры генерации территорий
        // -------------------------------------------------------
        private const int   GRID_COLS        = 4;   // 4 × 5 = 20 территорий
        private const int   GRID_ROWS        = 5;
        private const int   LLOYD_ITERATIONS = 12;  // итераций центроидной релаксации Ллойда

        // Минимальные расстояния между поселениями (глобально, между всеми фракциями)
        private const float CITY_MIN_DIST    = 11000f;
        private const float VILLAGE_MIN_DIST = 5500f;

        // Количество поселений на фракцию
        private const int CITIES_PER_FACTION   = 20;
        private const int VILLAGES_PER_FACTION = 50;

        // --- Сид ---
        private readonly int _seed;

        // --- RNG для Update (Fix: не создаём new Random() каждый кадр) ---
        private readonly Random _updateRng;

        // --- Цикл дня/ночи ---
        private double _dayTimer = 0.0;
        private const double DAY_DURATION = 300.0; // 5 минут реального времени = 1 игровой день
        public bool  IsNight        { get; private set; }
        public float DayProgress    { get; private set; }  // 0..1 в пределах суток
        public float NightIntensity { get; private set; }  // 0=полдень, 1=полночь (для оверлея)

        // --- Данные мира ---
        public Dictionary<int, Faction>      Factions  { get; private set; } = new();
        public Dictionary<int, NPC>          NPCs      { get; private set; } = new();
        public Dictionary<int, ResourceNode> Resources { get; private set; } = new();
        public List<Market>                  Markets   { get; private set; } = new();
        public TerritoryManager              TerritoryManager { get; private set; } = null!;

        // --- Кэши ---
        private readonly List<NPC>          _activeNPCs         = new();
        private readonly List<Settlement>   _visibleSettlements  = new();
        private readonly List<ResourceNode> _visibleResources    = new();
        public int ActiveNPCCount => _activeNPCs.Count;

        // --- Таймеры ---
        private double _marketTimer    = 0.0;
        private const double MARKET_INTERVAL = 1.0;
        private double _factionAITimer = 0.0;
        private const double FACTION_AI_INTERVAL = 5.0;

        // --- Счётчики ID ---
        private int _nextFactionId    = 1;
        private int _nextNpcId        = 1;
        private int _nextSettlementId = 1;
        private int _nextResourceId   = 1;

        private static readonly string[] FactionNames =
        {
            "Синий Альянс", "Красная Империя", "Зеленая Республика",
            "Северный Клан", "Восточный Синдикат", "Пустынные Разбойники",
            "Прибрежная Республика", "Техно-Охотники", "Горные Кланы",
            "Кровавые Наездники", "Священная Империя", "Плывущие Ниндзя",
            "Голодные Бандиты", "Наёмнический Союз", "Железное Братство",
            "Центральная Доминия", "Свободные Города", "Царство Шэнь",
            "Южные Королевства", "Западная Федерация"
        };

        public WorldManager()
        {
            _seed      = Environment.TickCount;
            _updateRng = new Random(_seed + 1); // отдельный RNG для Update, не пересоздаётся каждый кадр
            InitializeWorld();
        }

        // ================================================================
        //  ИНИЦИАЛИЗАЦИЯ  (многоуровневый детерминированный пайплайн)
        // ================================================================
        private void InitializeWorld()
        {
            var rng = new Random(_seed);

            // ШАГ 1: Генерируем 20 органических равновеликих территорий
            //        через алгоритм Ллойда (Voronoi + центроидная релаксация)
            var territories = GenerateOrganicTerritories(rng);  // List[20] полигонов

            // ШАГ 2: Создаём фракции; столица каждой = центроид территории
            CreateFactions(rng, territories);

            // ШАГ 3: Размещаем поселения внутри полигонов без глобального наложения
            //        globalPoints — общий список занятых позиций по всем фракциям
            var globalPoints = new List<Vector2>(
                Factions.Values.Select(f => f.CapitalPosition));

            foreach (var faction in Factions.Values)
            {
                int idx = faction.Id - 1;                    // factionId начинается с 1
                var territory = territories[idx];
                GenerateSettlementsInTerritory(rng, faction, territory, globalPoints);
            }

            // ШАГ 4: TerritoryManager получает готовые полигоны (не пересчитывает)
            var factionTerritories = Factions.ToDictionary(
                kv => kv.Key,
                kv => territories[kv.Key - 1]);
            TerritoryManager = new TerritoryManager(Factions, factionTerritories);

            // ШАГ 5: Остальная генерация
            GenerateResources(rng);
            GenerateMarkets();
            GenerateNPCs(rng);
            GenerateFactionLeaders(rng);
            SetInitialRelations(rng);
        }

        // ================================================================
        //  УРОВЕНЬ 0 — органические равновеликие территории (алгоритм Ллойда)
        // ================================================================

        /// <summary>
        /// Создаёт 20 полигонов-территорий, похожих на страны Африки:
        /// равные по площади, но с органичными, нерегулярными границами.
        /// Алгоритм: сетка 4×5 с шумом → итеративная центроидная релаксация Ллойда.
        /// </summary>
        private List<List<Vector2>> GenerateOrganicTerritories(Random rng)
        {
            float minX = -WORLD_RADIUS, maxX = WORLD_RADIUS;
            float minY = -WORLD_RADIUS, maxY = WORLD_RADIUS;
            float cellW = (maxX - minX) / GRID_COLS;
            float cellH = (maxY - minY) / GRID_ROWS;
            
            // Начальные сиды: равномерная сетка + минимальный шум
            var seeds = new List<Vector2>(20);
            float margin = WORLD_RADIUS * 0.1f; // Уменьшаем отступ
            
            for (int row = 0; row < GRID_ROWS; row++)
            {
                for (int col = 0; col < GRID_COLS; col++)
                {
                    float cx = minX + margin + (col + 0.5f) * (cellW - 2 * margin);
                    float cy = minY + margin + (row + 0.5f) * (cellH - 2 * margin);
                    
                    // Уменьшаем шум до 2% для равномерности
                    float nx = (float)(rng.NextDouble() * 2 - 1) * cellW * 0.02f;
                    float ny = (float)(rng.NextDouble() * 2 - 1) * cellH * 0.02f;
                    
                    seeds.Add(new Vector2(cx + nx, cy + ny));
                }
            }
            
            // Релаксация Ллойда
            for (int iter = 0; iter < LLOYD_ITERATIONS; iter++)
            {
                var polys = ComputeVoronoi(seeds, minX, maxX, minY, maxY);
                var next  = new List<Vector2>(seeds.Count);

                for (int i = 0; i < seeds.Count; i++)
                {
                    next.Add(polys[i].Count >= 3
                        ? ComputeWeightedCentroid(polys[i])
                        : seeds[i]); // вырожденный полигон — оставляем сид
                }

                seeds = next;
            }

            var finalTerritories = ComputeVoronoi(seeds, minX, maxX, minY, maxY);

#if DEBUG
            // Статистика площадей территорий (только в Debug-сборке)
            float totalArea = 0, minArea = float.MaxValue, maxArea = float.MinValue;
            foreach (var t in finalTerritories)
            {
                float area = CalculatePolygonArea(t);
                totalArea += area;
                if (area < minArea) minArea = area;
                if (area > maxArea) maxArea = area;
            }
            float avgArea  = totalArea / finalTerritories.Count;
            float variance = (maxArea - minArea) / avgArea * 100;
            Console.WriteLine($"[World] Территории: avg={avgArea:F0}, разброс={variance:F1}%");
#endif

            return finalTerritories;
        }

        private static float CalculatePolygonArea(List<Vector2> poly)
        {
            if (poly.Count < 3) return 0f;
            
            float area = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                int j = (i + 1) % poly.Count;
                area += poly[i].X * poly[j].Y;
                area -= poly[j].X * poly[i].Y;
            }
            return Math.Abs(area * 0.5f);
        }

        /// <summary>
        /// Строит 20 ячеек Вороного для набора сидов в пределах bbox.
        /// Каждая ячейка — выпуклый полигон, сумма покрывает весь прямоугольник.
        /// </summary>
        private static List<List<Vector2>> ComputeVoronoi(
            List<Vector2> seeds, float minX, float maxX, float minY, float maxY)
        {
            var result = new List<List<Vector2>>(seeds.Count);
            for (int i = 0; i < seeds.Count; i++)
            {
                // ✅ ИСПРАВЛЕНИЕ: ПРАВИЛЬНЫЙ ПРЯМОУГОЛЬНИК
                var poly = new List<Vector2>
                {
                    new(minX, minY),  // левый нижний
                    new(maxX, minY),  // правый нижний
                    new(maxX, maxY),  // правый верхний
                    new(minX, maxY)   // левый верхний ← БЫЛО: new(minY, maxY)
                };

                for (int j = 0; j < seeds.Count; j++)
                {
                    if (i == j) continue;
                    poly = ClipPolygonByBisector(poly, seeds[i], seeds[j]);
                    if (poly.Count < 3) break;
                }
                
                // ✅ ВОССТАНОВЛЕНИЕ: если полигон вырожденный — создаём минимальный
                if (poly.Count < 3)
                {
                    poly = new List<Vector2>
                    {
                        seeds[i] + new Vector2(-1500, -1500),
                        seeds[i] + new Vector2(1500, -1500),
                        seeds[i] + new Vector2(1500, 1500),
                        seeds[i] + new Vector2(-1500, 1500)
                    };
                }
                
                result.Add(poly);
            }
            return result;
        }

        /// <summary>
        /// Обрезает полигон по серединному перпендикуляру между pointA и pointB.
        /// Сохраняет сторону pointA (ближе к нашему сиду).
        /// </summary>
        private static List<Vector2> ClipPolygonByBisector(
            List<Vector2> poly, Vector2 pointA, Vector2 pointB)
        {
            if (poly.Count < 3) return poly;
            
            Vector2 mid = (pointA + pointB) * 0.5f;
            Vector2 dir = Vector2.Normalize(pointB - pointA);  // Направление от A к B
            
            // Дополнительная защита от нулевого направления
            if (float.IsNaN(dir.X) || float.IsNaN(dir.Y))
            {
                dir = new Vector2(1, 0); // безопасное направление по умолчанию
            }
            
            var result = new List<Vector2>();
            
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 current = poly[i];
                Vector2 next = poly[(i + 1) % poly.Count];
                
                // Расстояние от точки до биссектрисы ВДОЛЬ направления
                float dc = Vector2.Dot(current - mid, dir);
                float dn = Vector2.Dot(next - mid, dir);
                
                // Точка current ближе к A, если dc < 0 (ключевое исправление!)
                if (dc <= 0)
                    result.Add(current);
                    
                // Если ребро пересекает биссектрису
                if (dc * dn < 0)
                {
                    float t = dc / (dc - dn);  // Правильный параметр пересечения
                    Vector2 intersection = current + (next - current) * t;
                    result.Add(intersection);
                }
            }
            
            // Защита от вырожденных полигонов
            if (result.Count < 3) 
            {
                // Создаем минимальный полигон вокруг pointA
                float size = 1000f;
                result = new List<Vector2>
                {
                    pointA + new Vector2(-size, -size),
                    pointA + new Vector2(size, -size),
                    pointA + new Vector2(size, size),
                    pointA + new Vector2(-size, size)
                };
            }
            
            return result;
        }

        /// <summary>
        /// Взвешенный центроид выпуклого полигона (через триангуляцию).
        /// ИСПРАВЛЕННАЯ ВЕРСИЯ - использует правильную геометрическую формулу.
        /// </summary>
        private static Vector2 ComputeWeightedCentroid(List<Vector2> poly)
        {
            if (poly.Count < 3) 
                return poly.Count > 0 ? poly[0] : Vector2.Zero;
            
            float area = 0;
            float cx = 0;
            float cy = 0;
            
            for (int i = 0; i < poly.Count; i++)
            {
                int j = (i + 1) % poly.Count;
                float cross = poly[i].X * poly[j].Y - poly[j].X * poly[i].Y;
                area += cross;
                cx += (poly[i].X + poly[j].X) * cross;
                cy += (poly[i].Y + poly[j].Y) * cross;
            }
            
            area *= 0.5f;
            float factor = 1.0f / (6.0f * Math.Abs(area));
            
            return new Vector2(cx * factor, cy * factor);
        }

        // ================================================================
        //  УРОВЕНЬ 1 — столицы фракций (центроид территории)
        // ================================================================
        private void CreateFactions(Random rng, List<List<Vector2>> territories)
        {
            // Определяем типы фракций и их распределение
            var factionTypes = new Dictionary<FactionType, int>
            {
                { FactionType.Military, 3 },
                { FactionType.Economic, 3 },
                { FactionType.Technological, 3 },
                { FactionType.Magical, 2 },
                { FactionType.Agricultural, 2 },
                { FactionType.Nomadic, 2 },
                { FactionType.Religious, 2 },
                { FactionType.Industrial, 2 },
                { FactionType.Stealth, 1 },
                { FactionType.Diplomatic, 2 }
            };
            
            var availableTypes = new List<FactionType>();
            foreach (var kvp in factionTypes)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    availableTypes.Add(kvp.Key);
                }
            }
            
            // Перемешиваем типы для случайного распределения
            for (int i = 0; i < availableTypes.Count; i++)
            {
                int j = rng.Next(availableTypes.Count);
                (availableTypes[i], availableTypes[j]) = (availableTypes[j], availableTypes[i]);
            }

            for (int i = 0; i < 20; i++)
            {
                var poly = territories[i];
                Vector2 capPos = poly.Count >= 3 ? ComputeWeightedCentroid(poly) : Vector2.Zero;
                
                // Получаем тип фракции
                FactionType type = i < availableTypes.Count ? 
                    availableTypes[i] : FactionType.Military; // Резервный тип
                
                // Генерируем уникальные параметры
                var (resourceMult, combat, buildSpeed, diplomacy, preferredResource, description) = 
                    GenerateFactionCharacteristics(rng, type);
                
                // Равномерно распределённые оттенки по кругу
                Color color = HsvToColor(i * (360f / 20f), 0.75f, 0.90f);
                
                var faction = new Faction(
                    _nextFactionId++, 
                    FactionNames[i], 
                    color, 
                    capPos,
                    type,
                    description,
                    resourceMult,
                    combat,
                    buildSpeed,
                    diplomacy,
                    preferredResource
                );
                
                Factions[faction.Id] = faction;
            }
        }
        
        private (float, float, float, float, string, string) GenerateFactionCharacteristics(
            Random rng, FactionType type)
        {
            string preferredResource;
            string description;
            
            // Базовые значения для типа
            float baseResource = 1.0f;
            float baseCombat = 1.0f;
            float baseBuild = 1.0f;
            float baseDiplomacy = 1.0f;
            
            switch (type)
            {
                case FactionType.Military:
                    baseCombat = 1.3f;
                    baseResource = 0.8f;
                    preferredResource = "weapons";
                    description = "Военная фракция с мощной армией, но слабой экономикой. Специализируется на производстве оружия и военной техники.";
                    break;
                case FactionType.Economic:
                    baseResource = 1.4f;
                    baseDiplomacy = 1.2f;
                    preferredResource = "materials";
                    description = "Торговая фракция с развитой экономикой. Эксперты в переговорах и управлении ресурсами.";
                    break;
                case FactionType.Technological:
                    baseBuild = 1.5f;
                    baseResource = 1.1f;
                    preferredResource = "materials";
                    description = "Научно-техническая фракция с передовыми технологиями. Создает уникальные устройства и улучшения.";
                    break;
                case FactionType.Magical:
                    baseResource = 1.2f;
                    baseCombat = 1.1f;
                    preferredResource = "luxury";
                    description = "Магическая фракция с мистическими способностями. Использует древние знания и артефакты.";
                    break;
                case FactionType.Agricultural:
                    baseResource = 1.3f;
                    preferredResource = "food";
                    description = "Аграрная фракция с развитым сельским хозяйством. Обеспечивает продовольствием соседей.";
                    break;
                case FactionType.Nomadic:
                    baseCombat = 1.2f;
                    baseBuild = 0.9f;
                    preferredResource = "food";
                    description = "Кочевая фракция с мобильными отрядами. Быстро перемещается и совершает набеги.";
                    break;
                case FactionType.Religious:
                    baseDiplomacy = 1.3f;
                    baseResource = 1.0f;
                    preferredResource = "luxury";
                    description = "Религиозная фракция с духовными лидерами. Оказывает влияние на мораль соседей.";
                    break;
                case FactionType.Industrial:
                    baseResource = 1.2f;
                    baseBuild = 1.3f;
                    preferredResource = "materials";
                    description = "Промышленная фракция с мощными заводами. Производит материалы и оборудование.";
                    break;
                case FactionType.Stealth:
                    baseCombat = 0.9f;
                    baseDiplomacy = 1.1f;
                    preferredResource = "luxury";
                    description = "Скрытная фракция с мастерами шпионажа. Специализируется на диверсиях и разведке.";
                    break;
                case FactionType.Diplomatic:
                    baseDiplomacy = 1.5f;
                    baseCombat = 0.7f;
                    preferredResource = "materials";
                    description = "Дипломатическая фракция с талантливыми переговорщиками. Создает союзы и разрешает конфликты.";
                    break;
                default:
                    preferredResource = "food";
                    description = "Универсальная фракция со сбалансированными характеристиками.";
                    break;
            }
            
            // Небольшие случайные вариации для уникальности каждой фракции
            float resourceMult = baseResource * (0.95f + (float)rng.NextDouble() * 0.1f);
            float combat = baseCombat * (0.95f + (float)rng.NextDouble() * 0.1f);
            float buildSpeed = baseBuild * (0.95f + (float)rng.NextDouble() * 0.1f);
            float diplomacy = baseDiplomacy * (0.95f + (float)rng.NextDouble() * 0.1f);
            
            return (resourceMult, combat, buildSpeed, diplomacy, preferredResource, description);
        }

        /// <summary>HSV → RGB цвет (h: 0-360, s/v: 0-1).</summary>
        private static Color HsvToColor(float h, float s, float v)
        {
            h %= 360f;
            float c = v * s;
            float x = c * (1f - MathF.Abs((h / 60f) % 2f - 1f));
            float m = v - c;
            float r, g, b;
            if      (h < 60f)  { r = c; g = x; b = 0; }
            else if (h < 120f) { r = x; g = c; b = 0; }
            else if (h < 180f) { r = 0; g = c; b = x; }
            else if (h < 240f) { r = 0; g = x; b = c; }
            else if (h < 300f) { r = x; g = 0; b = c; }
            else               { r = c; g = 0; b = x; }
            return new Color(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255),
                (byte)255);
        }

        // ================================================================
        //  УРОВНИ 2 и 3 — города и деревни внутри территории
        // ================================================================

        /// <summary>
        /// Размещает 20 городов и 50 деревень строго внутри полигона фракции.
        /// globalPoints — общий список для проверки расстояний МЕЖДУ всеми фракциями.
        /// </summary>
        private void GenerateSettlementsInTerritory(
            Random rng, Faction faction, List<Vector2> polygon, List<Vector2> globalPoints)
        {
            // --- Столица (уже в центроиде, просто регистрируем) ---
            var capital = new Settlement(
                _nextSettlementId++,
                $"{faction.Name} Capital",
                faction.CapitalPosition,
                SettlementType.Capital,
                faction.Id);
            faction.Settlements.Add(capital);
            // CapitalPosition уже в globalPoints из InitializeWorld

            float[] bb = GetBoundingBox(polygon);

            // --- Уровень 2: 20 городов ---
            for (int i = 0; i < CITIES_PER_FACTION; i++)
            {
                Vector2 pos = SamplePointInPolygon(
                    rng, polygon, globalPoints, CITY_MIN_DIST,
                    bb[0], bb[1], bb[2], bb[3], maxAttempts: 400);

                var city = new Settlement(
                    _nextSettlementId++,
                    $"{faction.Name} City {i + 1}",
                    pos, SettlementType.City, faction.Id);
                faction.Settlements.Add(city);
                globalPoints.Add(pos);
            }

            // --- Уровень 3: 50 деревень ---
            for (int i = 0; i < VILLAGES_PER_FACTION; i++)
            {
                Vector2 pos = SamplePointInPolygon(
                    rng, polygon, globalPoints, VILLAGE_MIN_DIST,
                    bb[0], bb[1], bb[2], bb[3], maxAttempts: 400);

                var village = new Settlement(
                    _nextSettlementId++,
                    $"{faction.Name} Village {i + 1}",
                    pos, SettlementType.Village, faction.Id);
                faction.Settlements.Add(village);
                globalPoints.Add(pos);
            }
        }

        /// <summary>
        /// Rejection sampling: случайная точка в bbox → проверка принадлежности полигону
        /// → проверка минимального расстояния от globalPoints.
        /// При неудаче возвращает центроид (гарантированно внутри).
        /// </summary>
        private Vector2 SamplePointInPolygon(
            Random rng, List<Vector2> polygon, List<Vector2> existing, float minDist,
            float bMinX, float bMaxX, float bMinY, float bMaxY, int maxAttempts)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                float x = bMinX + (float)rng.NextDouble() * (bMaxX - bMinX);
                float y = bMinY + (float)rng.NextDouble() * (bMaxY - bMinY);
                var   p = new Vector2(x, y);

                if (!IsPointInPolygon(p, polygon)) continue;

                bool tooClose = false;
                foreach (var e in existing)
                {
                    if (Vector2.DistanceSquared(e, p) < minDist * minDist)
                    { tooClose = true; break; }
                }
                if (!tooClose) return p;
            }

            // Fallback: центроид полигона (всегда внутри выпуклого полигона)
            return ComputeWeightedCentroid(polygon);
        }

        // ================================================================
        //  ВСПОМОГАТЕЛЬНЫЕ ГЕОМЕТРИЧЕСКИЕ МЕТОДЫ
        // ================================================================

        /// <summary>Проверяет принадлежность точки полигону (ray casting).</summary>
        public static bool IsPointInPolygon(Vector2 pt, List<Vector2> polygon)
        {
            if (polygon.Count < 3) return false;
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((polygon[i].Y > pt.Y) != (polygon[j].Y > pt.Y)) &&
                    (pt.X < (polygon[j].X - polygon[i].X) * (pt.Y - polygon[i].Y)
                             / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                    inside = !inside;
            }
            return inside;
        }

        private static float[] GetBoundingBox(List<Vector2> poly)
        {
            float mnX = float.MaxValue, mxX = float.MinValue;
            float mnY = float.MaxValue, mxY = float.MinValue;
            foreach (var v in poly)
            {
                if (v.X < mnX) mnX = v.X; if (v.X > mxX) mxX = v.X;
                if (v.Y < mnY) mnY = v.Y; if (v.Y > mxY) mxY = v.Y;
            }
            return new[] { mnX, mxX, mnY, mxY };
        }

        // ================================================================
        //  ОСТАЛЬНЫЕ СИСТЕМЫ (ресурсы, рынки, NPC — без изменений)
        // ================================================================
        private void GenerateResources(Random rng)
        {
            for (int i = 0; i < 2000; i++)
            {
                float x    = (float)(rng.NextDouble() * WORLD_RADIUS * 2 - WORLD_RADIUS);
                float y    = (float)(rng.NextDouble() * WORLD_RADIUS * 2 - WORLD_RADIUS);
                var   type = (ResourceType)rng.Next(0, 7);
                var   node = new ResourceNode(_nextResourceId++, $"{type} Deposit {i}",
                    new Vector2(x, y), type);
                Resources[node.Id] = node;
            }
        }

        private void GenerateMarkets()
        {
            foreach (var faction in Factions.Values)
                foreach (var settlement in faction.Settlements)
                    Markets.Add(new Market(settlement.Id));
        }

        private void GenerateNPCs(Random rng)
        {
            foreach (var faction in Factions.Values)
            {
                foreach (var settlement in faction.Settlements)
                {
                    int count = settlement.Type switch
                    {
                        SettlementType.Capital => 20,
                        SettlementType.City    => 10,
                        SettlementType.Village => 4,
                        _                      => 2
                    };

                    for (int i = 0; i < count; i++)
                    {
                        float angle = (float)(rng.NextDouble() * Math.PI * 2);
                        float dist  = (float)(rng.NextDouble() * settlement.Radius * 0.7f);
                        var   pos   = settlement.Position + new Vector2(
                            (float)Math.Cos(angle) * dist,
                            (float)Math.Sin(angle) * dist);

                        var npc = new NPC(
                            _nextNpcId++,
                            TerritoryFactionNPCName(faction.Name, rng),
                            pos, faction.Id);

                        npc.PatrolCenter   = settlement.Position;
                        npc.PatrolRadius   = settlement.Radius;
                        npc.HomeSettlement = settlement;
                        npc.Job = (i % 7) switch
                        {
                            0 => NPCJob.Guard,
                            1 => NPCJob.Miner,
                            2 => NPCJob.Farmer,
                            3 => NPCJob.Merchant,
                            4 => NPCJob.Worker,
                            5 => NPCJob.Blacksmith,
                            _ => NPCJob.Soldier
                        };

                        NPCs[npc.Id]       = npc;
                        faction.Members.Add(npc);
                    }
                }
            }
        }

        private void GenerateFactionLeaders(Random rng)
        {
            foreach (var faction in Factions.Values)
            {
                var capital = faction.Settlements.FirstOrDefault(s => s.Type == SettlementType.Capital);
                if (capital == null) continue;

                var leader = new NPC(
                    _nextNpcId++,
                    $"Lord {TerritoryFactionNPCName(faction.Name, rng)}",
                    capital.Position, faction.Id);

                leader.Job           = NPCJob.Leader;
                leader.Speed         = 0.0f;
                leader.HomeSettlement = capital;
                leader.PatrolCenter  = capital.Position;
                leader.PatrolRadius  = 50.0f;

                NPCs[leader.Id]    = leader;
                faction.Leader     = leader;
                faction.Members.Add(leader);
            }
        }

        private void SetInitialRelations(Random rng)
        {
            var list = Factions.Values.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a = list[i]; var b = list[j];
                    double r = rng.NextDouble();
                    FactionRelation rel;
                    if (r < 0.08)
                    {
                        rel = FactionRelation.War;
                        a.AtWarWith.Add(b.Id); b.AtWarWith.Add(a.Id);
                    }
                    else if (r < 0.20) rel = FactionRelation.Allied;
                    else               rel = FactionRelation.Neutral;

                    a.SetRelation(b.Id, rel);
                    b.SetRelation(a.Id, rel);
                }
            }
        }

        // ================================================================
        //  UPDATE
        // ================================================================
        public void Update(double dt, Vector2 playerPos)
        {
            // --- Цикл дня/ночи ---
            _dayTimer += dt;
            if (_dayTimer >= DAY_DURATION) _dayTimer -= DAY_DURATION;

            DayProgress = (float)(_dayTimer / DAY_DURATION);
            IsNight     = DayProgress > 0.6f;

            // Плавная интенсивность: 0=полдень, 1=глубокая ночь
            NightIntensity = DayProgress switch
            {
                < 0.5f  => 0.0f,                                   // день
                < 0.65f => (DayProgress - 0.5f) / 0.15f,          // закат
                < 0.85f => 1.0f,                                   // ночь
                _       => 1.0f - (DayProgress - 0.85f) / 0.15f   // рассвет
            };

            LoadTerritoryChunksAround(playerPos.X, playerPos.Y);

            _activeNPCs.Clear();
            foreach (var npc in NPCs.Values)
            {
                float d = Vector2.Distance(npc.Position, playerPos);
                if (d < ACTIVE_ZONE_RADIUS)
                {
                    npc.IsSimplified = false;
                    npc.Update(dt);
                    _activeNPCs.Add(npc);
                }
                else if (d < SIMPLIFIED_ZONE_RADIUS)
                {
                    npc.IsSimplified = true;
                    npc.UpdateSimplified(dt);
                }
            }

            _factionAITimer += dt;
            bool runAI = _factionAITimer >= FACTION_AI_INTERVAL;
            if (runAI) _factionAITimer = 0;

            foreach (var f in Factions.Values)
                f.Update(dt, runAI ? Factions : null);

            _marketTimer += dt;
            if (_marketTimer >= MARKET_INTERVAL)
            {
                foreach (var m in Markets) m.UpdatePrices((float)_marketTimer);
                _marketTimer = 0;
            }

            // Fix: используем поле _updateRng, а не new Random() каждый кадр
            foreach (var r in Resources.Values)
            {
                if (!r.IsActive && Vector2.Distance(r.Position, playerPos) < ACTIVE_ZONE_RADIUS)
                    if (_updateRng.NextDouble() < 0.0005 * dt) r.Replenish(500.0f);
            }
        }

        // ================================================================
        //  РЕНДЕР (игровой вид, не карта)
        // ================================================================
        public void Render(Vector2 playerPos)
        {
            TerritoryManager.Render(playerPos, ACTIVE_ZONE_RADIUS * 2);

            foreach (var chunk in _loadedChunks.Values)
                chunk.Render();

            _visibleSettlements.Clear();
            foreach (var f in Factions.Values)
                foreach (var s in f.Settlements)
                    if (Vector2.Distance(playerPos, s.Position) < 8000)
                        _visibleSettlements.Add(s);

            foreach (var s in _visibleSettlements) s.Render();

            _visibleResources.Clear();
            foreach (var r in Resources.Values)
                if (Vector2.Distance(r.Position, playerPos) < 5000)
                    _visibleResources.Add(r);

            foreach (var r in _visibleResources) r.Render();

            foreach (var npc in _activeNPCs)
                if (Factions.TryGetValue(npc.FactionId, out var f))
                    npc.Render(f.Color);

            foreach (var s in _visibleSettlements)
            {
                var market = Markets.FirstOrDefault(m => m.SettlementId == s.Id);
                market?.Render(new Vector2(s.Position.X + s.Radius + 20, s.Position.Y - s.Radius));
            }
        }

        // ================================================================
        //  ВСПОМОГАТЕЛЬНЫЕ
        // ================================================================
        public FactionRelation GetPlayerRelationToFaction(int factionId, Player player)
        {
            float rep = player.GetReputation(factionId);
            if (player.IsWanted(factionId)) return FactionRelation.Hostile;
            if (rep < -50) return FactionRelation.Hostile;
            if (rep > 50)  return FactionRelation.Allied;
            return FactionRelation.Neutral;
        }

        // --- Чанки ---
        private long GetChunkKey(double x, double y)
        {
            long cx = (long)(x / CHUNK_SIZE);
            long cy = (long)(y / CHUNK_SIZE);
            return (cx << 32) | (cy & 0xFFFFFFFFL);
        }

        public void LoadTerritoryChunksAround(double px, double py)
        {
            for (int x = -_viewDistance; x <= _viewDistance; x++)
            {
                for (int y = -_viewDistance; y <= _viewDistance; y++)
                {
                    long cx  = (long)(px / CHUNK_SIZE) + x;
                    long cy  = (long)(py / CHUNK_SIZE) + y;
                    long key = (cx << 32) | (cy & 0xFFFFFFFFL);
                    if (!_loadedChunks.ContainsKey(key))
                        _loadedChunks[key] = new Chunk(key, _seed);
                }
            }

            var toRemove = _loadedChunks.Keys.Where(k =>
            {
                long cx = k >> 32;
                long cy = k & 0xFFFFFFFFL;
                double wx = cx * CHUNK_SIZE;
                double wy = cy * CHUNK_SIZE;
                return Math.Sqrt((wx - px) * (wx - px) + (wy - py) * (wy - py))
                       > (_viewDistance + 2) * CHUNK_SIZE;
            }).ToList();

            foreach (var k in toRemove) _loadedChunks.Remove(k);
        }

        public void Dispose() => _loadedChunks.Clear();

        // --- Генерация имён NPC ---
        private static readonly string[] FirstNames =
            { "Aric", "Bera", "Cade", "Delia", "Ekon", "Fara", "Grim", "Hana", "Ivar", "Jana" };
        private static readonly string[] LastNames =
            { "Stone", "Kell", "Marsh", "Nord", "Orin", "Pike", "Rask", "Skov", "Tor", "Vane" };

        private static string TerritoryFactionNPCName(string factionName, Random rng) =>
            $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}";
    }
}

