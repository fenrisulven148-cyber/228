using Raylib_cs;
using System.Numerics;

namespace KenshiPlanet.Core
{
    /// <summary>
    /// Глобальное хранилище шрифта с поддержкой кириллицы.
    /// Инициализируется в Game.InitializeFont() один раз при старте.
    /// </summary>
    public static class FontManager
    {
        private static Font _font;
        private static bool _initialized = false;

        /// <summary>
        /// Шрифт с кириллицей для всех DrawTextEx-вызовов.
        /// </summary>
        public static Font Font
        {
            get => _font;
            set { _font = value; _initialized = true; }
        }

        public static bool IsInitialized => _initialized;

        // ----------------------------------------------------------------
        //  Вспомогательные обёртки — аналоги DrawText / MeasureText,
        //  но через кириллический шрифт.
        // ----------------------------------------------------------------

        public static void DrawText(string text, int x, int y, int fontSize, Color color)
            => Raylib.DrawTextEx(_font, text, new Vector2(x, y), fontSize, 1f, color);

        public static void DrawText(string text, float x, float y, int fontSize, Color color)
            => Raylib.DrawTextEx(_font, text, new Vector2(x, y), fontSize, 1f, color);

        public static float MeasureText(string text, int fontSize)
            => Raylib.MeasureTextEx(_font, text, fontSize, 1f).X;
    }
}
