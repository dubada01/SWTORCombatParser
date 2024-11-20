using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayOptionViewModel : INotifyPropertyChanged
    {
        private bool isSelected = false;
        public string HelpText => GetHelpFromType();
        public OverlayOptionViewModel()
        {
            MetricColorLoader.OnOverlayTypeColorUpdated += UpdateColor;
        }

        private void UpdateColor(OverlayType type)
        {
            if (type == Type)
                OnPropertyChanged("Type");
        }

        private string GetHelpFromType()
        {
            switch (Type)
            {
                case OverlayType.APM:
                    return "Abilities Per Minute: The number of abilities activated per minutes of combat";
                case OverlayType.DPS:
                    return "Damage Per Second: The total effective damage (damage that reduced enemy HP) per second of combat.\r\nThis overlay bar is colored by focus damage and \"fluff\" damage\r\n*has leaderboard support*";
                case OverlayType.BurstDPS:
                    return "The max DPS over a 10 second period";
                case OverlayType.FocusDPS:
                    return "The effective damage to targets deemed not to be fluff per second of combat\r\n*has leaderboard support*";
                case OverlayType.HPS:
                    return "Heals Per Second: The raw ammount of healing per second of combat\r\n*has leaderboard support*";
                case OverlayType.EHPS:
                    return "Effective Heals Per Second: The ammount of effective heals (heals that increased ally HP) plus shielding per second of combat.\r\nThis overlay bar is colored by shielding and healing\r\n*has leaderboard support*";
                case OverlayType.HealReactionTime:
                    return "The number of times a heal ability was activated within 2 seconds of the target of this heal losing >10% of their HP.";
                case OverlayType.HealReactionTimeRatio:
                    return "The ratio of Heal Reaction Time per number of heal abilities activated";
                case OverlayType.TankHealReactionTime:
                    return "The same as Heal Reaction Time, but only calculated if the target of the heal is a tank";
                case OverlayType.BurstEHPS:
                    return "The max EHPS over a 10 second period";
                case OverlayType.ProvidedAbsorb:
                    return "The total provided absorb to other players";
                case OverlayType.Mitigation:
                    return "The sum of damage avoided and absorbed. \r\n*has leaderboard support*";
                case OverlayType.DamageSavedDuringCD:
                    return "An estimation of the damage avoided by the use of cooldowns.\r\nCalculated by comparing the average damage taken from abilities when cooldowns were NOT active vs when they WERE active.";
                case OverlayType.ShieldAbsorb:
                    return "The value of damage absorbed by tank shielding";
                case OverlayType.AbsorbProvided:
                    return "The total provided absorb to other players";
                case OverlayType.DamageAvoided:
                    return "Avoided damage is estimated by using historical values for times that an ability was not dodged/missed/etc... multiplied by the number of times a hit with the same ability Id was a dodged/missed/etc...";
                case OverlayType.Threat:
                    return "The total threat from all enemies";
                case OverlayType.DamageTaken:
                    return "The effective damage taken. The bar is split by total damage taken with the portion of mitigated damage colored differently";
                case OverlayType.BurstDamageTaken:
                    return "The max damage taken over a 10 second period";
                case OverlayType.InterruptCount:
                    return "The count of effective interrupt usages";
                case OverlayType.ThreatPerSecond:
                    return "The total threat from all enemies per second";
                case OverlayType.NonEDPS:
                    return "Raw DPS: The total raw damage per second of combat. This includes overdamage that does not reduce enemy HP (as it happens after they've already died";
                case OverlayType.Damage:
                    return "Total effective damage";
                case OverlayType.RawDamage:
                    return "Total raw damage";
                case OverlayType.EffectiveHealing:
                    return "Total effective healing and shielding";
                case OverlayType.RawHealing:
                    return "Total raw healing";
                case OverlayType.CritPercent:
                    return "Current percentage of critical hits";
                case OverlayType.SingleTargetDPS:
                    return "Max Single target DPS";
                case OverlayType.SingleTargetEHPS:
                    return "Max single target EHPS";
                case OverlayType.CustomVariable:
                case OverlayType.None:
                    return "Unexpected Overlay";
                default:
                    return "";

            }
        }

        public OverlayType Type { get; set; }
        public bool IsSelected
        {
            get => isSelected; set
            {
                isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
