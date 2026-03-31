using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace KenshiPlanet.Utils
{
    public static class PolygonFiller
    {
        /// <summary>
        /// Заливает выпуклый полигон заданным цветом
        /// </summary>
        public static void FillConvexPolygon(Vector2[] points, Color color)
        {
            if (points.Length < 3) return;
            
            // Для выпуклых полигонов используем триангуляцию через центральную точку
            Vector2 center = GetCentroid(points);
            
            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                Raylib.DrawTriangle(
                    points[i], 
                    center, 
                    points[next], 
                    color
                );
            }
        }
        
        /// <summary>
        /// Вычисляет центроид полигона
        /// </summary>
        private static Vector2 GetCentroid(Vector2[] points)
        {
            float cx = 0, cy = 0;
            foreach (var p in points)
            {
                cx += p.X;
                cy += p.Y;
            }
            return new Vector2(cx / points.Length, cy / points.Length);
        }
    }
}
