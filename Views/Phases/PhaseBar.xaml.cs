using ScottPlot.Drawing.Colormaps;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Phases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.Phases
{
    /// <summary>
    /// Interaction logic for PhaseBar.xaml
    /// </summary>
    public partial class PhaseBar : UserControl
    {
        Dictionary<PhaseInstance,Button> _phaseButtons = new Dictionary<PhaseInstance, Button>();
        private List<PhaseInstance> _phases = new List<PhaseInstance>();
        public PhaseBar(PhaseBarViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PhaseInstancesUpdated += UpdatePhases;
            PhaseManager.SelectedPhasesUpdated += UpdateButtonStates;
            CombatSelectionMonitor.OnInProgressCombatSelected += UpdatePhaseBar;
            CombatSelectionMonitor.CombatSelected += UpdatePhaseBar;
        }

        private void UpdateButtonStates(List<PhaseInstance> list)
        {
            App.Current.Dispatcher.Invoke(() => {
                ResetButtonBackgrounds();
                foreach (var phaseInstance in list)
                {
                    var button = _phaseButtons[phaseInstance];
                    button.Background = (SolidColorBrush)FindResource("GreenColorBrush");
                }
                if(list.Count == 0)
                {
                    PartitionBorder.BorderBrush = (SolidColorBrush)FindResource("GreenColorBrush");
                }
                else
                {
                    PartitionBorder.BorderBrush = (SolidColorBrush)FindResource("Gray8Brush");
                }
            });

        }
        private void ResetButtonBackgrounds()
        {
            foreach(var button in _phaseButtons.Values)
            {
                button.Background = (SolidColorBrush)FindResource("Gray5Brush");
            }
        }   
        private void Reset()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                PartitionsHolder.ColumnDefinitions.Clear();
                PartitionsHolder.Children.Clear();  
            });
            _phaseButtons.Clear();
        }

        private void UpdatePhases(List<PhaseInstance> list)
        {
            _phases = list;
        }
        private void UpdatePhaseBar(Combat newCombat)
        {
            Reset();
            var currentCombat = newCombat;
            var combatDuration = currentCombat.DurationSeconds;
            var startTime = currentCombat.StartTime;

            var previousStop = -1d;
            var columnIndex = 0;
            try
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var phase in _phases)
                    {
                        if (previousStop != -1)
                        {
                            var relativeStop = (phase.PhaseStart - startTime).TotalSeconds / combatDuration;
                            var stopWidth = relativeStop - previousStop;
                            AddColumnDefinition(stopWidth);
                            columnIndex++;
                        }
                        else
                        {
                            var relativeStop = (phase.PhaseStart - startTime).TotalSeconds / combatDuration;
                            AddColumnDefinition(relativeStop);
                            columnIndex++;
                        }
                        previousStop = (phase.PhaseEnd - startTime).TotalSeconds / combatDuration;


                        var relativeStart = (phase.PhaseStart - startTime).TotalSeconds / combatDuration;
                        var relativeEnd = (phase.PhaseEnd - startTime).TotalSeconds / combatDuration;
                        if (phase.PhaseEnd == DateTime.MinValue)
                        {
                            relativeEnd = (currentCombat.EndTime - startTime).TotalSeconds / combatDuration;
                        }
                        var width = relativeEnd - relativeStart;
                        AddColumnDefinition(width);
                        var button = new Button()
                        {
                            Foreground = Brushes.WhiteSmoke,
                            Background = (SolidColorBrush)FindResource("Gray5Brush"),
                            Content = new TextBlock
                            {
                                Text = phase.SourcePhase.Name,
                                TextTrimming = TextTrimming.CharacterEllipsis, // This sets the text trimming
                            },
                            CommandParameter = phase,
                            Style = (Style)FindResource("RoundCornerButton"),
                            ToolTip = $"{phase.SourcePhase.Name}: {(phase.PhaseStart-startTime).TotalSeconds} - {(phase.PhaseEnd - startTime).TotalSeconds}"
                        };
                        button.Command = (DataContext as PhaseBarViewModel).PhaseSelectionToggled;
                        _phaseButtons[phase] = button;
                        Grid.SetColumn(button, columnIndex);
                        PartitionsHolder.Children.Add(button);
                        columnIndex++;
                    }
                    if (!_phases.Any(l => l.PhaseEnd == DateTime.MinValue) && _phases.Count != 0)
                    {
                        var maxPhase = _phases.MaxBy(p => p.PhaseEnd);
                        var remainingTime = (currentCombat.EndTime - maxPhase.PhaseEnd).TotalSeconds / combatDuration;
                        AddColumnDefinition(remainingTime);
                    }
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        private void AddColumnDefinition(double width)
        {
            if (width < 0 || double.IsNaN(width) || double.IsInfinity(width))
                width = 0;
            PartitionsHolder.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(width, GridUnitType.Star) });
        }
    }
}
