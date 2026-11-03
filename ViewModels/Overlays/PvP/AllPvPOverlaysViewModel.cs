using System;
using SWTORCombatParser.Model.Overlays;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overlays.PvP
{
    public class AllPvPOverlaysViewModel : ReactiveObject
    {
        private OpponentOverlayViewModel _opponentOverlayViewModel;
        private MedalTrackingViewModel _medalTrackingViewModel;
        private bool opponentHPEnabled;
        private bool medalTrackingEnabled;
        public event Action MapClosed = delegate { };
        public event Action OpponentClosed = delegate { };

        public AllPvPOverlaysViewModel()
        {
            _opponentOverlayViewModel = new OpponentOverlayViewModel("PvP_HP");
            _opponentOverlayViewModel.CloseRequested += () => OpponentClosed();
            _opponentOverlayViewModel.OverlayStateChanged += UpdateOverlay;
            opponentHPEnabled = DefaultGlobalOverlays.GetOverlayInfoForType("PvP_HP").Acive;
            _opponentOverlayViewModel.OverlayEnabled = opponentHPEnabled;
            
            _medalTrackingViewModel = new MedalTrackingViewModel("PvP_MedalTracking");
            _medalTrackingViewModel.CloseRequested += () => MapClosed();
            _medalTrackingViewModel.OverlayStateChanged += UpdateOverlay;
            medalTrackingEnabled = DefaultGlobalOverlays.GetOverlayInfoForType("PvP_MedalTracking").Acive;
            _medalTrackingViewModel.OverlayEnabled = medalTrackingEnabled;
        }
        public bool MedalTrackingEnabled
        {
            get => medalTrackingEnabled;
            set
            {
                medalTrackingEnabled = value;
                DefaultGlobalOverlays.SetActive("PvP_MedalTracking", medalTrackingEnabled);
                _medalTrackingViewModel.OverlayEnabled = medalTrackingEnabled;
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
            if (overlayType == "MedalTracking")
            {
                medalTrackingEnabled = state;
                this.RaisePropertyChanged(nameof(MedalTrackingEnabled));
            }
            else
            {
                opponentHPEnabled = state;
                this.RaisePropertyChanged(nameof(OpponentHPEnabled));
            }
        }
        internal void LockOverlays()
        {
            _opponentOverlayViewModel.LockOverlays();
            _medalTrackingViewModel.LockOverlays();
        }

        internal void UnlockOverlays()
        {
            _opponentOverlayViewModel.UnlockOverlays();
            _medalTrackingViewModel.UnlockOverlays();
        }
    }
}
