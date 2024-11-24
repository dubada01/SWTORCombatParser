using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SWTORCombatParser.ViewModels.BattleReview;


namespace SWTORCombatParser.Views.Battle_Review
{
    /// <summary>
    /// Interaction logic for EventHistoryView.xaml
    /// </summary>
    public partial class EventHistoryView : UserControl
    {
        EventHistoryViewModel _eventViewModel;
        public EventHistoryView(EventHistoryViewModel eventViewModel)
        {
            DataContext = eventViewModel;
            _eventViewModel = eventViewModel;
            InitializeComponent();
        }
        private double GetRowHeight()
        {
            // Get the first rendered row in the DataGrid
            var firstRow = DataArea.GetVisualDescendants()
                .OfType<DataGridRow>()
                .FirstOrDefault();

            // If a row is rendered, return its actual height
            if (firstRow != null)
            {
                return firstRow.Bounds.Height;
            }

            // Fallback to a default value if no rows are rendered
            return 0.0;
        }
        private int _previousIndex = -1; // Tracks the previously selected index
        private void Selection1List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataArea.SelectedItem == null || DataArea.SelectedIndex < 0 || !_eventViewModel.DeathReview)
                return;

            // Assume dynamic row height
            double rowHeight = GetRowHeight(); // Dynamically calculate row height
            if (rowHeight <= 0)
                return;

            // Calculate the number of rows visible in the viewport
            int rowsInViewport = (int)(DataArea.Bounds.Height / rowHeight);

            // Determine the scrolling direction
            int offset;
            if (_previousIndex >= 0 && DataArea.SelectedIndex > _previousIndex)
            {
                // Moving down: Ensure the selected item stays near the bottom of the viewport
                offset = -1*(rowsInViewport / 2); // Center the selected item in the viewport
            }
            else
            {
                // Moving up: Ensure the selected item stays near the top of the viewport
                offset = rowsInViewport / 2; // Center the selected item in the viewport
            }

            // Calculate the target index to scroll into view
            int targetIndex = Math.Clamp(DataArea.SelectedIndex - offset, 0, DataArea.ItemsSource.Cast<object>().Count() - 1);

            // Get the item at the target index
            var items = DataArea.ItemsSource.Cast<object>().ToList();
            if (targetIndex < items.Count)
            {
                // Scroll the calculated item into view
                DataArea.ScrollIntoView(items[targetIndex], DataArea.Columns[0]);
            }

            // Update the previous index
            _previousIndex = DataArea.SelectedIndex;
        }

        private void DataArea_MouseEnter(object sender, PointerEventArgs e)
        {
            _eventViewModel.HasFocus = true;
        }

        private void DataArea_MouseLeave(object sender, PointerEventArgs e)
        {
            _eventViewModel.HasFocus = false;
        }
    }
}
