using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reactive;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels
{
    public class TabInstance :ReactiveObject, INotifyPropertyChanged
    {
        public TabInstance()
        {

        }
        public event Action<TabInstance> RequestTabClose = delegate { };
        public ReactiveCommand<Unit,Unit> CloseTabCommand => ReactiveCommand.Create(CloseTab);

        private void CloseTab()
        {
            RequestTabClose(this);
        }

        public ReactiveCommand<Unit,Unit> ToggleLockedCommand => ReactiveCommand.Create(ToggleLocked);

        private void ToggleLocked()
        {
            var tabViewModel = TabContent.DataContext as OverlayViewModel;
            tabViewModel.OverlaysLocked = !tabViewModel.OverlaysLocked;
            UpdateLockIcon();
        }

        public void UpdateLockIcon()
        {
            if (HeaderText != "Overlays")
                return;
            var tabViewModel = TabContent.DataContext as OverlayViewModel;
            OverlayLockIcon = tabViewModel.OverlaysLocked
                ? Path.Combine(Environment.CurrentDirectory, "resources/lockedIcon.png")
                : Path.Combine(Environment.CurrentDirectory, "resources/unlockedIcon.png");
            OnPropertyChanged("OverlayLockIcon");
        }
        public Guid HistoryID { get; set; }
        public bool IsHistoricalTab { get; set; }
        public bool IsOverlaysTab { get; set; }
        public string OverlayLockIcon { get; set; } = "../resources/lockedIcon.png";
        public string TabIcon { get; set; }
        public string HeaderText { get; set; }
        public UserControl TabContent { get; set; }
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
