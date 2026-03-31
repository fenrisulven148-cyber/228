using Raylib_cs;
using System.Numerics;
using System.Collections.Generic;

namespace KenshiPlanet.World
{
    public class Chunk
    {
        public long Id { get; }
        private Vector2 _origin;
        private List<(Vector2 pos, Color color)> _decorations = new();

        public Chunk(long id, int worldSeed)
        {
            Id = id;
            long cx = id >> 32;
            long cy = id & 0xFFFFFFFFL;
            _origin = new Vector2(cx * 5000, cy * 5000);

            var rng = new Random(worldSeed ^ (int)id);
            // Природные декорации (камни, растения)
            int count = rng.Next(10, 40);
            for (int i = 0; i < count; i++)
            {
                var pos = _origin + new Vector2(rng.Next(0, 5000), rng.Next(0, 5000));
                Color col = rng.Next(3) switch
                {
                    0 => new Color(60, 80, 40, 180),   // трава
                    1 => new Color(90, 85, 80, 180),   // камень
                    2 => new Color(40, 60, 30, 180),   // кустарник
                    _ => new Color(60, 80, 40, 180)    // по умолчанию трава
                };
                _decorations.Add((pos, col));
            }
        }

        public void Render()
        {
            foreach (var (pos, color) in _decorations)
                Raylib.DrawCircleV(pos, 4.0f, color);
            
            // Debug: граница чанка (убрать при желании)
            Raylib.DrawRectangleLines((int)_origin.X, (int)_origin.Y, 5000, 5000,
                new Color(30, 30, 30, 80));
        }
    }
}
