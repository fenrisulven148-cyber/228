using System.Collections.Generic;
using System.Linq;
using KenshiPlanet.Factions;
using KenshiPlanet.Entities;

namespace KenshiPlanet.Combat
{
    public static class CombatSystem
    {
        /// <summary>
        /// Рассчитывает общую боевую мощь фракции
        /// </summary>
        public static float CalculateCombatStrength(int factionId, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction)) 
                return 0.0f;
            
            // Базовая боевая мощь с учетом модификатора фракции
            float baseStrength = faction.MilitaryStrength * faction.CombatEffectiveness;
            
            // Дополнительные бонусы для определенных типов фракций
            switch (faction.Type)
            {
                case FactionType.Military:
                    baseStrength *= 1.5f; // Военные фракции получают +50% к боевой мощи
                    break;
                case FactionType.Nomadic:
                    baseStrength *= 1.2f; // Кочевники получают +20%
                    break;
                case FactionType.Stealth:
                    baseStrength *= 0.9f; // Скрытные фракции немного слабее в открытом бою
                    break;
                case FactionType.Diplomatic:
                    baseStrength *= 0.7f; // Дипломаты значительно слабее в бою
                    break;
            }
            
            // Бонус за наличие уникальных военных зданий
            if (faction.UniqueBuildings.Contains("Barracks"))
                baseStrength *= 1.1f;
            if (faction.UniqueBuildings.Contains("MilitaryAcademy"))
                baseStrength *= 1.15f;
            if (faction.UniqueBuildings.Contains("Armory"))
                baseStrength *= 1.05f;
            
            // Бонус за количество оружия
            float weaponsBonus = faction.Resources.GetValueOrDefault("Weapons", 0) * 0.001f;
            baseStrength += weaponsBonus;
            
