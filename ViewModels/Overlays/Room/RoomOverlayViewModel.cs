using SWTORCombatParser.DataStructures.RoomOverlay;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SWTORCombatParser.ViewModels.Overlays.Room
{
    public class RoomOverlayViewModel : INotifyPropertyChanged
    {
        private DateTime _startTime;

        private bool _isActive = false;
        private RoomOverlay _roomOverlay;
        private List<RoomOverlaySettings> _settings;
        private RoomOverlaySettings _currentCombatOverlaySettings;

        private string _currentBossName;

        private RoomOverlayUpdate _currentUpdate;
        private string imagePath;
        private DispatcherTimer _dTimer;
        private bool _isTriggered;

        public RoomOverlayViewModel()
        {
            _dTimer = new DispatcherTimer();
            _roomOverlay = new RoomOverlay(this);
            _roomOverlay.Show();
            _settings = RoomOverlayLoader.GetRoomOverlaySettings();
            CombatLogStreamer.CombatUpdated += NewInCombatLogs;
            EncounterTimerTrigger.EncounterDetected += OnBossEncounterDetected;
            SetInitialPosition();
        }

        private void OnBossEncounterDetected(string arg1, string arg2, string arg3)
        {
            if (!_isActive || _isTriggered)
                return;
            ImagePath = Path.Combine("../../resources/RoomOverlays/IP-CPT", "Empty.png"); ;
            _isTriggered = true;
            _currentBossName = arg2;

            _startTime = DateTime.Now;
            // var currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(_startTime);

            _currentCombatOverlaySettings = _settings.FirstOrDefault(s => s.EncounterName == _currentBossName || s.EncounterName == "Any");
            if (_currentCombatOverlaySettings != null)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsActive = true;
                    OnPropertyChanged("IsActive");
                    _dTimer.Start();
                    _dTimer.Interval = TimeSpan.FromSeconds(0.1);
                    _dTimer.Tick += CheckForNewState;
                });

            }
        }

        public bool IsActive { get; set; }
        public string CharImagePath => "../../resources/RoomOverlays/PlayerLocation.png";
        public string ImagePath
        {
            get => imagePath; set
            {
                imagePath = value;
                OnPropertyChanged();
            }
        }
        public event Action<bool> OnLocking = delegate { };


        public bool OverlaysMoveable { get; set; }
        public void LockOverlays()
        {
            OnLocking(true);
            OverlaysMoveable = false;
            IsActive = false;
            OnPropertyChanged("IsActive");
            OnPropertyChanged("OverlaysMoveable");
        }
        public void UnlockOverlays()
        {
            OnLocking(false);
            OverlaysMoveable = true;
            IsActive = true;
            OnPropertyChanged("IsActive");
            OnPropertyChanged("OverlaysMoveable");
        }
        private void NewInCombatLogs(CombatStatusUpdate obj)
        {
            if (!_isActive)
                return;
            //if (obj.Type == UpdateType.Start)
            //{
            //    OnBossEncounterDetected("test", "test", "test");
            //}
            if (obj.Type == UpdateType.Stop)
            {
                _isTriggered = false;
                _currentBossName = "";
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsActive = false;
                    _dTimer.Stop();
                    _dTimer.Tick -= CheckForNewState;
                    OnPropertyChanged("IsActive");
                });
            }
        }

        private void CheckForNewState(object sender, EventArgs e)
        {
            var elapsedTime = (DateTime.Now - _startTime).TotalSeconds;
            var triggerdUpdate = _currentCombatOverlaySettings.UpateObjects.FirstOrDefault(u => u.DisplayTimeSecondsElapsed <= elapsedTime && u.TriggerTimeSecondeElapsed > elapsedTime);
            if (triggerdUpdate != null && _currentUpdate != triggerdUpdate)
            {
                _currentUpdate = triggerdUpdate;
                ImagePath = Path.Combine("../../resources/RoomOverlays/IP-CPT", _currentUpdate.ImageOverlayPath);
            }
            var roomTop = _currentCombatOverlaySettings.Top;
            var roomLeft = _currentCombatOverlaySettings.Left;
            var roomWidth = _currentCombatOverlaySettings.Width;
            var roomHeight = _currentCombatOverlaySettings.Height;

            var location = CombatLogStateBuilder.CurrentState.CurrentLocalCharacterPosition;
            var xFraction = (location.X - roomLeft) / roomWidth;
            var yFraction = (location.Y - roomTop) / roomHeight;
            _roomOverlay.DrawCharacter(xFraction, yFraction, location.Facing);
        }

        private void SetInitialPosition()
        {
            var defaults = DefaultRoomOverlayManager.GetDefaults();
            _isActive = defaults.Acive;
            _roomOverlay.Top = defaults.Position.Y;
            _roomOverlay.Left = defaults.Position.X;
            _roomOverlay.Width = defaults.WidtHHeight.X;
            _roomOverlay.Height = defaults.WidtHHeight.Y;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
