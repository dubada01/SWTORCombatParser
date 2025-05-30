using SWTORCombatParser.DataStructures.RoomOverlay;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.Room;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.Shapes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;


namespace SWTORCombatParser.ViewModels.Overlays.Room
{
    public class RoomOverlayViewModel : BaseOverlayViewModel
    {
        private DateTime _startTime;

        private bool _isActive = false;
        private RoomOverlay _roomOverlay;
        private List<RoomOverlaySettings> _settings;
        public List<Ellipse> Hazards { get; set; }
        private RoomOverlaySettings _currentCombatOverlaySettings;

        private string _currentBossName;

        private RoomHazard _currentHazard;
        private Bitmap imagePath;
        private bool _isTriggered;
        private bool viewExtraInfo;
        public override bool ShouldBeVisible => _isTriggered;
        public RoomOverlayViewModel(string overlayName) : base(overlayName)
        {
            _roomOverlay = new RoomOverlay(this);
            MainContent = _roomOverlay;
            _settings = RoomOverlayLoader.GetRoomOverlaySettings();
            CombatLogStreamer.CombatUpdated += NewInCombatLogs;
            EncounterTimerTrigger.BossCombatDetected += OnBossBossCombatDetected;
            
            
            ImagePath = new Bitmap(
                AssetLoader.Open(new Uri("avares://Orbs/resources/RoomOverlays/IP-CPT/Empty.png")));
            _isTriggered = true;
            UpdateVisibility();
            Dispatcher.UIThread.Invoke(() =>
            {
                IsActive = true;
            });
            _isTriggered = false;
        }

        private void OnBossBossCombatDetected(string arg1, string arg2, string arg3)
        {
            if (!Active || _isTriggered)
                return;
            ImagePath = new Bitmap(
                AssetLoader.Open(new Uri("avares://Orbs/resources/RoomOverlays/IP-CPT/Empty.png")));
            _isTriggered = true;
            UpdateVisibility();
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
                    ImagePath =new Bitmap(
                        AssetLoader.Open(new Uri("avares://Orbs/resources/RoomOverlays/NAHUT/NAHUT_Room.jpg")));
                    var hazard = new NAHUT_Hazard(_roomOverlay, _currentCombatOverlaySettings);
                    _currentHazard = hazard;
                }
                Dispatcher.UIThread.Invoke(() =>
                {
                    IsActive = true;
                });
                _currentHazard?.Start();
            }
        }
        public bool ViewExtraInfo
        {
            get => viewExtraInfo; set
            {
                DefaultRoomOverlayManager.SetViewExtra(value);
                this.RaiseAndSetIfChanged(ref viewExtraInfo, value);
            }
        }
        public bool IsActive { get => _isActive; set => this.RaiseAndSetIfChanged(ref _isActive, value); }
        public Bitmap CharImagePath => new Bitmap(
            AssetLoader.Open(new Uri("avares://Orbs/resources/RoomOverlays/PlayerLocation.png")));
        public Bitmap ImagePath
        {
            get => imagePath; set
            {
                this.RaiseAndSetIfChanged(ref imagePath, value);
            }
        }
        private void NewInCombatLogs(CombatStatusUpdate obj)
        {
            if (!Active)
                return;
            if (obj.Type == UpdateType.Stop)
            {
                _isTriggered = false;
                _currentBossName = "";
                Dispatcher.UIThread.Invoke(() =>
                {
                    IsActive = false;
                });
                _currentHazard?.Stop();
            }
        }
        private void OnNewImageFromHazard(Uri newPath)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ImagePath = new Bitmap(
                    AssetLoader.Open(newPath));
            });
        }
    }
}
