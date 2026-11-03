using SWTORCombatParser.Model.Overlays;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;


namespace SWTORCombatParser.Utilities.Converters
{
    public class OverlayTypeToReadableNameConverter : IValueConverter
    {
        private static Dictionary<OverlayType, string> overlayTypeToString = new Dictionary<OverlayType, string>()
{
    { OverlayType.None, "None" },
    { OverlayType.DPS, "Effective DPS" },
        { OverlayType.Damage, "Damage" },
    { OverlayType.NonEDPS, "Raw DPS" },
    { OverlayType.BurstDPS, "Burst DPS" },
    { OverlayType.FocusDPS, "Boss DPS" },
    { OverlayType.HPS, "HPS" },
    { OverlayType.EHPS, "EHPS + Shield" },
    { OverlayType.BurstEHPS, "Burst EHPS" },
    { OverlayType.HealReactionTime, "# of <2sec Reactions" },
    { OverlayType.HealReactionTimeRatio, "Ratio of <2sec heals" },
    { OverlayType.TankHealReactionTime, "Heal Reaction: Tanks" },
    { OverlayType.Mitigation, "Damage Mitigation" },
    { OverlayType.DamageSavedDuringCD, "Damage Saved During CDs" },
    { OverlayType.ShieldAbsorb, "Tank Shielding" },
    { OverlayType.DamageAvoided, "Damage Avoided" },
            {OverlayType.BurstDamageTaken, "Burst Damage Taken" },
            { OverlayType.DamageTaken, "Damage Taken"},
    { OverlayType.ProvidedAbsorb, "Provided Absorb" },
    { OverlayType.APM, "APM" },
    { OverlayType.InterruptCount, "Interrupt Count" },
    { OverlayType.ThreatPerSecond, "Threat per second" },
    { OverlayType.RawDamage, "Raw Damage" },
    { OverlayType.RawHealing, "Raw Healing" },
    { OverlayType.EffectiveHealing,"Effective Healing"},
        { OverlayType.CritPercent,"Crit %"},
            {OverlayType.SingleTargetDPS,"Single Target DPS" },
            {OverlayType.SingleTargetEHPS, "Single Target EHPS" },
            {OverlayType.CleanseCount, "Cleanse Count" },
            {OverlayType.CleanseSpeed, "Cleanse Speed" }
};
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value.GetType() == typeof(List<OverlayType>))
            {
                List<string> typeStrings = new List<string>();
                foreach (var type in (List<OverlayType>)value)
                {
                    typeStrings.Add(GetStringForType(type));
                }
                return typeStrings;
            }
            return GetStringForType(value);
        }

        private static string GetStringForType(object value)
        {
            OverlayType oType = (OverlayType)value;
            if (overlayTypeToString.ContainsKey(oType))
            {
                return overlayTypeToString[oType];
            }
            return oType.ToString();
        }
        private static OverlayType GetTypeForString(object value)
        {
            string oType = (string)value;
            Dictionary<string, OverlayType> stringToOverlayType = overlayTypeToString.ToDictionary(x => x.Value, x => x.Key);
            if (stringToOverlayType.ContainsKey(oType))
            {
                return stringToOverlayType[oType];
            }
            return Enum.Parse<OverlayType>(oType);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return GetTypeForString(value);
        }
    }
}
