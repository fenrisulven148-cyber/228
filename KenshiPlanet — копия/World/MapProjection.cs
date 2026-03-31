using System.Numerics;

namespace KenshiPlanet.World
{
    /// <summary>
    /// Единственная система координат для глобальной карты.
    /// Все компоненты используют только этот класс.
    /// </summary>
    public class MapProjection
    {
        private readonly float _minX, _maxX, _minY, _maxY;
        private readonly int _margin = 50;

        public MapProjection(float minX, float maxX, float minY, float maxY)
        {
            _minX = minX;
            _maxX = maxX;
            _minY = minY;
            _maxY = maxY;
        }

        public Vector2 WorldToScreen(Vector2 worldPos, int screenWidth, int screenHeight)
        {
            float worldWidth  = _maxX - _minX;
            float worldHeight = _maxY - _minY;

            float x = ((worldPos.X - _minX) / worldWidth)  * (screenWidth  - _margin * 2) + _margin;
            float y = ((worldPos.Y - _minY) / worldHeight) * (screenHeight - _margin * 2) + _margin;

            return new Vector2(x, y);
        }
        
        public Vector2 ScreenToWorld(Vector2 screenPos, int screenWidth, int screenHeight)
        {
            float worldWidth  = _maxX - _minX;
            float worldHeight = _maxY - _minY;

            float x = ((screenPos.X - _margin) / (screenWidth  - _margin * 2)) * worldWidth + _minX;
            float y = ((screenPos.Y - _margin) / (screenHeight - _margin * 2)) * worldHeight + _minY;

            return new Vector2(x, y);
        }
    }
}
