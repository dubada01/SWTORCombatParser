using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using SWTORCombatParser.ViewModels.Overlays.Room;
using SWTORCombatParser.Views.Overlay.AbilityList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Overlays.AbilityList
{
    public class AbilityListSetupViewModel
    {
        private AbilityListViewModel _viewModel;
        private AbilityListView _view;
        private bool abilityListEnabled;

        public AbilityListSetupViewModel()
        {
            _viewModel = new AbilityListViewModel();
            _view = new AbilityListView(_viewModel);
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType("AbilityList");
            _view.Top = defaults.Position.Y;
            _view.Left = defaults.Position.X;
            _view.Width = defaults.WidtHHeight.X;
            _view.Height = defaults.WidtHHeight.Y;
            abilityListEnabled = defaults.Acive;
            if (defaults.Acive)
                _view.Show();
        }
        public bool AbilityListEnabled
        {
            get => abilityListEnabled; set
            {
                abilityListEnabled = value;
                if(abilityListEnabled)
                {
                    _view.Show();
                    _viewModel.IsEnabled = true;
                }
                else
                {
                    _view.Hide();
                    _viewModel.IsEnabled = false;
                }
                DefaultGlobalOverlays.SetActive("AbilityList", abilityListEnabled);
            }
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
