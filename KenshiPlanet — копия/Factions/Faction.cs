using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KenshiPlanet.Economy;
using KenshiPlanet.Entities;
using KenshiPlanet.World;

namespace KenshiPlanet.Factions
{
    public enum FactionType 
    {
        Military,      // Агрессивная, сильная армия, слабая экономика
        Economic,      // Торговцы, сильная экономика, слабая армия
        Technological, // Научный подход, технологии, слабые ресурсы
        Magical,       // Магические способности, необычные ресурсы
        Agricultural,  // Фермеры, много еды, мало материалов
        Nomadic,       // Кочевые, мобильные, слабые постройки
        Religious,     // Духовные лидеры, влияние на мораль
        Industrial,    // Производственные мощности, много материалов
        Stealth,       // Скрытность, шпионаж, слабые открытые бои
        Diplomatic     // Переговоры, союзы, слабые боевые действия
    }

    public class Faction
    {
        public int     Id              { get; set; }
        public string  Name            { get; set; } = string.Empty;
        public Color   Color           { get; set; }
        public Vector2 CapitalPosition { get; set; }
        public FactionType Type { get; set; }
        public string Description { get; set; } = string.Empty;

        // Уникальные модификаторы для каждой фракции
        public float ResourceProductionMultiplier { get; set; } = 1.0f; // 0.8-1.5
        public float CombatEffectiveness { get; set; } = 1.0f;         // 0.7-1.3
        public float BuildingSpeed { get; set; } = 1.0f;               // 0.9-1.4
        public float DiplomacyEffect { get; set; } = 1.0f;             // 0.7-1.5
        
        // Специфичные ресурсы
        public string PreferredResource { get; set; } = "food";
        
        // Уникальные здания
        public List<string> UniqueBuildings { get; set; } = new();

        public List<NPC>        Members     { get; set; } = new();
        public List<Settlement> Settlements { get; set; } = new();
        public NPC? Leader { get; set; }
        public FactionLaws Laws { get; set; } = new();

        public float Wealth { get; set; } = 10000.0f;
        public Dictionary<string, float> Resources { get; set; } = new()
        {
            { "Food",      5000.0f },
            { "Materials", 2000.0f },
            { "Weapons",   500.0f  },
            { "Tools",     300.0f  },
            { "Luxury",    100.0f  }
        };

        public float MilitaryStrength =>
            Members.Count(n => n.Job == NPCJob.Soldier || n.Job == NPCJob.Guard) * 10.0f * CombatEffectiveness
            + Resources.GetValueOrDefault("Weapons", 0) * 0.5f;

        public Dictionary<int, FactionRelation> Relations     { get; set; } = new();
        public List<int>                         AtWarWith     { get; set; } = new();
        public List<int>                         TradePartners { get; set; } = new();

        private double _aiTimer = 0.0;
        private const double AI_INTERVAL = 30.0;
        private readonly Random _rng;

        public int TotalCities   => Settlements.Count(s => s.Type == SettlementType.City);
        public int TotalVillages => Settlements.Count(s => s.Type == SettlementType.Village);
        public int TotalSoldiers => Members.Count(n => n.Job == NPCJob.Soldier);
        public List<string> EventLog { get; } = new();

        public Faction(int id, string name, Color color, Vector2 capitalPosition)
        {
            Id = id; Name = name; Color = color; CapitalPosition = capitalPosition;
            _rng = new Random(id * 7919);
        }

        // Новый конструктор для уникальных фракций
        public Faction(int id, string name, Color color, Vector2 capitalPos, 
                      FactionType type, string description,
                      float resourceMult, float combat, 
                      float buildSpeed, float diplomacy,
                      string preferredResource)
        {
            Id = id;
            Name = name;
            Color = color;
            CapitalPosition = capitalPos;
            Type = type;
            Description = description;
            ResourceProductionMultiplier = resourceMult;
            CombatEffectiveness = combat;
            BuildingSpeed = buildSpeed;
            DiplomacyEffect = diplomacy;
            PreferredResource = preferredResource;
            
            _rng = new Random(id * 7919);
            
            // Генерируем уникальные здания
            GenerateUniqueBuildings();
        }
        
