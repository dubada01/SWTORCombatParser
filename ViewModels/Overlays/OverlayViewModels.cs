using GalaSoft.MvvmLight.Command;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Overlay;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayViewModel : INotifyPropertyChanged
    {
        private bool overlaysMoveable = true;
        private List<OverlayInstanceViewModel> _currentOverlays = new List<OverlayInstanceViewModel>();
        private Dictionary<OverlayType, DefaultOverlayInfo> _overlayDefaults = new Dictionary<OverlayType, DefaultOverlayInfo>();
        public ObservableCollection<OverlayType> AvailableOverlayTypes { get; set; } = new ObservableCollection<OverlayType>();
        public OverlayViewModel()
        {
            _overlayDefaults = DefaultOverlayManager.GetDefaults();
            var enumVals = EnumUtil.GetValues<OverlayType>();
            foreach (var enumVal in enumVals.Where(e=>e != OverlayType.None))
            {
                AvailableOverlayTypes.Add(enumVal);
                if (!_overlayDefaults.ContainsKey(enumVal))
                    continue;
                if (_overlayDefaults[enumVal].Acive)
                    CreateOverlay(enumVal);
            }

        }


        public ICommand GenerateOverlay => new RelayCommand<OverlayType>(CreateOverlay);

        private void CreateOverlay(OverlayType type)
        {
            if (_currentOverlays.Any(o => o.Type == type))
                return;
            DefaultOverlayManager.SetActiveState(type, true);
            var viewModel = new OverlayInstanceViewModel(type);
            viewModel.OverlayClosed += RemoveOverlay;
            viewModel.OverlaysMoveable = overlaysMoveable;
            _currentOverlays.Add(viewModel);
            var dpsOverlay = new InfoOverlay(viewModel);
            dpsOverlay.Top = _overlayDefaults[type].Position.Y;
            dpsOverlay.Left = _overlayDefaults[type].Position.X;
            dpsOverlay.Width =  _overlayDefaults[type].WidtHHeight.X;
            dpsOverlay.Height = _overlayDefaults[type].WidtHHeight.Y;
            dpsOverlay.Show();
        }

        private void RemoveOverlay(OverlayInstanceViewModel obj)
        {
            _currentOverlays.Remove(obj);
        }

        public ICommand ToggleOverlayLockCommand => new CommandHandler(ToggleOverlayLock);

        private void ToggleOverlayLock()
        {
            overlaysMoveable = !overlaysMoveable;
            if (overlaysMoveable)
                _currentOverlays.ForEach(o => o.UnlockOverlays());
            else
                _currentOverlays.ForEach(o => o.LockOverlays());
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
