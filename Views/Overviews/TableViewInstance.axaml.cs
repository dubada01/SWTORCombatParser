
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using SWTORCombatParser.ViewModels.Overviews;

namespace SWTORCombatParser.Views.Overviews
{
    /// <summary>
    /// Interaction logic for TableViewInstance.xaml
    /// </summary>
    public partial class TableViewInstance : UserControl
    {
        public TableViewInstance()
        {
            InitializeComponent();
            DetailsGrid.LayoutUpdated += (_, _) => AlignTotals();
        }
        private ScrollBar? _horizontal;
        private ScrollBar? _vertical;
        private void AlignTotals()
        {
            if (_horizontal == null)
            {
                var bars = DetailsGrid.GetVisualDescendants().OfType<ScrollBar>().ToArray();
                _horizontal = bars.FirstOrDefault(b => b.Orientation == Orientation.Horizontal);
                _vertical = bars.FirstOrDefault(b => b.Orientation == Orientation.Vertical);
                if (_horizontal != null) _horizontal.PropertyChanged += (_, e) =>
                {
                    if (e.Property == ScrollBar.ValueProperty) TotalsScroll.Offset = new Vector(_horizontal.Value, 0);
                };
            }
            var columns = DetailsGrid.Columns.OrderBy(c => c.DisplayIndex).ToArray();
            for (var i = 0; i < columns.Length; i++)
            {
                var width = columns[i].ActualWidth;
                if (double.IsFinite(width) && Math.Abs(TotalsCells.ColumnDefinitions[i].Width.Value - width) > .1)
                    TotalsCells.ColumnDefinitions[i].Width = new GridLength(width);
                Grid.SetColumn(TotalsCells.Children[DetailsGrid.Columns.IndexOf(columns[i])], i);
            }
            var right = _vertical?.IsVisible == true ? _vertical.Bounds.Width : 0;
            if (Math.Abs(TotalsScroll.Margin.Right - right) > .1) TotalsScroll.Margin = new Thickness(0, 0, right, 0);
            if (_horizontal != null) TotalsScroll.Offset = new Vector(_horizontal.Value, 0);
        }
        private void RowLoading(object? sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is CombatInfoInstance row) _ = row.EnsureIconAsync();
        }
    }
}
