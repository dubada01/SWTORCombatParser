using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public enum SortingDirection
    {
        None,
        Ascending,
        Descending,
    }
    public class DataGridHeaderViewModel : ReactiveObject, INotifyPropertyChanged
    {
        private string selectedNewHeader;
        private SortingDirection sortDirection;

        public event Action<string> RequestedNewHeader = delegate { };
        public event Action<DataGridHeaderViewModel> RequestRemoveHeader = delegate { };
        public event Action<SortingDirection, string> SortingDirectionChanged = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public List<string> AvailableHeaderNames { get; set; }
        public string SelectedNewHeader
        {
            get => selectedNewHeader; set
            {
                selectedNewHeader = value;
                RequestedNewHeader(selectedNewHeader);
            }
        }
        public bool CanSort => IsRealHeader && !IsName;
        public SortingDirection SortDirection
        {
            get => sortDirection; set
            {
                sortDirection = value;
                OnPropertyChanged("SortedDescending");
                OnPropertyChanged("SortedAscending");
                OnPropertyChanged("CanSort");
            }
        }
        public bool SortedDescending => SortDirection == SortingDirection.Descending;
        public bool SortedAscending => SortDirection == SortingDirection.Ascending;
        public bool IsRealHeader { get; set; } = true;
        public string Text { get; set; }
        public bool IsName { get; set; }
        public ReactiveCommand<Unit,Unit> ToggleSortingCommand => ReactiveCommand.Create(ToggleSorting);

        private void ToggleSorting()
        {
            if (SortDirection == SortingDirection.None)
            {
                SortDirection = SortingDirection.Descending;
                SortingDirectionChanged(SortDirection, Text);
                return;
            }
            if (SortDirection == SortingDirection.Descending)
            {
                SortDirection = SortingDirection.Ascending;
                SortingDirectionChanged(SortDirection, Text);
                return;
            }
            if (SortDirection == SortingDirection.Ascending)
            {
                SortDirection = SortingDirection.None;
                SortingDirectionChanged(SortDirection, Text);
                return;
            }
        }

        public ReactiveCommand<Unit,Unit> HeaderClickedCommand => ReactiveCommand.Create(HeaderClicked);

        private void HeaderClicked()
        {
            RequestRemoveHeader(this);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
