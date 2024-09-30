using ScottPlot;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.CombatMetaData
{
    public class CombatEfffectViewModel :ReactiveObject, INotifyPropertyChanged
    {
        private Entity characterName = new Entity();
        private Combat _currentCombat;
        private List<CombatModifier> _currentCombatModifiers;
        private EffectViewModel selectedEffect;
        private DateTime _minTime;
        private DateTime _maxTime;
        private static string selfSelf = "Self -> Self";
        private static string selfOther = "Self -> Other";
        private static string otherSelf = "Other -> Self";
        private string selectedEffectType = selfSelf;
        private string selectedOther;
        private bool otherSelectionVisible;

        public bool OtherSelectionVisible
        {
            get => otherSelectionVisible; set
            {
                otherSelectionVisible = value;
                OnPropertyChanged();
            }
        }
        public List<string> AvailableEffectTypes { get; set; } = new List<string> { selfSelf, selfOther, otherSelf };
        public List<string> AvailableOthers { get; set; } = new List<string>();

        public string SelectedEffectType
        {
            get => selectedEffectType; set
            {
                SelectedOther = "All";
                selectedEffectType = value;
                if (selectedEffectType.Contains("Other"))
                {
                    RefreshAvailableOthers();
                    OtherSelectionVisible = true;
                }
                else
                {
                    OtherSelectionVisible = false;
                }
                _currentCombatModifiers = GetReleventModifiers();
                UpdateVisibleEffects();
                OnPropertyChanged();
            }
        }
        private void RefreshAvailableOthers()
        {
            if (_currentCombat == null)
                return;
            AvailableOthers = _currentCombat.AllEntities.Where(e => e != SelectedParticipant).Select(e => e.Name).ToList();
            AvailableOthers.Insert(0, "Enemies");
            AvailableOthers.Insert(0, "Friends");
            AvailableOthers.Insert(0, "All");
            OnPropertyChanged("AvailableOthers");
        }
        public string SelectedOther
        {
            get => selectedOther; set
            {
                selectedOther = value;
                _currentCombatModifiers = GetReleventModifiers();
                UpdateVisibleEffects();
                OnPropertyChanged();
            }
        }
        public Entity SelectedParticipant
        {
            get => characterName; set
            {
                characterName = value;
                RefreshAvailableOthers();
                OnPropertyChanged();
            }
        }
        public ReactiveCommand<Unit,Unit> ClearCombatEffectsCommand => ReactiveCommand.Create(ClearCombatEffects);

        private void ClearCombatEffects()
        {
            foreach (var effect in CombatEffects)
            {
                effect.Selected = false;
            }
            SelectedEffect = null;
            OnEffectsCleared();
        }

        public ObservableCollection<EffectViewModel> CombatEffects { get; set; } = new ObservableCollection<EffectViewModel>();
        public event Action<List<CombatModifier>> OnEffectSelected = delegate { };
        public event Action OnEffectsCleared = delegate { };
        public EffectViewModel SelectedEffect
        {
            get => selectedEffect; set
            {
                selectedEffect = value;
                if (selectedEffect == null)
                    return;
                foreach (var effect in CombatEffects)
                {
                    effect.Selected = false;
                }
                selectedEffect.Selected = true;
                OnEffectSelected(_currentCombatModifiers.Where(m => m.Name == selectedEffect.Name).ToList());
            }
        }


        public void Reset()
        {
            SelectedOther = "All";
            _currentCombat = null;
        }
        public void PopulateEffectsFromCombat(Combat combat)
        {
            _currentCombat = combat;
            _minTime = _currentCombat.StartTime;
            _maxTime = _currentCombat.EndTime;
            var currentState = CombatLogStateBuilder.CurrentState;
            _currentCombatModifiers = GetReleventModifiers();
        }

        internal void UpdateBasedOnVisibleData(AxisLimits newAxisLimits)
        {

            if (_currentCombat == null)
                return;
            var minX = newAxisLimits.Left;
            var maxX = newAxisLimits.Right;
            var combatLogs = _currentCombat.GetLogsInvolvingEntity(SelectedParticipant);

            var startTime = _currentCombat.StartTime;
            if (string.IsNullOrEmpty(SelectedParticipant.Name))
                return;
            var combatLogsInView = combatLogs.Where(l => (l.TimeStamp - startTime).TotalSeconds >= minX && (l.TimeStamp - startTime).TotalSeconds <= maxX);

            if (combatLogsInView.Count() == 0)
            {
                CombatEffects.Clear();
                return;
            }
            _minTime = combatLogsInView.Min(v => v.TimeStamp);
            _maxTime = combatLogsInView.Max(v => v.TimeStamp);
            UpdateVisibleEffects();
        }

        private void UpdateVisibleEffects()
        {

            var uniqueEffects = _currentCombatModifiers.Distinct(new EffectEquivelentComparison());
            var effectsList = uniqueEffects.GroupBy(v => (v.Name, v.Source),
                v => Math.Min(v.DurationSeconds, (_maxTime - v.StartTime).TotalSeconds), (info, durations) =>
                new EffectViewModel()
                {
                    Name = info.Name,
                    Source = info.Source.Name,
                    Duration = durations.Sum(),
                    Count = durations.Count()
                }).OrderByDescending(effect => effect.Duration).ToList();
            Dispatcher.UIThread.Invoke(() =>
            {
                CombatEffects.Clear();
                effectsList.ForEach(ef => CombatEffects.Add(ef));
            });
        }
        private List<CombatModifier> GetReleventModifiers()
        {
            List<CombatModifier> releventMods = new List<CombatModifier>();
            var currentState = CombatLogStateBuilder.CurrentState;
            if (SelectedEffectType == selfSelf)
                releventMods = currentState.GetPersonalEffects(_minTime, _maxTime, SelectedParticipant);
            if (SelectedEffectType == selfOther)
                releventMods = currentState.GetEffectsWithSource(_minTime, _maxTime, SelectedParticipant).Where(e => e.Target != SelectedParticipant).ToList();
            if (SelectedEffectType == otherSelf)
                releventMods = currentState.GetEffectsWithTarget(_minTime, _maxTime, SelectedParticipant).Where(e => e.Source != SelectedParticipant).ToList();
            if (SelectedOther == "All")
            {
                return releventMods;
            }
            if (SelectedOther == "Enemies")
            {
                if (SelectedEffectType.Split("->")[0].Trim() == "Other")
                    return releventMods.Where(m => !_currentCombat.CharacterParticipants.Contains(m.Source)).ToList();
                if (SelectedEffectType.Split("->")[0].Trim() != "Other")
                    return releventMods.Where(m => !_currentCombat.CharacterParticipants.Contains(m.Target)).ToList();
            }
            if (SelectedOther == "Friends")
            {
                if (SelectedEffectType.Split("->")[0].Trim() == "Other")
                    return releventMods.Where(m => _currentCombat.CharacterParticipants.Contains(m.Source)).ToList();
                if (SelectedEffectType.Split("->")[0].Trim() != "Other")
                    return releventMods.Where(m => _currentCombat.CharacterParticipants.Contains(m.Target)).ToList();
            }
            if (SelectedEffectType.Split("->")[0].Trim() == "Other")
                return releventMods.Where(m => m.Source.Name == SelectedOther).ToList();
            if (SelectedEffectType.Split("->")[0].Trim() != "Other")
                return releventMods.Where(m => m.Target.Name == SelectedOther).ToList();
            return releventMods;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