            return baseStrength;
        }
        
        /// <summary>
        /// Рассчитывает эффективность обороны для фракции
        /// </summary>
        public static float CalculateDefenseStrength(int factionId, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction)) 
                return 0.0f;
            
            float defense = CalculateCombatStrength(factionId, factions) * 0.8f; // Оборона обычно 80% от атаки
            
            // Дополнительные бонусы для обороны
            switch (faction.Type)
            {
                case FactionType.Military:
                    defense *= 1.3f; // Военные лучше обороняются
                    break;
                case FactionType.Industrial:
                    defense *= 1.1f; // Промышленные фракции имеют укрепленные заводы
                    break;
                case FactionType.Religious:
                    defense *= 1.2f; // Религиозные фракции сражаются храбро за веру
                    break;
            }
            
            // Бонус за оборонительные здания
            if (faction.UniqueBuildings.Contains("Temple"))
                defense *= 1.05f;
            if (faction.UniqueBuildings.Contains("Monastery"))
                defense *= 1.08f;
            
            return defense;
        }
        
        /// <summary>
        /// Рассчитывает вероятность победы в битве
        /// </summary>
        public static float CalculateVictoryChance(int attackerId, int defenderId, Dictionary<int, Faction> factions)
        {
            float attackerStrength = CalculateCombatStrength(attackerId, factions);
            float defenderStrength = CalculateDefenseStrength(defenderId, factions);
            
            if (attackerStrength <= 0 || defenderStrength <= 0)
                return 0.0f;
            
            // Базовая вероятность на основе соотношения сил
            float ratio = attackerStrength / defenderStrength;
            float baseChance = ratio / (1.0f + ratio);
            
            // Модификаторы в зависимости от типов фракций
            if (factions.TryGetValue(attackerId, out var attacker) && 
                factions.TryGetValue(defenderId, out var defender))
            {
                // Военные фракции лучше против дипломатических
                if (attacker.Type == FactionType.Military && defender.Type == FactionType.Diplomatic)
                    baseChance *= 1.2f;
                
                // Скрытные фракции имеют преимущество в неожиданных атаках
                if (attacker.Type == FactionType.Stealth && defender.Type != FactionType.Stealth)
                    baseChance *= 1.15f;
                
                // Дипломатические фракции хуже в атаке на военных
                if (attacker.Type == FactionType.Diplomatic && defender.Type == FactionType.Military)
                    baseChance *= 0.8f;
            }
            
            return Math.Clamp(baseChance, 0.1f, 0.9f); // Минимум 10%, максимум 90%
        }
        
        /// <summary>
        /// Рассчитывает потери в битве
        /// </summary>
        public static (int attackerLosses, int defenderLosses) CalculateBattleLosses(
            int attackerId, int defenderId, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(attackerId, out var attacker) || 
                !factions.TryGetValue(defenderId, out var defender))
                return (0, 0);
            
            int attackerSoldiers = attacker.TotalSoldiers;
            int defenderSoldiers = defender.TotalSoldiers;
            
            float victoryChance = CalculateVictoryChance(attackerId, defenderId, factions);
            
            // Базовые потери в процентах
            float attackerLossRate = 0.1f + (1.0f - victoryChance) * 0.3f; // Проигравший теряет больше
            float defenderLossRate = 0.1f + victoryChance * 0.4f; // Проигравший теряет больше
            
            // Модификаторы потерь в зависимости от типов фракций
            if (attacker.Type == FactionType.Military)
                attackerLossRate *= 0.8f; // Военные несут меньше потерь
            
            if (defender.Type == FactionType.Military)
                defenderLossRate *= 0.7f; // Оборона военных эффективнее
            
            if (attacker.Type == FactionType.Stealth)
                attackerLossRate *= 0.6f; // Скрытные фракции несут меньше потерь
            
            // Рассчитываем абсолютные потери
            int attackerLosses = (int)(attackerSoldiers * attackerLossRate);
            int defenderLosses = (int)(defenderSoldiers * defenderLossRate);
            
            // Ограничиваем потери доступным количеством солдат
            attackerLosses = Math.Min(attackerLosses, attackerSoldiers);
            defenderLosses = Math.Min(defenderLosses, defenderSoldiers);
            
            return (attackerLosses, defenderLosses);
        }
        
        /// <summary>
        /// Проверяет, должна ли фракция начать войну
        /// </summary>
        public static bool ShouldDeclareWar(int factionId, int targetId, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction) || 
                !factions.TryGetValue(targetId, out var target))
                return false;
            
            // Военные фракции более агрессивны
            float aggressionModifier = faction.Type switch
            {
                FactionType.Military => 1.5f,
                FactionType.Nomadic => 1.3f,
                FactionType.Stealth => 0.8f, // Скрытные фракции реже объявляют войну
                FactionType.Diplomatic => 0.3f, // Дипломаты редко воюют
                _ => 1.0f
            };
            
            // Рассчитываем соотношение сил
            float ourStrength = CalculateCombatStrength(factionId, factions);
            float theirStrength = CalculateCombatStrength(targetId, factions);
            float strengthRatio = ourStrength / Math.Max(1, theirStrength);
            
            // Фракции атакуют если у них есть преимущество в силах
            bool hasStrengthAdvantage = strengthRatio > 1.2f;
            
            // Дипломатические фракции атакуют только при большом преимуществе
            if (faction.Type == FactionType.Diplomatic)
                return hasStrengthAdvantage && strengthRatio > 2.0f;
            
            // Военные фракции могут атаковать даже при небольшом преимуществе
            if (faction.Type == FactionType.Military)
                return hasStrengthAdvantage && strengthRatio > 1.1f;
            
            return hasStrengthAdvantage && strengthRatio > 1.5f;
        }
        
        /// <summary>
        /// Получает эффективный радиус патрулирования для фракции
        /// </summary>
        public static float GetPatrolRadius(int factionId, Dictionary<int, Faction> factions)
        {
            if (!factions.TryGetValue(factionId, out var faction)) 
                return 50000.0f;
            
            float baseRadius = 50000.0f;
            
            switch (faction.Type)
            {
                case FactionType.Military:
                    baseRadius *= 1.5f; // Военные патрулируют дальше
                    break;
                case FactionType.Nomadic:
                    baseRadius *= 2.0f; // Кочевники имеют большой радиус
                    break;
                case FactionType.Stealth:
                    baseRadius *= 0.8f; // Скрытные фракции патрулируют меньше
                    break;
                case FactionType.Diplomatic:
                    baseRadius *= 0.6f; // Дипломаты минимизируют военное присутствие
                    break;
            }
            
            return baseRadius;
        }
    }
}
