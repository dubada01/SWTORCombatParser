using SWTORCombatParser.ViewModels.Overlays.BossFrame;
using SWTORCombatParser.ViewModels.Overlays.PvP;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using SWTORCombatParser.ViewModels.Overlays.Room;
using SWTORCombatParser.Views.Overlay.BossFrame;
using SWTORCombatParser.Views.Overlay.PvP;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using SWTORCombatParser.Views.Overlay.Room;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Overlays
{
    class OthersOverlaySetupViewModel : INotifyPropertyChanged
    {
        private RaidHotsConfigViewModel _raidHotsConfigViewModel;
        private AllPvPOverlaysViewModel _PvpOverlaysConfigViewModel;
        private BossFrameConfigViewModel _bossFrameViewModel;
        private RoomOverlayViewModel _roomOverlayViewModel;
        public BossFrameSetup BossFrameView { get; set; }
        public RoomSetup RoomOverlaySetup { get; set; }
        public PvpOverlaySetup PvpOverlays { get; set; }
        public RaidHOTsSteup RaidHotsConfig { get; set; }
        public OthersOverlaySetupViewModel()
        {
            _bossFrameViewModel = new BossFrameConfigViewModel();
            BossFrameView = new BossFrameSetup(_bossFrameViewModel);

            _roomOverlayViewModel = new RoomOverlayViewModel();
            RoomOverlaySetup = new RoomSetup(_roomOverlayViewModel);



            RaidHotsConfig = new RaidHOTsSteup();
            _raidHotsConfigViewModel = new RaidHotsConfigViewModel();
            RaidHotsConfig.DataContext = _raidHotsConfigViewModel;

            PvpOverlays = new PvpOverlaySetup();
            _PvpOverlaysConfigViewModel = new AllPvPOverlaysViewModel();
            PvpOverlays.DataContext = _PvpOverlaysConfigViewModel;

            OnPropertyChanged("RaidHotsConfig");
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void UpdateLock(bool overlaysLocked)
        {
            _raidHotsConfigViewModel.ToggleLock(overlaysLocked);
            if (overlaysLocked)
            {
                _bossFrameViewModel.LockOverlays();
                _roomOverlayViewModel.LockOverlays();
                _PvpOverlaysConfigViewModel.LockOverlays();
            }
            else
            {
                _bossFrameViewModel.UnlockOverlays();
                _roomOverlayViewModel.UnlockOverlays();
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
