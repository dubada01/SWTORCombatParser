﻿using SWTORCombatParser.DataStructures.RoomOverlay;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.Room;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Shapes;
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
        public bool OverlayEnabled
        {
            get { return _isActive; }
            set
            {
                DefaultRoomOverlayManager.SetActiveState(value);
                this.RaiseAndSetIfChanged(ref _isActive, value);
                if (!_isActive)
                {
                    _roomOverlay.Hide();
                }
                else
                {
                    _roomOverlay.Show();
                }
            }
        }
        public bool IsActive { get => _isActive; set => this.RaiseAndSetIfChanged(ref _isActive, value); }
        public string CharImagePath => "../../../resources/RoomOverlays/PlayerLocation.png";
        public string ImagePath
        {
            get => imagePath; set
            {
                this.RaiseAndSetIfChanged(ref imagePath, value);
            }
        }
        public void LockOverlays()
        {
            SetLock(true);
            OverlaysMoveable = false;
            IsActive = false;
        }
        public void UnlockOverlays()
        {
            SetLock(false);
            OverlaysMoveable = true;
            IsActive = true;
        }
        private void NewInCombatLogs(CombatStatusUpdate obj)
        {
            if (!OverlayEnabled)
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
        private void OnNewImageFromHazard(string newPath)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ImagePath = newPath;
            });
        }
        private void SetInitialPosition()
        {
            var defaults = DefaultRoomOverlayManager.GetDefaults();
            OverlayEnabled = defaults.Acive;
            ViewExtraInfo = defaults.ViewExtraData;
            _roomOverlay.SetSizeAndLocation(new Point(defaults.Position.X, defaults.Position.Y), new Point(defaults.WidtHHeight.X, defaults.WidtHHeight.Y));
        }
    }
}
