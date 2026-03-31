using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using KenshiPlanet.Core;
using KenshiPlanet.Economy;
using KenshiPlanet.Entities;
using KenshiPlanet.World;

namespace KenshiPlanet.UI
{
    /// <summary>
    /// UI система для взаимодействия с ресурсами (как в Kenshi)
    /// </summary>
    public class ResourceInteractionUI
    {
        private ResourceNode? _selectedNode = null;
        private bool _showContextMenu = false;
        private Vector2 _contextMenuPosition = Vector2.Zero;
        
        // Расстояние взаимодействия с ресурсом
        public const float INTERACTION_DISTANCE = 100f;
        
        // Позиция контекстного меню
        private const int CONTEXT_MENU_WIDTH = 200;
        private const int CONTEXT_MENU_HEIGHT = 80;
        
        // Ссылка на мир для доступа к ресурсам
        private WorldManager? _world;
        
        public void SetWorld(WorldManager world) => _world = world;
        
        public void Update(Player player, double deltaTime)
        {
            // Проверяем наличие ресурсов поблизости
            if (_selectedNode == null || !_selectedNode.IsActive)
            {
                _showContextMenu = false;
                _selectedNode = null;
            }
            
            // Если меню открыто, обрабатываем клики
            if (_showContextMenu && _selectedNode != null)
            {
                HandleContextMenuInput(player, (float)deltaTime);
            }
        }
        
        private void HandleContextMenuInput(Player player, float deltaTime)
        {
            Vector2 mousePos = Raylib.GetMousePosition();
            
            // Проверка клика вне меню - закрытие
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                bool clickedOnMenu = mousePos.X >= _contextMenuPosition.X &&
                                   mousePos.X <= _contextMenuPosition.X + CONTEXT_MENU_WIDTH &&
                                   mousePos.Y >= _contextMenuPosition.Y &&
                                   mousePos.Y <= _contextMenuPosition.Y + CONTEXT_MENU_HEIGHT;
                
                if (!clickedOnMenu)
                {
                    _showContextMenu = false;
                    _selectedNode = null;
                }
            }
        }
        
        public void TrySelectNode(ResourceNode node, Vector2 playerPos)
        {
            float distance = Vector2.Distance(playerPos, node.Position);
            
            if (distance <= INTERACTION_DISTANCE && node.IsActive)
            {
                _selectedNode = node;
                _showContextMenu = true;
                
                // Позиционируем меню рядом с курсором
                Vector2 mousePos = Raylib.GetMousePosition();
                _contextMenuPosition = new Vector2(mousePos.X, mousePos.Y);
                
                // Не даем меню выйти за пределы экрана
                int sw = Raylib.GetScreenWidth();
                int sh = Raylib.GetScreenHeight();
                
                if (_contextMenuPosition.X + CONTEXT_MENU_WIDTH > sw)
                    _contextMenuPosition.X = sw - CONTEXT_MENU_WIDTH - 10;
                if (_contextMenuPosition.Y + CONTEXT_MENU_HEIGHT > sh)
                    _contextMenuPosition.Y = sh - CONTEXT_MENU_HEIGHT - 10;
            }
        }
        
        public void StartMining(MiningSession miningSession)
        {
            if (_selectedNode != null && _selectedNode.IsActive)
            {
                miningSession.Start(_selectedNode);
                _showContextMenu = false;
            }
        }
        
        public void Render(MiningSession miningSession, Player player)
        {
            // Рендер контекстного меню
            if (_showContextMenu && _selectedNode != null)
            {
                RenderContextMenu();
            }
            
            // Рендер прогресс-бара добычи
            if (miningSession.IsMining && miningSession.TargetNode != null)
            {
                RenderMiningProgress(miningSession, player);
            }
            
            // Подсветка ближайшего ресурса
            RenderNearestResourceHighlight(player);
        }
        
