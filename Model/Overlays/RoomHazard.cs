using SWTORCombatParser.DataStructures.RoomOverlay;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Views.Overlay.Room;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SWTORCombatParser.Model.Overlays
{
    public class IPCPT_Hazard : RoomHazard
    {
        private RoomOverlaySettings _overlaySettings;
        private RoomOverlay _roomOverlay;
        private DispatcherTimer _dTimer;
        private DateTime _startTime;
        private RoomOverlayUpdate _currentUpdate;
        private bool _viewExtraInfo;

        public event Action<string> OnNewImagePath = delegate { };
        public IPCPT_Hazard(RoomOverlay roomView, RoomOverlaySettings settings, bool useExtraInfo = false) : base(roomView)
        {
            _viewExtraInfo = useExtraInfo;
            _roomOverlay = roomView;
            _overlaySettings = settings;
            _dTimer = new DispatcherTimer();
            Start();
        }

        public override void Start()
        {
            _startTime = DateTime.Now;
            _dTimer.Start();
            _dTimer.Interval = TimeSpan.FromSeconds(0.1);
            _dTimer.Tick += CheckForNewState;
        }
        public override void Stop()
        {
            _dTimer.Start();
        }
        private void CheckForNewState(object sender, EventArgs e)
        {
            var elapsedTime = (DateTime.Now - _startTime).TotalSeconds;
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
