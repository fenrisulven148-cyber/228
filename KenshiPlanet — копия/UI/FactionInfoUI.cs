using Raylib_cs;
using System;
using System.Numerics;
using KenshiPlanet.Factions;
using KenshiPlanet.Core;

namespace KenshiPlanet.UI
{
    public static class FactionInfoUI
    {
        public static void DrawFactionInfo(Faction faction, int screenWidth, int screenHeight)
        {
            if (faction == null) return;

            int x = 20, y = 100, lineHeight = 25;

            Raylib.DrawRectangle(x - 10, y - 10, 400, 300, new Color(0, 0, 0, 180));
            Raylib.DrawRectangleLines(x - 10, y - 10, 400, 300, Color.White);

            FontManager.DrawText(faction.Name, x, y, 24, faction.Color);
            y += lineHeight + 10;

            FontManager.DrawText($"Тип: {GetFactionTypeName(faction.Type)}", x, y, 18, Color.White);
            y += lineHeight;

            if (!string.IsNullOrEmpty(faction.Description))
            {
                DrawMultilineText(faction.Description, x, y, 380, 16, Color.LightGray);
                y += 60;
            }

            if (faction.UniqueBuildings.Count > 0)
            {
                FontManager.DrawText("Уникальные здания:", x, y, 16, Color.Gold);
                y += 20;
                DrawMultilineText(string.Join(", ", faction.UniqueBuildings), x + 10, y, 360, 14, Color.White);
                y += 40;
            }

            y += 10;
            FontManager.DrawText("Характеристики:", x, y, 18, Color.White);
            y += 25;

            DrawModifierBar("Производство",  faction.ResourceProductionMultiplier, x, y); y += 25;
            DrawModifierBar("Боевая мощь",   faction.CombatEffectiveness,          x, y); y += 25;
            DrawModifierBar("Строительство", faction.BuildingSpeed,                x, y); y += 25;
            DrawModifierBar("Дипломатия",    faction.DiplomacyEffect,              x, y); y += 25;

            FontManager.DrawText($"Предпочтительный ресурс: {GetResourceName(faction.PreferredResource)}", x, y, 16, Color.White);
        }

        private static string GetFactionTypeName(FactionType type) => type switch
        {
            FactionType.Military      => "Военная",
            FactionType.Economic      => "Экономическая",
            FactionType.Technological => "Технологическая",
            FactionType.Magical       => "Магическая",
            FactionType.Agricultural  => "Аграрная",
            FactionType.Nomadic       => "Кочевая",
            FactionType.Religious     => "Религиозная",
            FactionType.Industrial    => "Промышленная",
            FactionType.Stealth       => "Скрытная",
            FactionType.Diplomatic    => "Дипломатическая",
            _                         => "Универсальная"
        };

        private static string GetResourceName(string resource) => resource.ToLower() switch
        {
            "food"      => "Еда",
            "materials" => "Материалы",
            "weapons"   => "Оружие",
            "tools"     => "Инструменты",
            "luxury"    => "Предметы роскоши",
            _           => resource
        };

        private static void DrawModifierBar(string label, float value, int x, int y)
        {
            FontManager.DrawText($"{label}:", x, y, 14, Color.White);

            string valueText = $"{value:F2}x";
            FontManager.DrawText(valueText, x + 120, y, 14, Color.White);

            Color barColor = value < 0.9f ? Color.Red : (value > 1.1f ? Color.Green : Color.Yellow);
            int barWidth = (int)(value * 80);
            Raylib.DrawRectangle(x + 180, y + 2, barWidth, 12, barColor);
            Raylib.DrawRectangleLines(x + 180, y + 2, 80, 12, Color.White);
        }

        private static void DrawMultilineText(string text, int x, int y, int maxWidth, int fontSize, Color color)
        {
            string[] words = text.Split(' ');
            string currentLine = "";
            int currentY = y;

            foreach (string word in words)
            {
                string testLine = currentLine == "" ? word : currentLine + " " + word;
                float textWidth = FontManager.MeasureText(testLine, fontSize);

                if (textWidth <= maxWidth)
                {
                    currentLine = testLine;
                }
                else
                {
                    if (currentLine != "")
                    {
                        FontManager.DrawText(currentLine, x, currentY, fontSize, color);
                        currentY += fontSize + 2;
                    }
                    currentLine = word;
                }
            }

            if (currentLine != "")
                FontManager.DrawText(currentLine, x, currentY, fontSize, color);
        }

        public static void DrawFactionComparison(Faction faction1, Faction faction2, int screenWidth, int screenHeight)
        {
            if (faction1 == null || faction2 == null) return;

            int x = screenWidth - 420, y = 100, lineHeight = 25;

            Raylib.DrawRectangle(x - 10, y - 10, 400, 250, new Color(0, 0, 0, 180));
            Raylib.DrawRectangleLines(x - 10, y - 10, 400, 250, Color.White);

            FontManager.DrawText("Сравнение фракций", x, y, 20, Color.White); y += 30;

            FontManager.DrawText(faction1.Name, x,       y, 16, faction1.Color);
            FontManager.DrawText("VS",          x + 150, y, 16, Color.White);
            FontManager.DrawText(faction2.Name, x + 200, y, 16, faction2.Color);
            y += 25;

            DrawComparisonBar("Производство",  faction1.ResourceProductionMultiplier, faction2.ResourceProductionMultiplier, x, y); y += 20;
            DrawComparisonBar("Боевая мощь",   faction1.CombatEffectiveness,          faction2.CombatEffectiveness,          x, y); y += 20;
            DrawComparisonBar("Строительство", faction1.BuildingSpeed,                faction2.BuildingSpeed,                x, y); y += 20;
            DrawComparisonBar("Дипломатия",    faction1.DiplomacyEffect,              faction2.DiplomacyEffect,              x, y);
        }

        private static void DrawComparisonBar(string label, float value1, float value2, int x, int y)
        {
            FontManager.DrawText($"{label}:", x, y, 12, Color.White);

            Raylib.DrawRectangle(x + 100, y + 2, (int)(value1 * 60), 10, Color.Blue);
            Raylib.DrawRectangle(x + 200, y + 2, (int)(value2 * 60), 10, Color.Red);

            FontManager.DrawText($"{value1:F2}", x + 100, y + 15, 10, Color.White);
            FontManager.DrawText($"{value2:F2}", x + 200, y + 15, 10, Color.White);
        }
    }
}