        private void GenerateUniqueBuildings()
        {
            switch (Type)
            {
                case FactionType.Military:
                    UniqueBuildings.Add("Barracks");
                    UniqueBuildings.Add("Armory");
                    UniqueBuildings.Add("MilitaryAcademy");
                    break;
                case FactionType.Economic:
                    UniqueBuildings.Add("TradeCenter");
                    UniqueBuildings.Add("Bank");
                    UniqueBuildings.Add("MarketExchange");
                    break;
                case FactionType.Technological:
                    UniqueBuildings.Add("ResearchLab");
                    UniqueBuildings.Add("EngineeringCenter");
                    UniqueBuildings.Add("QuantumReactor");
                    break;
                case FactionType.Magical:
                    UniqueBuildings.Add("MageTower");
                    UniqueBuildings.Add("CrystalForge");
                    UniqueBuildings.Add("SummoningCircle");
                    break;
                case FactionType.Agricultural:
                    UniqueBuildings.Add("Greenhouse");
                    UniqueBuildings.Add("Mill");
                    UniqueBuildings.Add("Brewery");
                    break;
                case FactionType.Nomadic:
                    UniqueBuildings.Add("Yard");
                    UniqueBuildings.Add("CaravanPost");
                    UniqueBuildings.Add("HuntingLodge");
                    break;
                case FactionType.Religious:
                    UniqueBuildings.Add("Temple");
                    UniqueBuildings.Add("Shrine");
                    UniqueBuildings.Add("Monastery");
                    break;
                case FactionType.Industrial:
                    UniqueBuildings.Add("Factory");
                    UniqueBuildings.Add("Refinery");
                    UniqueBuildings.Add("PowerPlant");
                    break;
                case FactionType.Stealth:
                    UniqueBuildings.Add("Hideout");
                    UniqueBuildings.Add("AssassinGuild");
                    UniqueBuildings.Add("SpyNetwork");
                    break;
                case FactionType.Diplomatic:
                    UniqueBuildings.Add("Embassy");
                    UniqueBuildings.Add("CouncilHall");
                    UniqueBuildings.Add("TreatyRoom");
                    break;
            }
        }

        public void Update(double deltaTime, Dictionary<int, Faction>? allFactions = null)
        {
            UpdateEconomy(deltaTime);
            foreach (var s in Settlements) s.Update(deltaTime);

            if (allFactions != null)
            {
                _aiTimer += deltaTime;
                if (_aiTimer >= AI_INTERVAL) { _aiTimer = 0; RunFactionAI(allFactions); }
            }
        }

        private void UpdateEconomy(double dt)
        {
            float f = (float)dt;
            
            // Применяем модификатор производства ресурсов
            float productionMultiplier = ResourceProductionMultiplier;
            
            // Дополнительный бонус за предпочтительный ресурс
            float foodBonus = (PreferredResource == "food") ? 1.2f : 1.0f;
            float materialsBonus = (PreferredResource == "materials") ? 1.2f : 1.0f;
            
            Resources["Food"] = Math.Max(0, Resources.GetValueOrDefault("Food") - Members.Count * 0.3f * f);
            int workers = Members.Count(n => n.Job == NPCJob.Worker || n.Job == NPCJob.Miner);
            Resources["Materials"] = Resources.GetValueOrDefault("Materials") + workers * 5.0f * f * productionMultiplier * materialsBonus;
            int smiths = Members.Count(n => n.Job == NPCJob.Blacksmith);
            if (smiths > 0)
            {
                float p = smiths * 2.0f * f * BuildingSpeed; // Учитываем скорость строительства
                Resources["Materials"] = Math.Max(0, Resources["Materials"] - p * 5);
                Resources["Weapons"] = Resources.GetValueOrDefault("Weapons") + p;
            }
            
            // Бонус к производству еды для аграрных фракций
            if (Type == FactionType.Agricultural)
            {
                int farmers = Members.Count(n => n.Job == NPCJob.Worker);
                Resources["Food"] = Resources.GetValueOrDefault("Food") + farmers * 3.0f * f * productionMultiplier * foodBonus;
            }
            
            Wealth += Wealth * Laws.TaxRate * 0.001f * f * ResourceProductionMultiplier;
            Wealth = Math.Max(0, Wealth - TotalSoldiers * 2.0f * f);
            foreach (var k in Resources.Keys.ToList()) if (Resources[k] < 0) Resources[k] = 0;
        }

        private void RunFactionAI(Dictionary<int, Faction> all)
        {
            var neighbors = all.Values.Where(f => f.Id != Id && Vector2.Distance(CapitalPosition, f.CapitalPosition) < 400000).ToList();
            EvaluateWar(all, neighbors);
            EvaluateTrade(neighbors);
            UpdateLaws();
            if (Wealth > 50000 && AtWarWith.Count > 0) EvaluateExpansion(all);
        }

