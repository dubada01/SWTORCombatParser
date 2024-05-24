﻿using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.RoomOverlay;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Overlay.Room;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using Timer = System.Timers.Timer;

namespace SWTORCombatParser.Model.Overlays
{
    public class IPCPT_Hazard : RoomHazard
    {
        private RoomOverlaySettings _overlaySettings;
        private RoomOverlay _roomOverlay;
        private Timer _timer;
        private DateTime _startTime;
        private RoomOverlayUpdate _currentUpdate;
        private bool _viewExtraInfo;

        public event Action<string> OnNewImagePath = delegate { };
        public IPCPT_Hazard(RoomOverlay roomView, RoomOverlaySettings settings, bool useExtraInfo = false) : base(roomView)
        {
            _viewExtraInfo = useExtraInfo;
            _roomOverlay = roomView;
            _overlaySettings = settings;
            _timer = new Timer();
        }

        public override void Start()
        {
            _startTime = TimeUtility.CorrectedTime;
            _timer.Start();
            _timer.Interval = 100;
            _timer.Elapsed += CheckForNewState;
        }
        public override void Stop()
        {
            _timer.Start();
        }
        private void CheckForNewState(object sender, EventArgs e)
        {
            var elapsedTime = (TimeUtility.CorrectedTime - _startTime).TotalSeconds;
            var triggerdUpdate = _overlaySettings.UpateObjects.FirstOrDefault(u => u.DisplayTimeSecondsElapsed <= elapsedTime && u.TriggerTimeSecondeElapsed > elapsedTime);
            if (triggerdUpdate != null && _currentUpdate != triggerdUpdate)
            {
                _currentUpdate = triggerdUpdate;
                var imageToUse = _viewExtraInfo && _currentUpdate.ImageOverlayPathExtra != "" ? _currentUpdate.ImageOverlayPathExtra : _currentUpdate.ImageOverlayPath;
                OnNewImagePath(Path.Combine("../../../resources/RoomOverlays/IP-CPT", imageToUse));
            }
            var roomTop = _overlaySettings.Top;
            var roomLeft = _overlaySettings.Left;
            var roomWidth = _overlaySettings.Width;
            var roomHeight = _overlaySettings.Height;

            var location = CombatLogStateBuilder.CurrentState.CurrentLocalCharacterPosition;
            var xFraction = (location.X - roomLeft) / roomWidth;
            var yFraction = (location.Y - roomTop) / roomHeight;
            _roomOverlay.DrawCharacter(xFraction, yFraction, location.Facing);
        }
    }
    public class NAHUT_Hazard : RoomHazard
    {
        private RoomOverlaySettings _settings;
        private RoomOverlay _roomOverlay;
        private string _pinId = "4122782057103360";
        private string _nailId = "4124985375326208";
        private string _reseedId = "4182177159839744";
        private string _pinDetonationId = "4124100612063621";
        private string _nailDetonationId = "4125006850163036";
        private string _prepareTheFieldEffectId = "4181979591344128";
        private List<string> _currentHazards = new List<string>();
        public NAHUT_Hazard(RoomOverlay roomView, RoomOverlaySettings settings): base(roomView)
        {
            _settings = settings;
            _roomOverlay = roomView;
        }

        private void UpdatePositions(ParsedLogEntry entry)
        {
            if(entry.Source.IsLocalPlayer || entry.Target.IsLocalPlayer)
            {
                UpdateCharacterPosition();
            }
            if(entry.Source.LogId.ToString() == _pinId && 
                !_currentHazards.Any(p=>p == entry.Source.Id.ToString()))
            {
                DrawNewHazard(entry.SourceInfo.Position, "PIN", entry.Source.Id.ToString());
            }
            if (entry.Source.LogId.ToString() == _nailId &&
                !_currentHazards.Any(p => p == entry.Source.Id.ToString()))
            {
                DrawNewHazard(entry.SourceInfo.Position, "NAIL", entry.Source.Id.ToString());
            }
            if(entry.Effect.EffectId == _reseedId || (entry.Effect.EffectId == _prepareTheFieldEffectId && entry.Effect.EffectType == EffectType.Apply))
            {
                ClearAllHazards();
            }
            if(entry.Effect.EffectId == _pinDetonationId || entry.Effect.EffectId == _nailDetonationId)
            {
                RemoveHazard(entry.Source.Id.ToString());
            }
        }

        private void DrawNewHazard(PositionData position, string v, string hazardId)
        {
            var roomTop = _settings.Top;
            var roomLeft = _settings.Left;
            var roomWidth = _settings.Width;
            var roomHeight = _settings.Height;
            var location = position;
            var xFraction = (location.X - roomLeft) / roomWidth;
            var yFraction = (location.Y - roomTop) / roomHeight;
            var widthFraction = (v == "PIN" ? 8 : 12)/roomWidth;
            _currentHazards.Add(hazardId);
            _roomOverlay.DrawHazard(xFraction, yFraction, widthFraction, hazardId);
        }
        private void ClearAllHazards()
        {
            _currentHazards.Clear();
            _roomOverlay.ClearAllHazards();
        }
        private void RemoveHazard(string hazardId)
        {
            _currentHazards.RemoveAll(p=> p == hazardId);
            _roomOverlay.ClearSpecificHazard(hazardId);
        }
        private void UpdateCharacterPosition()
        {
            var roomTop = _settings.Top;
            var roomLeft = _settings.Left;
            var roomWidth = _settings.Width;
            var roomHeight = _settings.Height;
            var location = CombatLogStateBuilder.CurrentState.CurrentLocalCharacterPosition;
            var xFraction = (location.X - roomLeft) / roomWidth;
            var yFraction = (location.Y - roomTop) / roomHeight;
            _roomOverlay.DrawCharacter(xFraction, yFraction, location.Facing);
        }
        public override void Start()
        {
            ClearAllHazards();
            CombatLogStreamer.NewLineStreamed += UpdatePositions;
        }

        public override void Stop()
        {
            CombatLogStreamer.NewLineStreamed -= UpdatePositions;

        }
    }
    public abstract class RoomHazard
    {
        private  RoomOverlay _currentRoomView;
        protected RoomHazard(RoomOverlay roomView)
        {
            _currentRoomView = roomView;
        }
        public abstract void Start();
        public abstract void Stop();
    }
}
