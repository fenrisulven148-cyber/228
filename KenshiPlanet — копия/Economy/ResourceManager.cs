using System.Collections.Generic;
using KenshiPlanet.Factions;

namespace KenshiPlanet.Economy
{
    public static class ResourceManager
    {
        /// <summary>
        /// Получает базовую скорость производства ресурсов для фракции
        /// </summary>
        public static float GetResourceProductionRate(int factionId, string resourceType, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction)) 
                return 1.0f;
            
            // Базовый множитель с учетом типа фракции
            float baseRate = faction.ResourceProductionMultiplier;
            
            // Дополнительный бонус за предпочтительный ресурс
            if (resourceType.ToLower() == faction.PreferredResource.ToLower())
                baseRate *= 1.2f;
            
            // Специальные бонусы для определенных типов фракций
            switch (faction.Type)
            {
                case FactionType.Agricultural:
                    if (resourceType == "food") baseRate *= 1.5f;
                    break;
                case FactionType.Industrial:
                    if (resourceType == "materials") baseRate *= 1.3f;
                    break;
                case FactionType.Military:
                    if (resourceType == "weapons") baseRate *= 1.4f;
                    break;
                case FactionType.Economic:
                    baseRate *= 1.1f; // Общий бонус ко всем ресурсам
                    break;
                case FactionType.Magical:
                    if (resourceType == "luxury") baseRate *= 1.6f;
                    break;
            }
            
            return baseRate;
        }
        
        /// <summary>
        /// Получает модификатор стоимости строительства для фракции
        /// </summary>
        public static float GetBuildingCostModifier(int factionId, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction)) 
                return 1.0f;
            
            // Обратная зависимость от скорости строительства
            return 2.0f - faction.BuildingSpeed; // Чем быстрее стройка, тем меньше стоимость
        }
        
        /// <summary>
        /// Получает эффективность торговли для фракции
        /// </summary>
        public static float GetTradeEfficiency(int factionId, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction)) 
                return 1.0f;
            
            float efficiency = faction.DiplomacyEffect;
            
            // Дополнительные бонусы для торговых фракций
            if (faction.Type == FactionType.Economic)
                efficiency *= 1.3f;
            else if (faction.Type == FactionType.Diplomatic)
                efficiency *= 1.2f;
            
            return efficiency;
        }
        
        /// <summary>
        /// Получает модификатор вербовки юнитов для фракции
        /// </summary>
        public static float GetRecruitmentModifier(int factionId, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction)) 
                return 1.0f;
            
            float modifier = 1.0f;
            
            switch (faction.Type)
            {
                case FactionType.Military:
                    modifier = 1.5f; // Военные фракции вербуют быстрее
                    break;
                case FactionType.Nomadic:
                    modifier = 1.3f; // Кочевники тоже хороши в вербовке
                    break;
                case FactionType.Diplomatic:
                    modifier = 0.7f; // Дипломаты предпочитают переговоры
                    break;
                case FactionType.Stealth:
                    modifier = 0.8f; // Скрытные фракции вербуют меньше, но качественнее
                    break;
            }
            
            return modifier;
        }
        
        /// <summary>
        /// Получает модификатор исследования технологий для фракции
        /// </summary>
        public static float GetResearchModifier(int factionId, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction)) 
                return 1.0f;
            
            float modifier = 1.0f;
            
            switch (faction.Type)
            {
                case FactionType.Technological:
                    modifier = 2.0f; // Технологические фракции исследуют в 2 раза быстрее
                    break;
                case FactionType.Magical:
                    modifier = 1.5f; // Магические фракции тоже хороши в исследованиях
                    break;
                case FactionType.Industrial:
                    modifier = 1.3f; // Промышленные фракции исследуют технологии производства
                    break;
                case FactionType.Religious:
                    modifier = 0.8f; // Религиозные фракции меньше интересуются технологиями
                    break;
            }
            
            return modifier;
        }
        
        /// <summary>
        /// Проверяет, может ли фракция строить определенное здание
        /// </summary>
        public static bool CanBuildBuilding(int factionId, string buildingType, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction)) 
                return false;
            
            // Уникальные здания доступны только для соответствующих фракций
            if (faction.UniqueBuildings.Contains(buildingType))
                return true;
            
            // Общие здания доступны всем
            var commonBuildings = new List<string> { "House", "Farm", "Mine", "Workshop", "Market" };
            return commonBuildings.Contains(buildingType);
        }
        
        /// <summary>
        /// Получает список доступных зданий для фракции
        /// </summary>
        public static List<string> GetAvailableBuildings(int factionId, Dictionary<int, Faction> factions)
        {
            var buildings = new List<string>();
            
            if (!factions.TryGetValue(factionId, out var faction)) 
                return buildings;
            
            // Добавляем уникальные здания
            buildings.AddRange(faction.UniqueBuildings);
            
            // Добавляем общие здания
            buildings.AddRange(new List<string> { "House", "Farm", "Mine", "Workshop", "Market" });
            
            return buildings;
        }
    }
}
