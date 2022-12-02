using SWTORCombatParser.Model.Overlays;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Overlays.PvP
{
    public class AllPvPOverlaysViewModel
    {
        private OpponentOverlayViewModel _opponentOverlayViewModel;
        private MiniMapViewModel _miniMapViewModel;
        private bool opponentHPEnabled;
        private bool miniMapEnabled;
        private int miniMapRangeBuffer;

        public AllPvPOverlaysViewModel()
        {
            _opponentOverlayViewModel = new OpponentOverlayViewModel();
            opponentHPEnabled = DefaultGlobalOverlays.GetOverlayInfoForType("PvP_HP").Acive;

            _miniMapViewModel = new MiniMapViewModel();
            miniMapEnabled = DefaultGlobalOverlays.GetOverlayInfoForType("PvP_MiniMap").Acive;
            MiniMapRangeBuffer = 15;
            

        }
        public int MiniMapRangeBuffer
        {
            get => miniMapRangeBuffer; set
            {
                miniMapRangeBuffer = value;
                _miniMapViewModel.Buffer = miniMapRangeBuffer;
            }
        }
        public bool MiniMapEnabled
        {
            get => miniMapEnabled;
            set
            {
                miniMapEnabled = value;
                DefaultGlobalOverlays.SetActive("PvP_MiniMap", miniMapEnabled);
                _miniMapViewModel.OverlayEnabled = miniMapEnabled;
            }
        }
        public bool OpponentHPEnabled
        {
            get => opponentHPEnabled; set
            {
                opponentHPEnabled = value;
                DefaultGlobalOverlays.SetActive("PvP_HP", opponentHPEnabled);
                _opponentOverlayViewModel.OverlayEnabled = opponentHPEnabled;
            }
        }
        internal void LockOverlays()
        {
            _opponentOverlayViewModel.LockOverlays();
            _miniMapViewModel.LockOverlays();
        }

        internal void UnlockOverlays()
        {
            _opponentOverlayViewModel.UnlockOverlays();
            _miniMapViewModel.UnlockOverlays();
        }
    }
}
