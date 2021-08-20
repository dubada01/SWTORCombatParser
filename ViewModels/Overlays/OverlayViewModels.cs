using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Overlay;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public enum OverlayType
    {
        DPS,
        HPS
    }
    public class OverlayViewModel : INotifyPropertyChanged
    {
        public ICommand GenerateDPSOverlay => new CommandHandler(CreateDPSOverlay);

        private void CreateDPSOverlay()
        {
            CreateOverlay(OverlayType.DPS);
        }
        public ICommand GenerateHPSOverlay => new CommandHandler(CreateHPSOverlay);

        private void CreateHPSOverlay()
        {
            CreateOverlay(OverlayType.HPS);
        }
        private void CreateOverlay(OverlayType type)
        {
            var viewModel = new OverlayInstanceViewModel(type);
            var dpsOverlay = new InfoOverlay(viewModel);
            dpsOverlay.Show();


        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
