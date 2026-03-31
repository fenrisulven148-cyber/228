using Raylib_cs;
using System.Collections.Generic;
using System.Numerics;
using KenshiPlanet.Factions;
using KenshiPlanet.Core;

namespace KenshiPlanet.World
{
    public class Settlement
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Vector2 Position { get; set; }
        public SettlementType Type { get; set; }
        public int FactionId { get; set; }
        public float Population { get; set; } = 100.0f;
        public float Health { get; set; } = 1000.0f;
        public float WallsHealth { get; set; } = 5000.0f;
        public List<Building> Buildings { get; set; } = new();
        public float Radius => Type switch
        {
            SettlementType.Capital => 500.0f,
            SettlementType.City    => 300.0f,
            SettlementType.Village => 150.0f,
            _                      => 100.0f
        };

        public Settlement(int id, string name, Vector2 position, SettlementType type, int factionId)
        {
            Id = id; Name = name; Position = position; Type = type; FactionId = factionId;
            GenerateBuildings();
        }

        private void GenerateBuildings()
        {
            int buildingCount = Type switch
            {
                SettlementType.Capital => 50,
                SettlementType.City    => 30,
                SettlementType.Village => 10,
                _                      => 5
            };

            Buildings.Add(new Building(BuildingType.Market, Position));
            Buildings.Add(new Building(BuildingType.House,  Position));

            if (Type != SettlementType.Village)
            {
                Buildings.Add(new Building(BuildingType.Hospital, Position));
                Buildings.Add(new Building(BuildingType.Barracks, Position));
            }

            if (Type == SettlementType.Capital)
            {
                Buildings.Add(new Building(BuildingType.TownHall, Position));
                Buildings.Add(new Building(BuildingType.Police,   Position));
                Buildings.Add(new Building(BuildingType.Bar,      Position));
            }

            Random rng = new Random(Id);
            for (int i = 0; i < buildingCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                float dist  = (float)(rng.NextDouble() * Radius * 0.8f);
                Vector2 buildingPos = new Vector2(
                    Position.X + (float)Math.Cos(angle) * dist,
                    Position.Y + (float)Math.Sin(angle) * dist
                );
                Buildings.Add(new Building(BuildingType.House, buildingPos));
            }
        }

        public void Update(double deltaTime)
        {
            if (Health > 0 && WallsHealth > 0)
            {
                float growthRate = Type switch
                {
                    SettlementType.Capital => 0.1f,
                    SettlementType.City    => 0.05f,
                    SettlementType.Village => 0.02f,
                    _                      => 0.01f
                };
                Population += growthRate * (float)deltaTime;
            }
        }

        public void Render()
        {
            if (Type != SettlementType.Village)
                Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, (int)Radius, Color.Gray);

            foreach (var building in Buildings)
                building.Render();

            if (Type != SettlementType.Village)
            {
                DrawCheckpoint(Position.X + Radius, Position.Y);
                DrawCheckpoint(Position.X - Radius, Position.Y);
                DrawCheckpoint(Position.X, Position.Y + Radius);
                DrawCheckpoint(Position.X, Position.Y - Radius);
            }

            // Название поселения (кириллица через FontManager)
            string label = $"{Name} ({Population:F0})";
            FontManager.DrawText(label, (int)Position.X - 50, (int)Position.Y - (int)Radius - 30, 16, Color.White);
        }

        private void DrawCheckpoint(float x, float y)
        {
            Raylib.DrawRectangle((int)x - 15, (int)y - 15, 30, 30, Color.Brown);
            Raylib.DrawRectangleLines((int)x - 15, (int)y - 15, 30, 30, Color.Black);
        }
    }

    public class Building
    {
        public BuildingType Type { get; set; }
        public Vector2 Position { get; set; }
        public float Health { get; set; } = 100.0f;

        public Building(BuildingType type, Vector2 position)
        {
            Type = type; Position = position;
        }

        public void Render()
        {
            Color buildingColor = Type switch
            {
                BuildingType.House     => Color.Brown,
                BuildingType.Market    => Color.Gold,
                BuildingType.Hospital  => Color.White,
                BuildingType.Barracks  => Color.DarkGreen,
                BuildingType.TownHall  => Color.Purple,
                BuildingType.Police    => Color.Blue,
                BuildingType.Bar       => Color.Orange,
                BuildingType.Workshop  => Color.Gray,
                BuildingType.Farm      => Color.Green,
                BuildingType.Mine      => Color.Black,
                _                      => Color.Gray
            };

            int size = Type switch
            {
                BuildingType.House     => 20,
                BuildingType.Market    => 40,
                BuildingType.Hospital  => 50,
                BuildingType.Barracks  => 60,
                BuildingType.TownHall  => 80,
                _                      => 30
            };

            Raylib.DrawRectangle((int)Position.X - size/2, (int)Position.Y - size/2, size, size, buildingColor);
            Raylib.DrawRectangleLines((int)Position.X - size/2, (int)Position.Y - size/2, size, size, Color.Black);
        }
    }

    public enum BuildingType
    {
        House, Market, Hospital, Barracks, TownHall, Police, Bar, Workshop, Farm, Mine, Wall, Gate
    }
}
