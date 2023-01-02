using System.Collections.Generic;
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
        TargetChanged,
        DamageTaken,
        HasEffect,
        IsFacing,
        And,
        Or
    }
    public class Timer
    {
        private bool isEnabled;
        public string TimerSource { get; set; }
        public string CharacterDiscipline { get; set; }
        public string Id { get; set; }
        public string ShareId { get; set; }
        public bool IsEnabled {
            get => isEnabled;
            set => isEnabled = value;
        }
        public string Source { get; set; } = "";
        public bool SourceIsLocal { get; set; }
        public bool SourceIsAnyButLocal { get; set; }
        public string Target { get; set; } = "";
        public bool TargetIsLocal { get; set; }
        public bool TargetIsAnyButLocal { get; set; }
        public double HPPercentage { get; set; }
        public double HPPercentageDisplayBuffer { get; set; } = 5;
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
        public string AlertText { get; set; }
        public double AlertDuration { get; set; }
        public double DurationSec { get; set; }
        public double HideUntilSec { get; set; }
        public Color TimerColor { get; set; }
        public bool UseAudio { get; set; }
        public string CustomAudioPath { get; set; }
        public int AudioStartTime { get; set; } = 4;
        public string SpecificBoss { get; set; }
        public string SpecificEncounter { get; set; }
        public string SpecificDifficulty {get;set;}
        public bool IsHot { get; set; }
        public bool IsBuiltInDot { get; set; }
        public bool IsMechanic { get; set; }
        public Timer Clause1 { get; set; }
        public Timer Clause2 { get; set; }
        public Timer Copy()
        {
            return new Timer()
            {
                Id = Id,
                Name = Name,
                Source = Source,
                SourceIsLocal = SourceIsLocal,
                SourceIsAnyButLocal = SourceIsAnyButLocal,
                Target = Target,
                AlertDuration = AlertDuration,
                CombatTimeElapsed = CombatTimeElapsed,
                TargetIsLocal = TargetIsLocal,
                TargetIsAnyButLocal= TargetIsAnyButLocal,
                HPPercentage = HPPercentage,
                HPPercentageDisplayBuffer = HPPercentageDisplayBuffer,
                TriggerType = TriggerType,
                Ability = Ability,
                Effect = Effect,
                IsPeriodic = IsPeriodic,
                Repeats = Repeats,
                IsAlert = IsAlert,
                AlertText = AlertText,
                DurationSec = DurationSec,
                TimerColor = TimerColor,
                SpecificBoss = SpecificBoss,
                SpecificEncounter = SpecificEncounter,
                SpecificDifficulty = SpecificDifficulty,
                ExperiationTimerId = ExperiationTimerId,
                IsEnabled = IsEnabled,
                TrackOutsideOfCombat = TrackOutsideOfCombat,
                CanBeRefreshed = CanBeRefreshed,
                AbilitiesThatRefresh = AbilitiesThatRefresh,
                IsHot = IsHot,
                IsMechanic = IsMechanic,
                HideUntilSec = HideUntilSec,
                UseAudio = UseAudio,
                CustomAudioPath = CustomAudioPath,
                AudioStartTime= AudioStartTime,
                Clause1 = Clause1,
                Clause2 = Clause2
            };
        }
    }
}
