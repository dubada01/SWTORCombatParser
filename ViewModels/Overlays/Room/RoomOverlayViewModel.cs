using SWTORCombatParser.DataStructures.RoomOverlay;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.Room;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SWTORCombatParser.ViewModels.Overlays.Room
{
    public class RoomOverlayViewModel : INotifyPropertyChanged
    {
        private DateTime _startTime;

        private bool _isActive = false;
        private RoomOverlay _roomOverlay;
        private List<RoomOverlaySettings> _settings;
        public List<Ellipse> Hazards { get; set; }
        private RoomOverlaySettings _currentCombatOverlaySettings;

        private string _currentBossName;

        private RoomHazard _currentHazard;
        private string imagePath;
        private bool _isTriggered;
        private bool viewExtraInfo;

        public RoomOverlayViewModel()
        {
            _roomOverlay = new RoomOverlay(this);
            _roomOverlay.Show();
            _settings = RoomOverlayLoader.GetRoomOverlaySettings();
            CombatLogStreamer.CombatUpdated += NewInCombatLogs;
            EncounterTimerTrigger.EncounterDetected += OnBossEncounterDetected;
            SetInitialPosition();
        }

        private void OnBossEncounterDetected(string arg1, string arg2, string arg3)
        {
            if (!OverlayEnabled || _isTriggered)
                return;
            ImagePath = System.IO.Path.Combine("../../../resources/RoomOverlays/IP-CPT", "Empty.png"); ;
            _isTriggered = true;
            _currentBossName = arg2;

            _startTime = DateTime.Now;
            // var currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(_startTime);

            _currentCombatOverlaySettings = _settings.FirstOrDefault(s => s.EncounterName == _currentBossName || s.EncounterName == "Any");
            if (_currentCombatOverlaySettings != null)
            {
                if(_currentCombatOverlaySettings.EncounterName == "IP-CPT")
                {
                    var hazard = new IPCPT_Hazard(_roomOverlay, _currentCombatOverlaySettings, ViewExtraInfo);
                    hazard.OnNewImagePath += OnNewImageFromHazard;
                    _currentHazard = hazard;
                }
                if(_currentCombatOverlaySettings.EncounterName == "NAHUT")
                {
                    ImagePath = System.IO.Path.Combine("../../../resources/RoomOverlays/NAHUT", "NAHUT_Room.jpg"); ;
                    var hazard = new NAHUT_Hazard(_roomOverlay, _currentCombatOverlaySettings);
                    _currentHazard = hazard;
                }
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsActive = true;
                    OnPropertyChanged("IsActive");
                });
                _currentHazard?.Start();
            }
        }
        public bool ViewExtraInfo
        {
            get => viewExtraInfo; set
            {
                DefaultRoomOverlayManager.SetViewExtra(value);
                viewExtraInfo = value;
                OnPropertyChanged();
            }
        }
        public bool OverlayEnabled
        {
            get { return _isActive; }
            set
            {
                DefaultRoomOverlayManager.SetActiveState(value);
                _isActive = value;
                if (!_isActive)
                {
                    _roomOverlay.Hide();
                }
                else
                {
                    _roomOverlay.Show();
                }
                OnPropertyChanged();
            }
        }
        public bool IsActive { get; set; }
        public string CharImagePath => "../../../resources/RoomOverlays/PlayerLocation.png";
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
            if (!OverlayEnabled)
                return;
            if (obj.Type == UpdateType.Stop)
            {
                _isTriggered = false;
                _currentBossName = "";
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsActive = false;
                    OnPropertyChanged("IsActive");
                });
                _currentHazard?.Stop();
            }
        }
        private void OnNewImageFromHazard(string newPath)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                ImagePath = newPath;
                OnPropertyChanged("ImagePath");
            });
        }
        private void SetInitialPosition()
        {
            var defaults = DefaultRoomOverlayManager.GetDefaults();
            OverlayEnabled = defaults.Acive;
            ViewExtraInfo = defaults.ViewExtraData;
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
