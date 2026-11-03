using System;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Timers;
using System.Collections.Generic;
using Avalonia.Media;
using Newtonsoft.Json;

namespace SWTORCombatParser.DataStructures
{
    public class ColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            // Serialize color as a hex string (e.g., #FF1A8814)
            writer.WriteValue(value.ToString());
        }

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // Read the hex string and convert it to an Avalonia Color
            var colorString = (string)reader.Value;
            return Color.Parse(colorString);
        }
    }
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
        Or,
        IsTimerTriggered,
        NewEntitySpawn,
        AbsorbShield,
        EntityDeath,
        VariableCheck,
        EffectCharges
    }
    
    public class Timer
    {
        private bool isEnabled;
        private string specificDifficulty;
        private double _hpPercentageDisplayBuffer = 5;

        public string TimerSource { get; set; }
        public string CharacterDiscipline { get; set; }
        public bool IsSubTimer { get; set; }
        public string ParentTimerId { get; set; }
        public string Id { get; set; }
        public string ShareId { get; set; }
        public bool IsEnabled
        {
            get => isEnabled;
            set => isEnabled = value;
        }
        public bool ShowIconIfPossible { get; set; }
        public string Source { get; set; } = "";
        public bool SourceIsLocal { get; set; }
        public bool SourceIsAnyButLocal { get; set; }
        public string Target { get; set; } = "";
        public bool TargetIsLocal { get; set; }
        public bool ShowTargetOnTimerUI { get; set; }
        public bool TargetIsAnyButLocal { get; set; }
        public double HPPercentage { get; set; }
        public double HPPercentageUpper { get; set; }

        public double HPPercentageDisplayBuffer
        {
            get => _hpPercentageDisplayBuffer;
            set
            {
                _hpPercentageDisplayBuffer = value;
                if (HPPercentageUpper == 0)
                    HPPercentageUpper = HPPercentage + value;
            }
        }

        public double AbsorbValue { get; set; }
        public string Name { get; set; }
        public TimerKeyType TriggerType { get; set; }
        public string SelectedCancelTimerId { get; set; }
        public string SeletedTimerIsActiveId { get; set; }
        public bool TrackOutsideOfCombat { get; set; }
        public string ExperiationTimerId { get; set; }
        public double CombatTimeElapsed { get; set; }
        public string Ability { get; set; } = "";
        public string Effect { get; set; } = "";
        public bool ResetOnEffectLoss { get; set; }
        public bool IsPeriodic { get; set; }
        public int Repeats { get; set; }
        public bool CanBeRefreshed { get; set; }
        public bool DontRefresh { get;set; }
        public List<string> AbilitiesThatRefresh { get; set; } = new List<string>();
        public bool IsAlert { get; set; }
        public string AlertText { get; set; }
        public double AlertDuration { get; set; }
        public double DurationSec { get; set; }
        public double HideUntilSec { get; set; }
        [JsonConverter(typeof(ColorConverter))]
        public Color TimerColor { get; set; }
        public bool UseAudio { get; set; }
        public string CustomAudioPath { get; set; }
        public int AudioStartTime { get; set; } = 4;
        public string SpecificBoss { get; set; }
        public string SpecificEncounter { get; set; }
        public bool ActiveForStory { get; set; }
        public bool ActiveForVeteran { get; set; }
        public bool ActiveForMaster { get; set; }
        public string SpecificDifficulty
        {
            get => specificDifficulty; set
            {
                specificDifficulty = value;
                if (specificDifficulty == "All")
                {
                    ActiveForStory = true;
                    ActiveForVeteran = true;
                    ActiveForMaster = true;
                }
                if (specificDifficulty == "Story")
                    ActiveForStory = true;
                if (specificDifficulty == "Veteran")
                    ActiveForVeteran = true;
                if (specificDifficulty == "Master")
                    ActiveForMaster = true;

            }
        }
        public bool IsHot { get; set; }
        public bool IsBuiltInDot { get; set; }
        public bool IsBuiltInDefensive { get; set; }
        public bool IsBuiltInOffensive { get; set; }
        public bool IsImportedFromSP { get; set; }
        public int TimerRev { get; set; }
        public bool IsUserAddedTimer { get; set; }
        public bool IsMechanic { get; set; }
        public Timer Clause1 { get; set; }
        public Timer Clause2 { get; set; }
        public string VariableName { get; set; }
        public string ModifyVariableName { get; set; }
        public VariableModifications ModifyVariableAction { get; set; }
        public int VariableModificationValue { get; set; }
        public VariableComparisons ComparisonAction { get; set; }
        public int ComparisonVal { get; set; }
        public int ComparisonValMin { get; set; }
        public int ComparisonValMax { get; set; }
        public bool ShouldModifyVariable { get; set; }
        public bool UseVisualsAndModify { get; set; }
        public bool IsCooldownTimer { get; set; }
        public bool ChargesSetVariable { get; set; }
        public string ChargesSetVariableName { get; set; }
        public bool ChangeBackgroundNearExpiration { get; set; }
        public Timer Copy()
        {
            return new Timer()
            {
                TimerSource = TimerSource,
                Id = Id,
                SelectedCancelTimerId = SelectedCancelTimerId,
                ParentTimerId = ParentTimerId,
                Name = Name,
                IsSubTimer = IsSubTimer,
                Source = GetTimerSourceType(this),
                SourceIsLocal = SourceIsLocal,
                SourceIsAnyButLocal = SourceIsAnyButLocal,
                Target = GetTimerTargetType(this),
                ShowTargetOnTimerUI = ShowTargetOnTimerUI,
                AlertDuration = AlertDuration,
                CombatTimeElapsed = CombatTimeElapsed,
                TargetIsLocal = TargetIsLocal,
                TargetIsAnyButLocal = TargetIsAnyButLocal,
                HPPercentage = HPPercentage,
                HPPercentageUpper = HPPercentageUpper,
                HPPercentageDisplayBuffer = HPPercentageDisplayBuffer,
                AbsorbValue = AbsorbValue,
                TriggerType = TriggerType,
                Ability = Ability,
                Effect = Effect,
                ResetOnEffectLoss = ResetOnEffectLoss,
                IsPeriodic = IsPeriodic,
                Repeats = Repeats,
                IsAlert = IsAlert,
                AlertText = AlertText,
                DurationSec = DurationSec,
                TimerColor = TimerColor,
                SpecificBoss = SpecificBoss,
                SpecificEncounter = SpecificEncounter,
                SpecificDifficulty = SpecificDifficulty,
                ExperiationTimerId = TriggerType == TimerKeyType.TimerExpired ? ExperiationTimerId : null,
                IsEnabled = IsEnabled,
                TrackOutsideOfCombat = TrackOutsideOfCombat,
                CanBeRefreshed = CanBeRefreshed,
                DontRefresh = DontRefresh,
                AbilitiesThatRefresh = AbilitiesThatRefresh,
                IsHot = IsHot,
                IsBuiltInDefensive = IsBuiltInDefensive,
                IsBuiltInOffensive = IsBuiltInOffensive,
                IsMechanic = IsMechanic,
                HideUntilSec = HideUntilSec,
                UseAudio = UseAudio,
                CustomAudioPath = CustomAudioPath,
                AudioStartTime = AudioStartTime,
                Clause1 = Clause1 != null ? Clause1.Copy() : Clause1,
                Clause2 = Clause2 != null ? Clause2.Copy() : Clause2,
                ActiveForStory = ActiveForStory,
                ActiveForVeteran = ActiveForVeteran,
                ActiveForMaster = ActiveForMaster,
                ComparisonVal = ComparisonVal,
                ComparisonAction = ComparisonAction,
                VariableName = VariableName,
                ComparisonValMax = ComparisonValMax,
                ComparisonValMin = ComparisonValMin,
                ModifyVariableAction = ModifyVariableAction,
                ModifyVariableName = ModifyVariableName,
                VariableModificationValue = VariableModificationValue,
                ShouldModifyVariable = ShouldModifyVariable,
                UseVisualsAndModify = UseVisualsAndModify,
                TimerRev = TimerRev,
                IsCooldownTimer = IsCooldownTimer,
                ChargesSetVariable = ChargesSetVariable,
                ChargesSetVariableName = ChargesSetVariableName,
                SeletedTimerIsActiveId = TriggerType == TimerKeyType.IsTimerTriggered ? SeletedTimerIsActiveId : null,
                IsUserAddedTimer = IsUserAddedTimer,
                ChangeBackgroundNearExpiration = ChangeBackgroundNearExpiration,
                ShowIconIfPossible = ShowIconIfPossible
            };

        }

        private string GetTimerTargetType(Timer legacyTimer)
        {
            if (legacyTimer.TargetIsLocal)
                return TimerTargetType.LocalPlayer.ToString();
            if (legacyTimer.TargetIsAnyButLocal)
                return TimerTargetType.NotLocalPlayer.ToString();
            if (legacyTimer.Target == "Any")
                return TimerTargetType.Any.ToString();
            return legacyTimer.Target;
        }

        private string GetTimerSourceType(Timer legacyTimer)
        {
            if (legacyTimer.SourceIsLocal)
                return TimerTargetType.LocalPlayer.ToString();
            if (legacyTimer.SourceIsAnyButLocal)
                return TimerTargetType.NotLocalPlayer.ToString();
            if (legacyTimer.Source == "Any")
                return TimerTargetType.Any.ToString();
            return legacyTimer.Source;
        }
    }
}