        private void EvaluateWar(Dictionary<int, Faction> all, List<Faction> neighbors)
        {
            // Мир с проигрышными войнами
            foreach (var eid in AtWarWith.ToList())
            {
                if (!all.TryGetValue(eid, out var enemy)) continue;
                if (MilitaryStrength < enemy.MilitaryStrength * 0.4f) { DeclarePeace(enemy); LogEvent($"Peace with {enemy.Name}"); }
            }
            // Новые войны
            if (AtWarWith.Count < 2 && Wealth > 15000)
            {
                foreach (var n in neighbors.OrderBy(x => x.MilitaryStrength))
                {
                    if (AtWarWith.Contains(n.Id) || GetRelation(n.Id) == FactionRelation.Allied) continue;
                    float ratio = MilitaryStrength / Math.Max(1, n.MilitaryStrength);
                    if (_rng.NextDouble() < (ratio > 1.5 ? 0.15 : 0.02)) { DeclareWar(n); LogEvent($"War on {n.Name}!"); break; }
                }
            }
        }

        private void EvaluateTrade(List<Faction> neighbors)
        {
            foreach (var n in neighbors)
            {
                if (AtWarWith.Contains(n.Id) || TradePartners.Contains(n.Id)) continue;
                
                // Учитываем дипломатический эффект
                float tradeChance = 0.04f * DiplomacyEffect;
                
                if ((Resources.GetValueOrDefault("Food") > 5000 && n.Resources.GetValueOrDefault("Food") < 1000) || _rng.NextDouble() < tradeChance)
                    EstablishTrade(n);
            }
        }

        private void UpdateLaws()
        {
            if (Resources.GetValueOrDefault("Food") < 500) { Laws.TaxRate = Math.Min(Laws.TaxRate + 0.02f, 0.4f); Laws.OpenBorders = true; }
            if (Wealth > 100000 && Laws.TaxRate > 0.05f) Laws.TaxRate -= 0.01f;

            if (AtWarWith.Count > 0)
            {
                Laws.ConscritionActive = true;
                int done = 0;
                foreach (var npc in Members) { if (done >= 3) break; if (npc.Job == NPCJob.Worker) { npc.Job = NPCJob.Soldier; done++; } }
                if (done > 0) LogEvent($"Conscripted {done} citizens");
            }
            else { Laws.ConscritionActive = false; }

            Laws.TaxRate = Math.Clamp(Laws.TaxRate + (_rng.NextDouble() < 0.05 ? (float)(_rng.NextDouble() - 0.5) * 0.05f : 0), 0, 0.5f);
        }

        private void EvaluateExpansion(Dictionary<int, Faction> all)
        {
            Faction? weakEnemy = null; float weakest = float.MaxValue;
            foreach (var eid in AtWarWith) {
                if (!all.TryGetValue(eid, out var e)) continue;
                if (e.MilitaryStrength < MilitaryStrength * 0.6f && e.MilitaryStrength < weakest) { weakest = e.MilitaryStrength; weakEnemy = e; }
            }
            if (weakEnemy == null) return;
            var t = weakEnemy.Settlements.Where(s => s.Type == SettlementType.Village).OrderBy(s => Vector2.Distance(CapitalPosition, s.Position)).FirstOrDefault();
            if (t != null && _rng.NextDouble() < 0.3) {
                weakEnemy.Settlements.Remove(t); t.FactionId = Id; Settlements.Add(t); Wealth -= 2000;
                LogEvent($"Captured {t.Name} from {weakEnemy.Name}!");
                weakEnemy.LogEvent($"Lost {t.Name} to {Name}!");
            }
        }

        public void DeclareWar(Faction other)
        {
            if (AtWarWith.Contains(other.Id)) return;
            AtWarWith.Add(other.Id); SetRelation(other.Id, FactionRelation.War); TradePartners.Remove(other.Id);
            if (!other.AtWarWith.Contains(Id)) { other.AtWarWith.Add(Id); other.SetRelation(Id, FactionRelation.War); other.TradePartners.Remove(Id); }
        }

        public void DeclarePeace(Faction other)
        {
            AtWarWith.Remove(other.Id); other.AtWarWith.Remove(Id);
            SetRelation(other.Id, FactionRelation.Neutral); other.SetRelation(Id, FactionRelation.Neutral);
        }

        public void EstablishTrade(Faction other)
        {
            if (TradePartners.Contains(other.Id) || AtWarWith.Contains(other.Id)) return;
            TradePartners.Add(other.Id); other.TradePartners.Add(Id);
            SetRelation(other.Id, FactionRelation.TradePartner); other.SetRelation(Id, FactionRelation.TradePartner);
            LogEvent($"Trade with {other.Name}");
        }

        public void SetRelation(int factionId, FactionRelation rel) => Relations[factionId] = rel;
        public FactionRelation GetRelation(int factionId) => Relations.TryGetValue(factionId, out var r) ? r : FactionRelation.Neutral;

        public void LogEvent(string msg) { EventLog.Insert(0, msg); if (EventLog.Count > 20) EventLog.RemoveAt(20); }
    }

    public enum FactionRelation { Hostile, Neutral, Allied, War, TradePartner }
    public enum SettlementType  { Capital, City, Village, Outpost, Ruins }
}
