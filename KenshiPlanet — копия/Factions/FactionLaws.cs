using System;
using System.Collections.Generic;
using System.Text;
using KenshiPlanet.Economy;

namespace KenshiPlanet.Factions
{
    /// <summary>
    /// Полная система законов фракции.
    /// Лидер может менять законы — это влияет на NPC и игрока.
    /// </summary>
    public class FactionLaws
    {
        // --- Товары ---
        public HashSet<ResourceType> IllegalGoods { get; set; } = new();

        // --- Деятельность ---
        public bool SlaveryLegal          { get; set; } = false;
        public bool MercenaryWorkLegal    { get; set; } = true;
        public bool StreetFightingLegal   { get; set; } = false;
        public bool PickpocketingLegal    { get; set; } = false;
        public bool DrugsLegal            { get; set; } = false;
        public bool UnlicensedTradeLegal  { get; set; } = false;

        // --- Экономика ---
        public float TaxRate              { get; set; } = 0.10f;  // 0.0–1.0
        public float ImportTariff         { get; set; } = 0.05f;  // сбор с иностранных торговцев
        public float CrimeFine            { get; set; } = 150.0f; // штраф в золоте за преступление

        // --- Границы ---
        public bool  OpenBorders          { get; set; } = true;
        public float BorderEntryFee       { get; set; } = 0.0f;

        // --- Воинская обязанность ---
        public bool  ConscritionActive    { get; set; } = false;   // принудительный призыв

        public bool IsGoodLegal(ResourceType type) => !IllegalGoods.Contains(type);

        public bool IsActivityLegal(string activity) => activity switch
        {
            "slavery"           => SlaveryLegal,
            "mercenary"         => MercenaryWorkLegal,
            "street_fight"      => StreetFightingLegal,
            "pickpocket"        => PickpocketingLegal,
            "drugs"             => DrugsLegal,
            "unlicensed_trade"  => UnlicensedTradeLegal,
            _                   => false
        };

        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tax: {TaxRate * 100:F0}% | Tariff: {ImportTariff * 100:F0}%");
            sb.AppendLine($"Crime fine: {CrimeFine}g");
            sb.AppendLine($"Borders: {(OpenBorders ? "Open" : $"Closed ({BorderEntryFee}g)")}");
            if (SlaveryLegal)         sb.AppendLine("✓ Slavery");
            if (DrugsLegal)           sb.AppendLine("✓ Drugs");
            if (ConscritionActive)    sb.AppendLine("! Conscription");
            if (IllegalGoods.Count > 0)
                sb.AppendLine($"Illegal: {string.Join(", ", IllegalGoods)}");
            return sb.ToString();
        }
    }
}
