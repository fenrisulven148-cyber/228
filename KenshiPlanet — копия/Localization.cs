using System.Collections.Generic;

namespace KenshiPlanet
{
    /// <summary>
    /// Централизованная система локализации для русского языка
    /// </summary>
    public static class Localization
    {
        public static string Get(string key) => _translations.TryGetValue(key, out var value) ? value : key;
        
        private static readonly Dictionary<string, string> _translations = new()
        {
            // HUD и интерфейс
            {"FPS", "FPS"},
            {"Zoom", "Масштаб"},
            {"NPCsActive", "Активные NPC"},
            {"Factions", "Фракции"},
            {"FastTime", "БЫСТРОЕ ВРЕМЯ"},
            {"Wilderness", "Дикая местность"},
            {"Wars", "Войны"},
            
            // Управление
            {"Controls", "WASD: Движение E: Купить еду F: Фракция L: Законы M: Карта Tab: x5 Время Колесо: Масштаб"},
            {"BuyFood", "[E] Купить еду"},
            {"Attack", "[ЛКМ] Атаковать"},
            
            // Панель фракции
            {"Wealth", "Богатство"},
            {"Members", "Члены"},
            {"Soldiers", "Солдаты"},
            {"MilitaryStrength", "Военная мощь"},
            {"Cities", "Города"},
            {"Villages", "Деревни"},
            {"Resources", "РЕСУРСЫ"},
            {"RecentEvents", "ПОСЛЕДНИЕ СОБЫТИЯ"},
            {"Close", "Закрыть"},
            {"UniqueBuildings", "Уникальные здания"},
            {"Gold", "г"},
            
            // Панель законов
            {"Laws", "ЗАКОНЫ"},
            {"Tax", "Налог"},
            {"CrimeFine", "Штраф"},
            {"BordersOpen", "Границы: Открыты"},
            {"BordersClosed", "Границы: Закрыты"},
            {"Activities", "Деятельность"},
            {"Slavery", "Рабство"},
            {"Drugs", "Наркотики"},
            {"MercenaryWork", "Наёмничество"},
            {"StreetFighting", "Уличные бои"},
            {"Conscription", "Призыв"},
            {"YourReputation", "Ваша репутация"},
            
            // Глобальная карта
            {"GlobalMapTitle", "ГЛОБАЛЬНАЯ КАРТА"},
            {"GlobalMapClose", "[M] Закрыть - Кликните на территории для информации"},
            {"PlayerMarker", "ВЫ"},
            {"ActiveWars", "АКТИВНЫЕ ВОЙНЫ:"},
            {"Peace", "(Мир)"},
            {"WarAgainst", "против"},
            
            // Статистика
            {"Territories", "Территории"},
            {"Valid", "валидных"},
            {"Invalid", "невалидных"},
            {"CitiesShort", "Г"},
            {"VillagesShort", "Д"},
            
            // Типы фракций
            {"Type", "Тип"},
            {"Production", "Производство"},
            {"Combat", "Боевая мощь"},
            {"Building", "Строительство"},
            {"Diplomacy", "Дипломатия"},
            {"Military", "Военная"},
            {"Economic", "Экономическая"},
            {"Technological", "Технологическая"},
            {"Magical", "Магическая"},
            {"Agricultural", "Сельскохозяйственная"},
            {"Nomadic", "Кочевая"},
            {"Religious", "Религиозная"},
            {"Industrial", "Промышленная"},
            {"Stealth", "Скрытная"},
            {"Diplomatic", "Дипломатическая"},
            {"Universal", "Универсальная"}
        };
    }
}
