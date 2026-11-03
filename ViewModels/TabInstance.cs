using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays;
using System;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels
{
    public class TabInstance :ReactiveObject
    {
        private Bitmap _overlayLockIcon = ImageHelper.LoadFromResource("avares://Orbs/resources/lockedIcon.png");
        private Bitmap _tabIcon;
        private SolidColorBrush _tabSelectedColor = new SolidColorBrush(Colors.DarkGray);
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
                ? ImageHelper.LoadFromResource("avares://Orbs/resources/lockedIcon.png")
                : ImageHelper.LoadFromResource("avares://Orbs/resources/unlockedIcon.png");
        }

        public SolidColorBrush TabSelectedColor
        {
            get => _tabSelectedColor;
            set => this.RaiseAndSetIfChanged(ref _tabSelectedColor, value);
        }

        public Guid HistoryID { get; set; }
        public bool IsHistoricalTab { get; set; }
        public bool IsOverlaysTab { get; set; }
        public bool IsNotOverlaysTab => !IsOverlaysTab;

        public Bitmap OverlayLockIcon
        {
            get => _overlayLockIcon;
            set => this.RaiseAndSetIfChanged(ref _overlayLockIcon, value);
        }

        public Bitmap TabIcon
        {
            get => _tabIcon;
            set => this.RaiseAndSetIfChanged(ref _tabIcon, value);
        }
        

        public string HeaderText { get; set; }
        public UserControl TabContent { get; set; }

        public void Select()
        {
            TabSelectedColor = new SolidColorBrush(Colors.SeaGreen);
        }

        public void Unselect()
        {
            TabSelectedColor = new SolidColorBrush(Colors.DarkGray);
        }
    }
}
