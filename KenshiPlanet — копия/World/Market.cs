using System;
using System.Collections.Generic;
using KenshiPlanet.Economy;

namespace KenshiPlanet.World
{
    /// <summary>
    /// Рынок поселения - управляет ценами и запасами ресурсов
    /// </summary>
    public class Market
    {
        public int SettlementId { get; private set; }
        
        // Цены на ресурсы
        private readonly Dictionary<ResourceType, float> _prices = new();
        
        // Запасы ресурсов на рынке
        private readonly Dictionary<ResourceType, float> _stock = new();
        
        // Спрос и предложение для динамического ценообразования
        private readonly Dictionary<ResourceType, float> _demand = new();
        private readonly Dictionary<ResourceType, float> _supply = new();
        
        // Базовые множители цен для этого рынка
        private readonly Dictionary<ResourceType, float> _priceModifiers = new();

        private readonly Random _rng = new();

        public Market(int settlementId)
        {
            SettlementId = settlementId;
            InitializePrices();
        }

        private void InitializePrices()
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                // Базовая цена из глобальных настроек
                float basePrice = ResourceGlobals.BasePrices.TryGetValue(type, out var bp) ? bp : 50f;
                
                // Случайный модификатор для конкретного рынка (+-20%)
                float modifier = 0.8f + (float)_rng.NextDouble() * 0.4f;
                
                _prices[type] = basePrice * modifier;
                _stock[type] = 100f; // Начальный запас
                _demand[type] = 0f;
                _supply[type] = 0f;
                _priceModifiers[type] = modifier;
            }
        }

        /// <summary>
        /// Получить текущую цену ресурса
        /// </summary>
        public float GetPrice(ResourceType type)
        {
            if (!_prices.ContainsKey(type))
                return ResourceGlobals.BasePrices.TryGetValue(type, out var bp) ? bp : 50f;
            
            return _prices[type];
        }

        /// <summary>
        /// Добавить спрос на ресурс (покупка игроком)
        /// </summary>
        public void AddDemand(ResourceType type, float amount)
        {
            if (!_demand.ContainsKey(type))
                _demand[type] = 0f;
            _demand[type] += amount;
        }

        /// <summary>
        /// Добавить предложение ресурса (продажа игроком)
        /// </summary>
        public void AddSupply(ResourceType type, float amount)
        {
            if (!_supply.ContainsKey(type))
                _supply[type] = 0f;
            _supply[type] += amount;
            
            // Увеличиваем запас на рынке при покупке у игрока
            if (!_stock.ContainsKey(type))
                _stock[type] = 0f;
            _stock[type] += amount;
        }

        /// <summary>
        /// Обновить цены на основе спроса и предложения
        /// Вызывается периодически (каждую секунду игрового времени)
        /// </summary>
        public void UpdatePrices(float deltaTime)
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                if (!_prices.ContainsKey(type))
                    continue;

                float basePrice = ResourceGlobals.BasePrices.TryGetValue(type, out var bp) ? bp : 50f;
                float currentPrice = _prices[type];
                
                // Получаем спрос и предложение
                float demand = _demand.ContainsKey(type) ? _demand[type] : 0f;
                float supply = _supply.ContainsKey(type) ? _supply[type] : 0f;
                
                // Коэффициент влияния спроса/предложения
                // Если спрос > предложения - цена растет
                // Если предложение > спроса - цена падает
                float balanceFactor = 1f;
                if (demand > 0f || supply > 0f)
                {
                    float ratio = (demand + 1f) / (supply + 1f);
                    balanceFactor = Math.Clamp(ratio, 0.5f, 2f);
                }
                
                // Плавное изменение цены к базовой с учетом баланса
                float targetPrice = basePrice * _priceModifiers[type] * balanceFactor;
                float speed = 0.01f; // Скорость изменения цен
                
                _prices[type] = currentPrice + (targetPrice - currentPrice) * speed;
                
                // Ограничиваем цену разумными пределами (50% - 200% от базовой)
                float minPrice = basePrice * 0.5f;
                float maxPrice = basePrice * 2f;
                _prices[type] = Math.Clamp(_prices[type], minPrice, maxPrice);
                
                // Постепенно уменьшаем накопленный спрос и предложение
                if (_demand.ContainsKey(type))
                    _demand[type] *= 0.99f;
                if (_supply.ContainsKey(type))
                    _supply[type] *= 0.99f;
            }
        }

        /// <summary>
        /// Купить ресурс со склада рынка
        /// </summary>
        public bool TryBuy(ResourceType type, float amount, out float cost)
        {
            cost = 0f;
            
            if (!_stock.ContainsKey(type) || _stock[type] < amount)
                return false;
            
            cost = GetPrice(type) * amount;
            _stock[type] -= amount;
            return true;
        }

        /// <summary>
        /// Продать ресурс на рынок
        /// </summary>
        public void Sell(ResourceType type, float amount)
        {
            if (!_stock.ContainsKey(type))
                _stock[type] = 0f;
            _stock[type] += amount;
        }

        /// <summary>
        /// Получить количество ресурса на складе
        /// </summary>
        public float GetStock(ResourceType type)
        {
            return _stock.ContainsKey(type) ? _stock[type] : 0f;
        }
        
        /// <summary>
        /// Отрисовка информации о рынке (для отладки)
        /// </summary>
        public void Render(Vector2 position)
        {
            // Простая отрисовка для отладки - можно расширить
            int x = (int)position.X;
            int y = (int)position.Y;
            
            Raylib.DrawRectangle(x, y, 150, 100, new Color(40, 40, 50, 200));
            Raylib.DrawRectangleLines(x, y, 150, 100, Color.White);
            
            FontManager.DrawText("Рынок", x + 5, y + 5, 14, Color.White);
            FontManager.DrawText($"Товаров: {_stock.Count}", x + 5, y + 25, 12, Color.LightGray);
        }
    }
}
