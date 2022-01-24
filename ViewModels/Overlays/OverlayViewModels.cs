using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Alerts;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayViewModel : INotifyPropertyChanged
    {

        private List<OverlayInstanceViewModel> _currentOverlays = new List<OverlayInstanceViewModel>();
        private Dictionary<OverlayType, DefaultOverlayInfo> _overlayDefaults = new Dictionary<OverlayType, DefaultOverlayInfo>();
        private AlertsViewModel _alertsViewModel;
        private string _currentCharacterName = "None";
        private bool overlaysLocked;
        private LeaderboardType selectedLeaderboardType;
        private TimersCreationViewModel _timersViewModel;

        public TimersCreationView TimersView { get; set; }
        public ObservableCollection<AlertTypeOption> AvailableAlerts => new ObservableCollection<AlertTypeOption>(_alertsViewModel.AvailableAlertTypes);
        public ObservableCollection<OverlayType> AvailableOverlayTypes { get; set; } = new ObservableCollection<OverlayType>();
        public List<LeaderboardType> LeaderboardTypes { get; set; } = new List<LeaderboardType>();
        public LeaderboardType SelectedLeaderboardType
        {
            get => selectedLeaderboardType;
            set
            {
                selectedLeaderboardType = value;
                Leaderboards.UpdateLeaderboardType(selectedLeaderboardType);
            }
        }
        public OverlayViewModel()
        {
            LeaderboardTypes = EnumUtil.GetValues<LeaderboardType>().ToList();
            _alertsViewModel = new AlertsViewModel();
            DefaultOverlayManager.Init();
            DefaultTimersManager.Init();
            var enumVals = EnumUtil.GetValues<OverlayType>().OrderBy(d=>d.ToString());
            foreach (var enumVal in enumVals.Where(e => e != OverlayType.None))
            {
                AvailableOverlayTypes.Add(enumVal);
            }
            TimersView = new TimersCreationView();
            _timersViewModel = new TimersCreationViewModel();
            TimersView.DataContext = _timersViewModel;
            OnPropertyChanged("TimersView");
        }
        private void CharacterLoaded(Entity character)
        {
            ResetOverlays();
            App.Current.Dispatcher.Invoke(() =>
            {
                //remove with 7.0
                if(CombatLogStateBuilder.CurrentState.LogVersion == LogVersion.Legacy)
                    _timersViewModel.SetClass(character, new DataStructures.SWTORClass() {Discipline = "Legacy" });

                _currentCharacterName = character.Name;
                _overlayDefaults = DefaultOverlayManager.GetDefaults(_currentCharacterName);
                var enumVals = EnumUtil.GetValues<OverlayType>();
                foreach (var enumVal in enumVals.Where(e => e != OverlayType.None))
                {
                    if (!_overlayDefaults.ContainsKey(enumVal))
                        continue;
                    if (_overlayDefaults[enumVal].Acive)
                        CreateOverlay(enumVal);
                }
            });

        }

        public ICommand GenerateOverlay => new CommandHandler(CreateOverlay);

        private void CreateOverlay(object type)
        {
            OverlayType overlayType = (OverlayType)type;
            if (_currentOverlays.Any(o => o.Type == overlayType))
                return;

            var viewModel = new OverlayInstanceViewModel(overlayType);
            if (OverlaysLocked)
                viewModel.LockOverlays();
            DefaultOverlayManager.SetActiveState(viewModel.Type, true, _currentCharacterName);
            viewModel.OverlayClosed += RemoveOverlay;
            viewModel.OverlaysMoveable = !OverlaysLocked;
            _currentOverlays.Add(viewModel);
            var overlay = new InfoOverlay(viewModel);
            overlay.SetPlayer(_currentCharacterName);
            if (_overlayDefaults.ContainsKey(viewModel.Type))
            {
                overlay.Top = _overlayDefaults[viewModel.Type].Position.Y;
                overlay.Left = _overlayDefaults[viewModel.Type].Position.X;
                overlay.Width = _overlayDefaults[viewModel.Type].WidtHHeight.X;
                overlay.Height = _overlayDefaults[viewModel.Type].WidtHHeight.Y;
            }
            overlay.Show();
        }

        private void RemoveOverlay(OverlayInstanceViewModel obj)
        {
            _currentOverlays.Remove(obj);
        }
        public void ResetOverlays()
        {
            _currentCharacterName = "";
            foreach (var overlay in _currentOverlays.ToList())
            {
                overlay.RequestClose();
            }
            _currentOverlays.Clear();
        }
        public bool OverlaysLocked
        {
            get => overlaysLocked;
            set
            {
                overlaysLocked = value;
                _timersViewModel.UpdateLock(value);
                ToggleOverlayLock();
            }
        }

        private void ToggleOverlayLock()
        {
            if (!OverlaysLocked)
                _currentOverlays.ForEach(o => o.UnlockOverlays());
            else
                _currentOverlays.ForEach(o => o.LockOverlays());
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        internal void LocalPlayerIdentified(Entity localPlayer)
        {
            if (localPlayer != null && _currentCharacterName != localPlayer.Name)
            {
                CharacterLoaded(localPlayer);
                _currentOverlays.ForEach(o => o.CharacterDetected(localPlayer.Name));
            }
        }
    }
}
