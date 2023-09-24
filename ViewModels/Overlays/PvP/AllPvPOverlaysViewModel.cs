using SWTORCombatParser.Model.Overlays;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Overlays.PvP
{
    public class AllPvPOverlaysViewModel : INotifyPropertyChanged
    {
        private OpponentOverlayViewModel _opponentOverlayViewModel;
        private MiniMapViewModel _miniMapViewModel;
        private bool opponentHPEnabled;
        private bool miniMapEnabled;
        private int miniMapRangeBuffer;

        public AllPvPOverlaysViewModel()
        {
            _opponentOverlayViewModel = new OpponentOverlayViewModel();
            _opponentOverlayViewModel.OverlayStateChanged += UpdateOverlay;
            opponentHPEnabled = DefaultGlobalOverlays.GetOverlayInfoForType("PvP_HP").Acive;

            _miniMapViewModel = new MiniMapViewModel();
            _miniMapViewModel.OverlayStateChanged += UpdateOverlay;
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

        private void UpdateOverlay(string overlayType, bool state)
        {
            if (overlayType == "MiniMap")
            {
                miniMapEnabled = state;
                OnPropertyChanged("MiniMapEnabled");

            }
            else
            {
                opponentHPEnabled = state;
                OnPropertyChanged("OpponentHPEnabled");
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
