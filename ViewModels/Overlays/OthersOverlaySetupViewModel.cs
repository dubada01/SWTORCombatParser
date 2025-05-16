using SWTORCombatParser.ViewModels.Overlays.BossFrame;
using SWTORCombatParser.ViewModels.Overlays.PvP;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using SWTORCombatParser.ViewModels.Overlays.Room;
using SWTORCombatParser.Views.Overlay.BossFrame;
using SWTORCombatParser.Views.Overlay.PvP;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using SWTORCombatParser.Views.Overlay.Room;
using ReactiveUI;
using SWTORCombatParser.ViewModels.Overlays.ThreatTable;

namespace SWTORCombatParser.ViewModels.Overlays
{
    class OthersOverlaySetupViewModel : ReactiveObject
    {
        public RaidHotsConfigViewModel _raidHotsConfigViewModel;
        public AllPvPOverlaysViewModel _PvpOverlaysConfigViewModel;
        public BossFrameConfigViewModel _bossFrameViewModel;
        public RoomOverlayViewModel _roomOverlayViewModel;
        public ThreatTableOverlayViewModel _threatTableOverlayViewModel;
        public BossFrameSetup BossFrameView { get; set; }
        public RoomSetup RoomOverlaySetup { get; set; }
        public PvpOverlaySetup PvpOverlays { get; set; }
        public RaidHOTsSteup RaidHotsConfig { get; set; }
        public OthersOverlaySetupViewModel()
        {
            _bossFrameViewModel = new BossFrameConfigViewModel("BossFrame");
            BossFrameView = new BossFrameSetup(_bossFrameViewModel);

            _roomOverlayViewModel = new RoomOverlayViewModel("RoomHazard");
            RoomOverlaySetup = new RoomSetup(_roomOverlayViewModel);
            
            _threatTableOverlayViewModel = new ThreatTableOverlayViewModel("ThreatTable");

            RaidHotsConfig = new RaidHOTsSteup();
            _raidHotsConfigViewModel = new RaidHotsConfigViewModel();
            RaidHotsConfig.DataContext = _raidHotsConfigViewModel;

            PvpOverlays = new PvpOverlaySetup();
            _PvpOverlaysConfigViewModel = new AllPvPOverlaysViewModel();
            PvpOverlays.DataContext = _PvpOverlaysConfigViewModel;

            this.RaisePropertyChanged(nameof(RaidHotsConfig));
        }
        internal void UpdateLock(bool overlaysLocked)
        {
            _raidHotsConfigViewModel.ToggleLock(overlaysLocked);
            if (overlaysLocked)
            {
                _bossFrameViewModel.LockOverlays();
                _roomOverlayViewModel.OverlaysMoveable = false;
                _PvpOverlaysConfigViewModel.LockOverlays();
                _threatTableOverlayViewModel.OverlaysMoveable = false;
            }
            else
            {
                _bossFrameViewModel.UnlockOverlays();
                _roomOverlayViewModel.OverlaysMoveable = true;
                _threatTableOverlayViewModel.OverlaysMoveable = true;
                _PvpOverlaysConfigViewModel.UnlockOverlays();
            }
        }

        internal void HideAll()
        {
            _raidHotsConfigViewModel.HideRaidHots();
        }

        internal void SetScalar(double sizeScalar)
        {
            _bossFrameViewModel.CurrentScale = sizeScalar;
        }
    }
}
