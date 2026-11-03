using System;
using Avalonia.Threading;

namespace SWTORCombatParser.ViewModels.Overlays.Notes
{
    public class RaidNotesSetupViewModel
    {
        private bool inInstance = false;
        private RaidNotesViewModel _viewModel;
        private bool raidNotesEnabled;
        public event Action<bool> OnEnabledChanged = delegate { };
        public RaidNotesSetupViewModel()
        {
            _viewModel = new RaidNotesViewModel("RaidNotes");
            _viewModel.CloseRequested += Disable;
            _viewModel.OnInInstanceChanged += InInstanceChanged;
        }
        private void InInstanceChanged(bool instanceStatus)
        {
            inInstance = instanceStatus;
            SetVisibilityForInstanceState();

        }
        private void SetVisibilityForInstanceState()
        {
            Dispatcher.UIThread.Invoke(() => {
                if (inInstance && RaidNotesEnabled)
                {
                    _viewModel.ShowOverlayWindow();
                }
                if (!inInstance)
                {
                    _viewModel.HideOverlayWindow();
                }
            });

        }
        public bool RaidNotesEnabled
        {
            get => raidNotesEnabled; set
            {
                raidNotesEnabled = value; 
                if (raidNotesEnabled)
                {
                    _viewModel.IsEnabled = true;
                    _viewModel.Active = true;
                }
                else
                {
                    _viewModel.HideOverlayWindow();
                    _viewModel.IsEnabled = false;
                    _viewModel.Active = false;
                }
            }
        }
        private void Disable()
        {
            RaidNotesEnabled = false;
            OnEnabledChanged(false);
        }
        internal void UpdateLock(bool overlaysLocked)
        {
            if (overlaysLocked)
            {
                _viewModel.LockOverlays();
                SetVisibilityForInstanceState();
            }
            else
            {
                _viewModel.UnlockOverlays();
                if(RaidNotesEnabled)
                    _viewModel.ShowOverlayWindow();
            }
        }
    }
}
