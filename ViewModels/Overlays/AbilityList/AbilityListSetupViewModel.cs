using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using SWTORCombatParser.ViewModels.Overlays.Room;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Overlay.AbilityList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels.Overlays.AbilityList
{
    public class AbilityListSetupViewModel
    {
        private AbilityListViewModel _viewModel;
        private BaseOverlayWindow _view;
        private bool abilityListEnabled;
        public event Action<bool> OnEnabledChanged = delegate { };
        public AbilityListSetupViewModel()
        {
            _viewModel = new AbilityListViewModel();
            _viewModel.OverlayName = "AbilityList";
            _viewModel.MainContent = new AbilityListView(_viewModel);
            _view = new BaseOverlayWindow(_viewModel);
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType(_viewModel.OverlayName);
            _view.SetSizeAndLocation(new Point(defaults.Position.X, defaults.Position.Y), new Point(defaults.WidtHHeight.X, defaults.WidtHHeight.Y));
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
                DefaultGlobalOverlays.SetActive(_viewModel.OverlayName , abilityListEnabled);
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
