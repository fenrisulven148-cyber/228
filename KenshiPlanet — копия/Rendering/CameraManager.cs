using Raylib_cs;
using System.Numerics;

namespace KenshiPlanet.Rendering
{
    public class CameraManager
    {
        private static CameraManager? _instance;
        public static CameraManager Instance => _instance ??= new CameraManager();

        public Camera2D Camera { get; private set; }

        private CameraManager()
        {
            Camera = new Camera2D
            {
                Zoom     = 0.3f,
                Target   = new Vector2(0, 0),
                Rotation = 0.0f,
                Offset   = Vector2.Zero
            };
        }

        /// <summary>
        /// Обновить камеру. Offset = центр экрана для слежения за игроком.
        /// </summary>
        public void UpdateCamera(Vector2 target, float zoom, Vector2? offset = null)
        {
            Camera = new Camera2D
            {
                Target   = target,
                Zoom     = zoom,
                Rotation = 0.0f,
                Offset   = offset ?? Vector2.Zero
            };
        }
        
        /// <summary>
        /// Преобразовать мировые координаты в экранные
        /// </summary>
        public Vector2 WorldToScreen(Vector2 worldPosition)
        {
            return Raylib.GetWorldToScreen2D(worldPosition, Camera);
        }
    }
}
