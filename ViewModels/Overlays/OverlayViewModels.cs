using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;
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
        private string _currentCharacterName = "None";
        private bool overlaysLocked;
        private LeaderboardType selectedLeaderboardType;
        private TimersCreationViewModel _timersViewModel;
        private RaidHotsConfigViewModel _raidHotsConfigViewModel;
        private BossFrameConfigViewModel _bossFrameViewModel;
        public RaidHOTsSteup RaidHotsConfig { get; set; }
        public TimersCreationView TimersView { get; set; }
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
            DefaultOverlayManager.Init();
            DefaultTimersManager.Init();
            var enumVals = EnumUtil.GetValues<OverlayType>().OrderBy(d=>d.ToString());
            foreach (var enumVal in enumVals.Where(e => e != OverlayType.None))
            {
                AvailableOverlayTypes.Add(enumVal);
            }
            _bossFrameViewModel = new BossFrameConfigViewModel();

            TimersView = new TimersCreationView();
            _timersViewModel = new TimersCreationViewModel();
            TimersView.DataContext = _timersViewModel;

            RaidHotsConfig = new RaidHOTsSteup();
            _raidHotsConfigViewModel = new RaidHotsConfigViewModel();
            RaidHotsConfig.DataContext = _raidHotsConfigViewModel;
            
            OnPropertyChanged("RaidHotsConfig");
            OnPropertyChanged("TimersView");
        }
        private void CharacterLoaded(Entity character)
        {
            ResetOverlays();
            App.Current.Dispatcher.Invoke(() =>
            {
                _currentCharacterName = character.Name;
                _overlayDefaults = DefaultOverlayManager.GetDefaults(_currentCharacterName);
                if (_overlayDefaults.First().Value.Locked)
                {
                    OverlaysLocked = true;
                    OnPropertyChanged("OverlaysLocked");
                }
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
            if (_currentOverlays.Any(o => o.CreatedType == overlayType))
                return;

            var viewModel = new OverlayInstanceViewModel(overlayType);
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
            if (OverlaysLocked)
                viewModel.LockOverlays();
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
            _timersViewModel.HideTimers();
            _currentOverlays.Clear();
        }
        public bool OverlaysLocked
        {
            get => overlaysLocked;
            set
            {
                overlaysLocked = value;
                _timersViewModel.UpdateLock(value);
                if (overlaysLocked)
                    _bossFrameViewModel.LockOverlays();
                else
                    _bossFrameViewModel.UnlockOverlays();
                ToggleOverlayLock();
            }
        }

        private void ToggleOverlayLock()
        {
            if (!OverlaysLocked)
                _currentOverlays.ForEach(o => o.UnlockOverlays());
            else
                _currentOverlays.ForEach(o => o.LockOverlays());
            DefaultOverlayManager.SetLockedState(OverlaysLocked, _currentCharacterName);
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

        internal void LiveParseStarted()
        {
            _raidHotsConfigViewModel.LiveParseActive();
        }
    }
}
