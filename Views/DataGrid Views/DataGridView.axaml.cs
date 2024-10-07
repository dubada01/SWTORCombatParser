using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
        private readonly DataGridViewModel _viewModel;

        public DataGridView(DataGridViewModel vm)
        {
            DataContext = vm;
            _viewModel = vm;
            InitializeComponent();
            _viewModel.ColumnsRefreshed += RefreshColumns;
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
                    var customComparer = new CustomComparer(statSlot.Header, ListSortDirection.Descending);
                    if (statSlot.Header == "Name")
                    {
                        // Create "Name" column with custom cell style to show an icon along with text
                        var nameColumn = new DataGridTemplateColumn
                        {
                            Header = "Name",
                            CellTemplate = new FuncDataTemplate<MemberInfoViewModel>((member,ns) =>
                            {
                                if(member == null)
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
                                if(!member.IsTotalsRow)
                                    stackPanel.Children.Add(icon);

                                var textBlock = new TextBlock
                                {
                                    Text = member.IsTotalsRow ? "Total" : member.PlayerName,
                                    FontSize = 12,
                                    Foreground = member.IsLocalPlayer ? Brushes.Goldenrod : Brushes.WhiteSmoke,
                                    VerticalAlignment = VerticalAlignment.Center
                                };
                                stackPanel.Children.Add(textBlock);
                                // Create the ToolTip
                                ToolTip.SetTip(stackPanel, new TextBlock { Text = $"Player: {member.PlayerName}\nClass: {member.ClassName}" });
                                return stackPanel;
                            }),
                            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                        };
                        DynamicDataGrid.Columns.Add(nameColumn);
                    }
                    else
                    {
                        var column = new DataGridTextColumn
                        {
                            Header = new TextBlock
                            {
                                Text = statSlot.Header,
                                TextWrapping = TextWrapping.Wrap, // Enables trimming with ellipsis
                                TextTrimming = TextTrimming.None,
                                Tag = statSlot,
                                // Optionally, you can set other TextBlock properties here
                                // such as FontWeight, FontSize, etc.
                            },
                            Binding = new Binding($"StatsSlots[{i}].Value"), // Binding to the appropriate StatSlot value,
                            CustomSortComparer = customComparer,
                            Foreground = statSlot.ForegroundColor,
                            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                            FontSize = 12
                        };
                        DynamicDataGrid.Columns.Add(column);
                    }
                }
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var column in DynamicDataGrid.Columns)
                {
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                }
            });

        }

        private void DynamicDataGrid_OnSorting(object? sender, DataGridColumnEventArgs e)
        {
            e.Handled = true; // Prevent the default sort behavior
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (e.Column is DataGridTextColumn textColumn)
                {
                    // Retrieve the header Control
                    if (textColumn.Header is Control headerControl)
                    {
                        if((textColumn.Header as TextBlock).Text == "Name")
                        {
                            return;
                        }
                        var parentGrid = VisualTreeHelpers.GetParent<Grid>(headerControl, 2);
                        // Find the SortIcon Path within the parent Grid
                        var sortIcon = VisualTreeHelpers.FindChildByName<Path>(parentGrid, "SortIcon");

                        // Determine the new sort direction
                        ListSortDirection newDirection = ListSortDirection.Ascending;

                        if (textColumn.Tag is ListSortDirection existingDirection)
                        {
                            newDirection = existingDirection == ListSortDirection.Ascending
                                ? ListSortDirection.Descending
                                : ListSortDirection.Ascending;
                        }

                        // Update the Tag to store the current sort direction
                        textColumn.Tag = newDirection;

                        // Clear sort indicators on other columns
                        foreach (var col in DynamicDataGrid.Columns)
                        {
                            if (col != textColumn && col.Header is Control otherHeaderControl)
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
                            sortIcon.Data = newDirection == ListSortDirection.Ascending
                                ? SortIconGeometries.AscendingGeometry
                                : SortIconGeometries.DescendingGeometry;

                            sortIcon.IsVisible = true;
                        }

                        // Retrieve the sort property based on the binding
// Retrieve the sort property based on the binding
                        string sortProperty = ((textColumn.Header as TextBlock)?.Tag as StatsSlotViewModel).Header;

                        // Instantiate the CustomComparer with the new direction
                        CustomComparer comparer = new CustomComparer(sortProperty, newDirection);

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
            });
        }

    }
}
