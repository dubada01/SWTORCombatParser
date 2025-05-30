using System;
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
        public event Action MapClosed = delegate { };
        public event Action OpponentClosed = delegate { };

        public AllPvPOverlaysViewModel()
        {
            _opponentOverlayViewModel = new OpponentOverlayViewModel("PvP_HP");
            _opponentOverlayViewModel.CloseRequested += () => OpponentClosed();
            _opponentOverlayViewModel.OverlayStateChanged += UpdateOverlay;
            opponentHPEnabled = DefaultGlobalOverlays.GetOverlayInfoForType("PvP_HP").Acive;
            _opponentOverlayViewModel.OverlayEnabled = opponentHPEnabled;
            
            _miniMapViewModel = new MiniMapViewModel("PvP_MiniMap");
            _miniMapViewModel.CloseRequested += () => MapClosed();
            _miniMapViewModel.OverlayStateChanged += UpdateOverlay;
            miniMapEnabled = DefaultGlobalOverlays.GetOverlayInfoForType("PvP_MiniMap").Acive;
            _miniMapViewModel.OverlayEnabled = miniMapEnabled;
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
