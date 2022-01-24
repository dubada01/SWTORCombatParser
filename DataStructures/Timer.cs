using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SWTORCombatParser.DataStructures
{
    public enum TimerKeyType
    {
        CombatStart,
        EntityHP,
        AbilityUsed,
        FightDuration,
        EffectGained,
        EffectLost,
        TimerExpired,
        TargetChanged
    }
    public class Timer
    {
        private bool isEnabled;
        public string CharacterOwner { get;set; }
        public string CharacterDiscipline { get; set; }
        public string Id { get; set; }
        public string ShareId { get; set; }
        public bool IsEnabled {
            get => isEnabled;
            set => isEnabled = value; 
        }
        public string Source { get; set; } = "";
        public bool SourceIsLocal { get; set; }
        public string Target { get; set; } = "";
        public bool TargetIsLocal { get; set; }
        public double HPPercentage { get; set; }
        public string Name { get; set; }
        public TimerKeyType TriggerType { get; set; }
        public bool TrackOutsideOfCombat { get; set; }
        public string ExperiationTimerId { get; set; }
        public double CombatTimeElapsed { get; set; }
        public string Ability { get; set; } = "";
        public string Effect { get; set; } = "";
        public bool IsPeriodic { get; set; }
        public int Repeats { get; set; }
        public bool CanBeRefreshed { get; set; }
        public List<string> AbilitiesThatRefresh { get; set; } = new List<string>();
        public bool IsAlert { get; set; }
        public double AlertDuration { get; set; }
        public double DurationSec { get; set; }
        public Color TimerColor { get; set; }
        public string SpecificBoss { get; set; }
        public string SpecificEncounter { get; set; }
        public Timer Copy()
        {
            return new Timer()
            {
                Id = Id,
                Name = Name,
                Source = Source,
                SourceIsLocal = SourceIsLocal,
                Target = Target,
                AlertDuration = AlertDuration,
                CombatTimeElapsed = CombatTimeElapsed,
                TargetIsLocal = TargetIsLocal,
                HPPercentage = HPPercentage,
                TriggerType = TriggerType,
                Ability = Ability,
                Effect = Effect,
                IsPeriodic = IsPeriodic,
                Repeats = Repeats,
                IsAlert = IsAlert,
                DurationSec = DurationSec,
                TimerColor = TimerColor,
                SpecificBoss = SpecificBoss,
                SpecificEncounter = SpecificEncounter,
                ExperiationTimerId = ExperiationTimerId,
                IsEnabled = IsEnabled,
                TrackOutsideOfCombat = TrackOutsideOfCombat,
                CanBeRefreshed = CanBeRefreshed,
                AbilitiesThatRefresh = AbilitiesThatRefresh,
            };
        }
    }
}