        private void RenderContextMenu()
        {
            if (_selectedNode == null) return;
            
            int x = (int)_contextMenuPosition.X;
            int y = (int)_contextMenuPosition.Y;
            
            // Фон меню
            Raylib.DrawRectangle(x, y, CONTEXT_MENU_WIDTH, CONTEXT_MENU_HEIGHT, new Color(30, 30, 40, 230));
            Raylib.DrawRectangleLines(x, y, CONTEXT_MENU_WIDTH, CONTEXT_MENU_HEIGHT, Color.White);
            
            // Название ресурса
            string resourceName = ResourceGlobals.RussianNames.TryGetValue(_selectedNode.Type, out var name) 
                ? name 
                : _selectedNode.Type.ToString();
            
            FontManager.DrawText(resourceName, x + 10, y + 8, 16, Color.White);
            
            // Количество ресурса
            string amountText = $"Осталось: {_selectedNode.Amount:F0} / {_selectedNode.MaxAmount:F0}";
            FontManager.DrawText(amountText, x + 10, y + 28, 12, Color.LightGray);
            
            // Кнопка "Добыть"
            int btnX = x + 10;
            int btnY = y + 50;
            int btnW = 180;
            int btnH = 25;
            
            Vector2 mousePos = Raylib.GetMousePosition();
            bool hover = mousePos.X >= btnX && mousePos.X <= btnX + btnW &&
                        mousePos.Y >= btnY && mousePos.Y <= btnY + btnH;
            
            Color btnColor = hover ? new Color(80, 100, 80, 255) : new Color(50, 70, 50, 255);
            Raylib.DrawRectangle(btnX, btnY, btnW, btnH, btnColor);
            Raylib.DrawRectangleLines(btnX, btnY, btnW, btnH, hover ? Color.White : Color.Gray);
            
            string btnText = "Добыть (ЛКМ)";
            Vector2 textSize = Raylib.MeasureTextEx(FontManager.Font, btnText, 14, 1);
            int textW = (int)textSize.X;
            FontManager.DrawText(btnText, btnX + (btnW - textW) / 2, btnY + 5, 14, Color.White);
            
            // Обработка клика по кнопке
            if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                // Сигнал о начале добычи будет обработан через событие
                OnMineButtonClick?.Invoke();
            }
        }
        
        private void RenderMiningProgress(MiningSession session, Player player)
        {
            if (session.TargetNode == null) return;
            
            // Позиция над головой игрока
            Vector2 screenPos = CameraManager.Instance.WorldToScreen(
                player.Position + new Vector2(0, -50));
            
            int barWidth = 200;
            int barHeight = 20;
            int x = (int)screenPos.X - barWidth / 2;
            int y = (int)screenPos.Y;
            
            // Фон прогресс-бара
            Raylib.DrawRectangle(x, y, barWidth, barHeight, new Color(40, 40, 40, 200));
            
            // Прогресс
            float progress = session.Progress;
            int fillWidth = (int)(barWidth * progress);
            
            Color progressColor = progress >= 1f ? Color.Green : new Color(255, 200, 0, 255);
            Raylib.DrawRectangle(x + 1, y + 1, fillWidth - 2, barHeight - 2, progressColor);
            
            // Рамка
            Raylib.DrawRectangleLines(x, y, barWidth, barHeight, Color.White);
            
            // Текст
            string timeLeft = $"{(MiningSession.SECONDS_PER_UNIT * (1 - progress)):F1}с";
            FontManager.DrawText(timeLeft, x + barWidth / 2 - 15, y + 2, 14, Color.White);
        }
        
        private void RenderNearestResourceHighlight(Player player)
        {
            if (_world == null) return;
            
            // Находим ближайший активный ресурс в радиусе интеракции
            ResourceNode? nearest = null;
            float nearestDist = float.MaxValue;
            
            foreach (var resource in _world.Resources.Values)
            {
                if (!resource.IsActive) continue;
                
                float dist = Vector2.Distance(player.Position, resource.Position);
                if (dist < INTERACTION_DISTANCE && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = resource;
                }
            }
            
            if (nearest != null)
            {
                // Подсветка ресурса
                Raylib.DrawCircleLines((int)nearest.Position.X, (int)nearest.Position.Y, 
                    30f, new Color(255, 255, 0, 200));
                
                // Подсказка о взаимодействии
                Vector2 screenPos = CameraManager.Instance.WorldToScreen(nearest.Position);
                string hint = "ЛКМ для взаимодействия";
                FontManager.DrawText(hint, (int)screenPos.X - 60, (int)screenPos.Y - 40, 12, Color.Yellow);
            }
        }
        
        public event Action? OnMineButtonClick;
        
        public ResourceNode? SelectedNode => _selectedNode;
        public bool IsMenuOpen => _showContextMenu;
        
        public void CloseMenu()
        {
            _showContextMenu = false;
            _selectedNode = null;
        }
    }
}
