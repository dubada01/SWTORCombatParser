using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.DataGrid;

namespace SWTORCombatParser.Views.DataGrid_Views
{
    
    public static class VisualTreeHelpers
    {
        /// <summary>
        /// Recursively searches for a child control with the specified name.
        /// </summary>
        public static T FindChildByName<T>(Control parent, string name) where T : class
        {
            if (parent == null)
                return null;

            if (parent.Name == name && parent is T typedParent)
                return typedParent;

            foreach (Control child in parent.GetVisualChildren())
            {
                var result = FindChildByName<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }
        /// <summary>
        /// Navigates up the visual tree a specified number of levels.
        /// </summary>
        public static T GetParent<T>(Control control, int levels = 1) where T : class
        {
            var parent = control.GetVisualParent();
            for (int i = 0; i < levels - 1 && parent != null; i++)
            {
                parent = parent.GetVisualParent();
            }
            return parent as T;
        }
    }
    public static class SortIconGeometries
    {
        public static readonly Geometry DescendingGeometry = Geometry.Parse("M1875 1011l-787 787v-1798h-128v1798l-787 -787l-90 90l941 941l941 -941z");
        public static readonly Geometry AscendingGeometry = Geometry.Parse("M1965 947l-941 -941l-941 941l90 90l787 -787v1798h128v-1798l787 787z");
    }

    /// <summary>
    /// Interaction logic for DataGridView.xaml
    /// </summary>
    public partial class DataGridView : UserControl
    {
        public ReactiveCommand<StatsSlotViewModel, Unit> RemoveColumnCommand { get; }
        // Determine the new sort direction
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;
        private readonly DataGridViewModel _viewModel;
        private string _sortProperty = "Name";
        private object? _selectionBeforeSort;
        private double? _scrollBeforeSort;

        public DataGridView(DataGridViewModel vm)
        {
            DataContext = vm;
            _viewModel = vm;
            InitializeComponent();
            _viewModel.ColumnsRefreshed += RefreshColumns;
            _viewModel.RowsReordering += RememberSelection;
            _viewModel.RowsReordered += RestoreSelection;
            RemoveColumnCommand = ReactiveCommand.Create<StatsSlotViewModel>(RemoveColumn);
            Loaded += SetDefaultSorting;
        }

        private ScrollBar? VerticalScrollBar => DynamicDataGrid.GetVisualDescendants().OfType<ScrollBar>()
            .FirstOrDefault(bar => bar.Orientation == Orientation.Vertical);

        private void RememberSelection()
        {
            _selectionBeforeSort = DynamicDataGrid.SelectedItem;
            _scrollBeforeSort = VerticalScrollBar?.Value;
        }

        private void RestoreSelection()
        {
            if (_selectionBeforeSort is MemberInfoViewModel selected && _viewModel.PartyMembers.Contains(selected) &&
                !ReferenceEquals(DynamicDataGrid.SelectedItem, selected))
                DynamicDataGrid.SelectedItem = selected;
            if (_scrollBeforeSort is { } offset && VerticalScrollBar is { } bar)
                bar.Value = Math.Clamp(offset, bar.Minimum, bar.Maximum);
            _selectionBeforeSort = null;
            _scrollBeforeSort = null;
        }

        private void SetDefaultSorting(object? sender, RoutedEventArgs e)
        {
            var parts = Settings.ReadSettingOfType<string>("grid_sort")?.Split("_+_");
            if (parts is { Length: 2 } && int.TryParse(parts[1], out var direction) &&
                Enum.IsDefined(typeof(ListSortDirection), direction))
            {
                _sortProperty = parts[0];
                _sortDirection = (ListSortDirection)direction;
            }
            RefreshColumns();
            ForceSort();
            UpdateSortIndicator();
        }

        private void RemoveColumn(StatsSlotViewModel columnVm) => _viewModel.RemoveHeader(columnVm.OverlayType);
        private static string HeaderText(DataGridColumn column) => column.Header is TextBlock text ? text.Text : column.Header?.ToString();

