using Raylib_cs;
using KenshiPlanet.Core;

namespace KenshiPlanet
{
    class Program
    {
        static void Main(string[] args)
        {
            // Настройка окна
            Raylib.InitWindow(1280, 720, "Kenshi Planet - Prototype");
            Raylib.SetTargetFPS(0); // Отключаем лимит FPS для рендера, будем контролировать сами
            
            // Инициализация ядра
            using var game = new Game();
            
            // Главный цикл
            while (!Raylib.WindowShouldClose())
            {
                game.Tick();
            }

            Raylib.CloseWindow();
        }
    }
}
