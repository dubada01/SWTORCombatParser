using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Views.Overlay.AbilityList;
using System;


namespace SWTORCombatParser.ViewModels.Overlays.AbilityList
{
    public class AbilityListSetupViewModel
    {
        private AbilityListViewModel _viewModel;
        private bool abilityListEnabled;
        public event Action<bool> OnEnabledChanged = delegate { };
        public AbilityListSetupViewModel()
        {
            _viewModel = new AbilityListViewModel("AbilityList");
            _viewModel.MainContent = new AbilityListView(_viewModel);
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType(_viewModel._overlayName);
            abilityListEnabled = defaults.Acive;
            if (defaults.Acive)
                _viewModel.ShowOverlayWindow();
        }
        public bool AbilityListEnabled
        {
            get => abilityListEnabled; set
            {
                abilityListEnabled = value;
                _viewModel.Active = value;
            }
        }
        private void Disable(AbilityListViewModel model)
        {
            AbilityListEnabled = false;
            OnEnabledChanged(false);
        }
        internal void SetScalar(double sizeScalar)
        {
            _viewModel.SizeScalar = sizeScalar;
        }
        internal void UpdateLock(bool overlaysLocked)
        {
            if (overlaysLocked)
            {
                _viewModel.LockOverlays();
            }
            else
            {
                _viewModel.UnlockOverlays();
            }
        }
    }
}
