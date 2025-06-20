using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
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
        private object _sortLock = new object();
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;
        private readonly DataGridViewModel _viewModel;
        private string _sortProperty = "Name";
        private DataGridTemplateColumn columnToAddSortIconTo;

        public DataGridView(DataGridViewModel vm)
        {
            DataContext = vm;
            _viewModel = vm;
            InitializeComponent();
            _viewModel.ColumnsRefreshed += RefreshColumns;
            RemoveColumnCommand = ReactiveCommand.Create<StatsSlotViewModel>(RemoveColumn);
            Loaded += SetDefaultSorting;
        }

        private void SetDefaultSorting(object? sender, RoutedEventArgs e)
        {
            var sortingInfo = Settings.ReadSettingOfType<string>("grid_sort");
            
            if (string.IsNullOrWhiteSpace(sortingInfo) || sortingInfo.Length < 2)
                throw new InvalidOperationException("Invalid sort settings format.");
            
            string sortProperty = sortingInfo.Split("_+_")[0];
            if (!int.TryParse(sortingInfo.Split("_+_")[1], out int direction))
            {
                Logging.LogError("Invalid sort settings format.");
                return;
            }
            _sortProperty = sortProperty;
            _sortDirection = (ListSortDirection)direction;
            RefreshColumns();
        }

        private void RemoveColumn(StatsSlotViewModel columnVm)
        {
            _viewModel.RemoveHeader(columnVm.OverlayType);
        }
        private void RefreshColumns()
        {
            // Clear any existing columns
            DynamicDataGrid.Columns.Clear();
            DynamicDataGrid.ItemsSource = _viewModel.PartyMembers;
            // Assuming MemberInfoList is a collection of MemberInfoViewModel
            if (_viewModel.PartyMembers != null && _viewModel.PartyMembers.Any())
            {
                // Retrieve the number of StatSlotViewModel items from the first row (assuming all rows have the same number)
                var firstRow = _viewModel.PartyMembers.First();
                int statCount = firstRow.StatsSlots.Count;

                // Dynamically create columns based on the StatSlotViewModel list
                for (int i = 0; i < statCount; i++)
                {
                    var statSlot = firstRow.StatsSlots[i];
                    if(statSlot.Header == "None")
                    {
                        continue;
                    }
                    var customComparer = new CustomComparer(statSlot.Header, _sortDirection);
                    if (statSlot.Header == "Name")
                    {
                        // Create "Name" column with custom cell style to show an icon along with text
                        var nameColumn = new DataGridTemplateColumn
                        {
                            Header = "Name",
                            CellTemplate = new FuncDataTemplate<MemberInfoViewModel>((member, ns) =>
                            {
                                if (member == null)
                                    return null;
                                var stackPanel = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal
                                };

                                var icon = new Image
                                {
                                    Width = 16,
                                    Height = 16,
                                    Source = member.ClassIcon,
                                    VerticalAlignment = VerticalAlignment.Center
                                };
                                if (!member.IsTotalsRow)
                                    stackPanel.Children.Add(icon);

                                var textBlock = new TextBlock
                                {
                                    Text = member.IsTotalsRow ? "Total" : member.PlayerName,
                                    FontWeight = member.IsTotalsRow ? FontWeight.Bold : FontWeight.Normal,
                                    FontSize = 12,
                                    Foreground = member.IsLocalPlayer ? Brushes.Goldenrod : Brushes.WhiteSmoke,
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    VerticalAlignment = VerticalAlignment.Center
                                };
                                stackPanel.Children.Add(textBlock);
                                // Create the ToolTip
                                ToolTip.SetTip(stackPanel,
                                    new TextBlock { Text = $"Player: {member.PlayerName}\nClass: {member.ClassName}" });
                                return stackPanel;
                            }),
                            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                        };
                        DynamicDataGrid.Columns.Add(nameColumn);
                    }
                    else
                    {
                        var column = new DataGridTemplateColumn
                        {
                            Header = new TextBlock
                            {
                                ContextMenu = new ContextMenu
                                {
                                    Items =
                                    {
                                        new MenuItem { Header = "Remove Column", Command = RemoveColumnCommand, CommandParameter = statSlot},
                                    }
                                },
                                Text = statSlot.Header,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                                Tag = statSlot,
                                // Optionally, you can set other TextBlock properties here
                                // such as FontWeight, FontSize, etc.
                            },
                            CellStyleClasses = { "rightAlign","static" },
                            CellTemplate = new FuncDataTemplate<MemberInfoViewModel>((member, ns) =>
                            {
                                var statToDisplay = member.StatsSlots.FirstOrDefault(s => s.Header == statSlot.Header);
                                if (statToDisplay == null)
                                    return new TextBlock();
                                var textBox = new TextBlock
                                {
                                    Text = statToDisplay.Value,
                                    TextTrimming = TextTrimming.CharacterEllipsis,
                                    Foreground = statToDisplay.ForegroundColor,
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    FontWeight = member.IsTotalsRow ? FontWeight.Bold : FontWeight.Normal,
                                    FontSize = member.IsTotalsRow ? 11 : 10,
                                    Margin = new Thickness(0,0,5,0)
                                };
                                return textBox;
                            }),
                            CustomSortComparer = customComparer,
                            CanUserSort = true,
                            SortMemberPath = "Value",
                            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                        };
                        DynamicDataGrid.Columns.Add(column);
                        if (statSlot.Header == _sortProperty)
                            columnToAddSortIconTo = column;
                    }
                }
            }


            foreach (var column in DynamicDataGrid.Columns)
            {
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            }

            ForceSort();
            if (columnToAddSortIconTo != null)
            {
                UpdateColumnForSort(columnToAddSortIconTo);
                Dispatcher.UIThread.InvokeAsync(() => SetSortIcon(columnToAddSortIconTo));
            }
        }

        private void DynamicDataGrid_OnSorting(object? sender, DataGridColumnEventArgs e)
        {
            e.Handled = true; // Prevent the default sort behavior
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (e.Column is DataGridTemplateColumn textColumn)
                {
                    if ((textColumn.Header as TextBlock).Text == "Name")
                    {
                        return;
                    }
                    UpdateColumnForSort(textColumn);
                    ForceSort();
                    SetSortIcon(textColumn);
                    Settings.WriteSetting("grid_sort",GetSortInfo());
                }
            });
        }

        private string GetSortInfo()
        {
            var direction = ((int)_sortDirection).ToString();
            var property = _sortProperty;
            return property +"_+_"+ direction;
        }

        private void UpdateColumnForSort(DataGridTemplateColumn textColumn)
        {
            if (textColumn.Header is TextBlock headerControl)
            {
                if (textColumn.Tag is ListSortDirection existingDirection)
                {
                    _sortDirection = existingDirection == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
                }

                // Update the Tag to store the current sort direction
                textColumn.Tag = _sortDirection;
                // Retrieve the sort property based on the binding
// Retrieve the sort property based on the binding
                _sortProperty = ((textColumn.Header as TextBlock)?.Tag as StatsSlotViewModel).Header;
            }
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
        private void ForceSort()
        {
            // Instantiate the CustomComparer with the new direction
            CustomComparer comparer = new CustomComparer(_sortProperty, _sortDirection);
            // Sort the items
            var items = DynamicDataGrid.ItemsSource as IEnumerable<MemberInfoViewModel>;
            if (items != null)
            {
                var sortedItems = new List<MemberInfoViewModel>(items);
                sortedItems.Sort(comparer);
                DynamicDataGrid.ItemsSource = new AvaloniaList<MemberInfoViewModel>(sortedItems);
            }
        }
    }
}
