using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SWTORCombatParser.ViewModels.Timers;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Views.Timers
{
    public partial class TimerModificationWindow : Window
    {
        private readonly ModifyTimerViewModel _vm;
        private bool _saved;

        public TimerModificationWindow() : this(new ModifyTimerViewModel("Shared")) { }

        public TimerModificationWindow(ModifyTimerViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            Opened += (_, _) => BehaviorOptions.IsExpanded =
                _vm.DontRefresh || _vm.ResetOnEffectLoss || _vm.CanBeRefreshed || !string.IsNullOrEmpty(_vm.SelectedCancelTimerId);
            TabContent.Content = new VisualsTabContent();
            _vm.OnNewTimer += CloseWindow;
            _vm.PropertyChanged += OnViewModelChanged;
            CancelButton.Click += (_, _) => Close();
            SaveButton.Click += (_, e) =>
            {
                var invalidInput = this.GetVisualDescendants().OfType<TextBox>()
                    .FirstOrDefault(input => input.IsEffectivelyVisible && DataValidationErrors.GetHasErrors(input));
                if (invalidInput == null) return;
                _vm.ReportInvalidInput();
                invalidInput.Focus();
                e.Handled = true;
            };
            VariableCheck.Click += (_, _) => EffectsTabStrip.SelectedIndex = _vm.IsModifyingVariables ? 1 : 0;
            KeyDown += (_, e) =>
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                Close();
            };
            Closed += (_, _) =>
            {
                _vm.OnNewTimer -= CloseWindow;
                _vm.PropertyChanged -= OnViewModelChanged;
                if (!_saved) _vm.Cancel();
            };
            UpdateEffectsTab();
        }

        private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_vm.IncludeTimerVisuals) || e.PropertyName == nameof(_vm.IsModifyingVariables))
                UpdateEffectsTab();
        }

        private void UpdateEffectsTab()
        {
            if (!_vm.IsModifyingVariables) EffectsTabStrip.SelectedIndex = 0;
            else if (!_vm.IncludeTimerVisuals) EffectsTabStrip.SelectedIndex = 1;
        }

        private void CloseWindow(Timer timer, bool editing, bool imported)
        {
            _saved = true;
            Close();
        }

        private void SourceEntered(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _vm.SaveSource();
            e.Handled = true;
        }

        private void TargetEntered(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _vm.SaveTarget();
            e.Handled = true;
        }

        private void EffectsTabStrip_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (TabContent == null) return;
            TabContent.Content = EffectsTabStrip.SelectedIndex switch
            {
                0 => new VisualsTabContent(),
                1 => new VariablesTabContent(),
                _ => TabContent.Content
            };
        }
    }
}
