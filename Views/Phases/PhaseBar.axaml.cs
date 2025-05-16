using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Phases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Views.Phases
{
    /// <summary>
    /// Interaction logic for PhaseBar.xaml
    /// </summary>
    public partial class PhaseBar : UserControl
    {
        Dictionary<PhaseInstance, Button> _phaseButtons = new Dictionary<PhaseInstance, Button>();
        private List<PhaseInstance> _phases = new List<PhaseInstance>();
        public PhaseBar(PhaseBarViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PhaseInstancesUpdated += UpdatePhases;
            PhaseManager.SelectedPhasesUpdated += UpdateButtonStates;
            CombatSelectionMonitor.OnInProgressCombatSelected += UpdatePhaseBar;

            Observable.FromEvent<Combat>(manager => CombatSelectionMonitor.CombatSelected += manager,
                manager => CombatSelectionMonitor.CombatSelected -= manager).Subscribe(UpdatePhaseBar);
        }

        private void UpdateButtonStates(List<PhaseInstance> list)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ResetButtonBackgrounds();
                foreach (var phaseInstance in list)
                {
                    var button = _phaseButtons[phaseInstance];
                    button.Background = (SolidColorBrush)Application.Current.FindResource("GreenColorBrush");
                }
                if (list.Count == 0)
                {
                    PartitionBorder.BorderBrush = (SolidColorBrush)Application.Current.FindResource("GreenColorBrush");
                }
                else
                {
                    PartitionBorder.BorderBrush = (SolidColorBrush)Application.Current.FindResource("Gray8Brush");
                }
            });

        }
        private void ResetButtonBackgrounds()
        {
            foreach (var button in _phaseButtons.Values)
            {
                button.Background = (SolidColorBrush)Application.Current.FindResource("Gray5Brush");
            }
        }
        private void Reset()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                PartitionsHolder.ColumnDefinitions.Clear();
                PartitionsHolder.Children.Clear();
            });
            _phaseButtons.Clear();
        }

        private void UpdatePhases(List<PhaseInstance> list)
        {
            _phases = list.OrderBy(p => p.PhaseStart).ToList();
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
                Dispatcher.UIThread.Invoke(() =>
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
                        var button = new Button
                        {
                            Foreground = Brushes.WhiteSmoke,
                            Background = (SolidColorBrush)Application.Current.FindResource("Gray5Brush"),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Center,
                            Content = new TextBlock
                            {
                                Text = phase.SourcePhase.Name,
                                TextTrimming = TextTrimming.CharacterEllipsis // This sets the text trimming
                            },
                            CommandParameter = phase
                        };

// Assign tooltip using ToolTip.SetTip
                        ToolTip.SetTip(button, $"{phase.SourcePhase.Name}: {(phase.PhaseStart - startTime).TotalSeconds} - {(phase.PhaseEnd - startTime).TotalSeconds}");

// Add style class
                        button.Classes.Add("RoundCornerButton");


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
                Logging.LogError(e.Message);
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