        private void RefreshColumns()
        {
            var slots = _viewModel.PartyMembers.FirstOrDefault()?.StatsSlots
                .Where(slot => slot.Header != "None").ToList() ?? new List<StatsSlotViewModel>();
            // Cell bindings handle value updates. Rebuild templates only when the
            // selected metrics actually change, preserving widths and scroll state.
            if (DynamicDataGrid.Columns.Select(HeaderText).SequenceEqual(slots.Select(slot => slot.Header))) return;
            var widths = DynamicDataGrid.Columns.ToDictionary(HeaderText, column => column.Width);
            DynamicDataGrid.Columns.Clear();
            foreach (var slot in slots)
            {
                DataGridTemplateColumn column;
                if (slot.Header == "Name")
                {
                    column = new DataGridTemplateColumn
                    {
                        Header = new TextBlock { Text = "Name" }, CanUserSort = true, MinWidth = 150,
                        CellTemplate = new FuncDataTemplate<MemberInfoViewModel>((member, _) =>
                        {
                            if (member == null) return null;
                            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Margin = new Thickness(5,0) };
                            if (!member.IsTotalsRow)
                            {
                                var icon = new Image { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
                                icon.Bind(Image.SourceProperty, new Binding(nameof(MemberInfoViewModel.ClassIcon)) { Source = member });
                                panel.Children.Add(icon);
                            }
                            var name = new TextBlock
                            {
                                FontWeight = member.IsTotalsRow ? FontWeight.Bold : FontWeight.Normal, FontSize = 12,
                                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            };
                            name.Bind(TextBlock.TextProperty, new Binding(nameof(MemberInfoViewModel.PlayerName)) { Source = member });
                            name.Bind(TextBlock.ForegroundProperty, new Binding(nameof(MemberInfoViewModel.NameForeground)) { Source = member });
                            panel.Children.Add(name);
                            panel.Bind(ToolTip.TipProperty, new Binding(nameof(MemberInfoViewModel.PlayerSummary)) { Source = member });
                            return panel;
                        })
                    };
                }
                else
                {
                    column = new DataGridTemplateColumn
                    {
                        MinWidth = 78,
                        Header = new TextBlock
                        {
                            Text = slot.Header, TextTrimming = TextTrimming.CharacterEllipsis, Tag = slot,
                            ContextMenu = new ContextMenu { Items = { new MenuItem { Header = "Remove Column", Command = RemoveColumnCommand, CommandParameter = slot } } }
                        },
                        CellStyleClasses = { "rightAlign", "static" }, CanUserSort = true,
                        CellTemplate = new FuncDataTemplate<MemberInfoViewModel>((member, _) =>
                        {
                            var value = member?.StatsSlots.FirstOrDefault(s => s.OverlayType == slot.OverlayType);
                            if (value == null) return new TextBlock();
                            var text = new TextBlock
                            {
                                TextTrimming = TextTrimming.CharacterEllipsis, Foreground = value.ForegroundColor,
                                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                                FontWeight = member.IsTotalsRow ? FontWeight.Bold : FontWeight.Normal,
                                FontSize = 12, Margin = new Thickness(0, 0, 7, 0)
                            };
                            text.Bind(TextBlock.TextProperty, new Binding(nameof(StatsSlotViewModel.Value)) { Source = value });
                            return text;
                        })
                    };
                }
                DynamicDataGrid.Columns.Add(column);
            }
            // Assign star widths after the complete set has been added; assigning
            // them one at a time lets the first column consume the viewport.
            foreach (var column in DynamicDataGrid.Columns)
                column.Width = widths.TryGetValue(HeaderText(column), out var width)
                    ? new DataGridLength(width.Value, width.UnitType)
                    : new DataGridLength(HeaderText(column) == "Name" ? 1.65 : 1, DataGridLengthUnitType.Star);
            if (!slots.Any(slot => slot.Header == _sortProperty))
                _sortProperty = slots.FirstOrDefault(slot => slot.Header != "Name")?.Header ?? "Name";
            ForceSort();
            UpdateSortIndicator();
        }

        private void DynamicDataGrid_OnSorting(object? sender, DataGridColumnEventArgs e)
        {
            e.Handled = true;
            if (e.Column is not DataGridTemplateColumn column || column.Header is not TextBlock header) return;
            _sortDirection = header.Text == _sortProperty && _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending : ListSortDirection.Ascending;
            _sortProperty = header.Text;
            ForceSort();
            SetSortIcon(column);
            Settings.WriteSetting("grid_sort", _sortProperty + "_+_" + (int)_sortDirection);
        }

        private void UpdateSortIndicator()
        {
            var column = DynamicDataGrid.Columns.OfType<DataGridTemplateColumn>().FirstOrDefault(c => HeaderText(c) == _sortProperty);
            if (column != null) Dispatcher.UIThread.Post(() => SetSortIcon(column), DispatcherPriority.Loaded);
        }
        private void SetSortIcon(DataGridTemplateColumn textColumn)
        {
            if (textColumn.Header is TextBlock headerControl)
            {
                var parentGrid = VisualTreeHelpers.GetParent<Grid>(headerControl, 2);
                // Find the SortIcon Path within the parent Grid
                var sortIcon = VisualTreeHelpers.FindChildByName<Path>(parentGrid, "SortIcon");

                // Clear sort indicators on other columns
                foreach (var col in DynamicDataGrid.Columns)
                {
                    if (col != textColumn && col.Header is TextBlock otherHeaderControl)
                    {
                        var otherParentGrid = VisualTreeHelpers.GetParent<Grid>(otherHeaderControl, 2);
                        // Find the SortIcon Path within the parent Grid
                        var otherSortIcon =
                            VisualTreeHelpers.FindChildByName<Path>(otherParentGrid, "SortIcon");
                        if (otherSortIcon != null)
                        {
                            otherSortIcon.Data = null;
                            otherSortIcon.IsVisible = false;
                        }
                    }
                }
                // Update the SortIcon for the clicked column
                if (sortIcon != null)
                {
                    sortIcon.Data = _sortDirection == ListSortDirection.Ascending
                        ? SortIconGeometries.AscendingGeometry
                        : SortIconGeometries.DescendingGeometry;

                    sortIcon.IsVisible = true;
                }
            }
        }
        private void ForceSort() => _viewModel.Sort(_sortProperty, _sortDirection);
    }
}
