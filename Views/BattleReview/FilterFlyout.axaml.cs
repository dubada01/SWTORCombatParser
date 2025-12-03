using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.BattleReview;

namespace SWTORCombatParser.Views.Battle_Review
{
    public partial class FilterFlyout : UserControl
    {
        public static readonly StyledProperty<string> HeaderTextProperty =
            AvaloniaProperty.Register<FilterFlyout, string>(nameof(HeaderText));
        private ObservableCollection<FilterOption>? _hookedOptions;
        private CheckBox? _toggleAll;
        private bool? _lastToggleAllState = true;
        private bool _suppressToggleAllEvents = false;
        private bool _suppressOptionPropertyChanged = false;

        public static readonly StyledProperty<bool> IsFilteringProperty = AvaloniaProperty.Register<
            FilterFlyout,
            bool
        >(nameof(IsFiltering));
        public bool IsFiltering
        {
            get => GetValue(IsFilteringProperty);
            private set => SetValue(IsFilteringProperty, value);
        }

        public string HeaderText
        {
            get => GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        // bindable property so parent view / VM can push options
        public static readonly StyledProperty<ObservableCollection<FilterOption>> OptionsProperty =
            AvaloniaProperty.Register<FilterFlyout, ObservableCollection<FilterOption>>(
                nameof(Options)
            );

        // Options = Checkboxes
        public ObservableCollection<FilterOption> Options
        {
            get => GetValue(OptionsProperty) ?? new ObservableCollection<FilterOption>();
            set => SetValue(OptionsProperty, value);
        }

        // Event to notify parent when filter changes
        public event EventHandler<FilterChangedEventArgs>? FilterChanged;

        public FilterFlyout()
        {
            InitializeComponent();

            // Hook flyout opened so we populate when user opens it (VM/logs likely ready)
            var btn = this.FindControl<Button>("RootButton");
            if (btn?.Flyout is Flyout f)
            {
                f.Opened += (_, __) =>
                {
                    PopulateOptionsFromViewModel();
                    AttachToggleAllHandlers();
                };
            }

            // Observe Options property so we can attach handlers whenever Options is replaced
            this.GetObservable(OptionsProperty).Subscribe(HookOptions);
        }

        private void AttachToggleAllHandlers()
        {
            if (_toggleAll != null)
                return;

            // find the Button then its Flyout content and locate the ToggleAll CheckBox inside the flyout
            var rootButton = this.FindControl<Button>("RootButton");
            var flyout = rootButton?.Flyout as Flyout;
            var flyoutContent = flyout?.Content as Control;

            // try to find ToggleAll inside the flyout content
            _toggleAll = flyoutContent?.FindControl<CheckBox>("ToggleAll");
            if (_toggleAll != null)
            {
                // make sure we only attach once
                _toggleAll.Click -= ToggleAll_Click;
                _toggleAll.Click += ToggleAll_Click;

                // set initial visual tri-state to match current options (if any)
                UpdateToggleAllState();
            }

            // if Options already exist but HookOptions hasn't run, ensure _hookedOptions is set
            if (_hookedOptions == null && Options != null)
            {
                HookOptions(Options);
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            PopulateOptionsFromViewModel();
        }

        private void PopulateOptionsFromViewModel()
        {
            // Walk logical/visual parents using Control.Parent to find the EventHistoryViewModel
            var node = this as Control;
            while (node != null)
            {
                if (node.DataContext is EventHistoryViewModel vm)
                {
                    // capture previous enabled map so we don't lose user choices when rebuilding
                    var previous =
                        Options?.ToDictionary(o => o.Value, o => o.IsEnabled)
                        ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                    // use the unfiltered source so options remain stable even when a column filter is applied
                    var items = vm.AllLogEntries;
                    if (!string.IsNullOrEmpty(HeaderText) && items != null)
                    {
                        IEnumerable<string> values = Enumerable.Empty<string>();
                        switch (HeaderText)
                        {
                            case "Source":
                                values = items.Select(i =>
                                    i.Source?.Name ?? i.Source?.ToString() ?? string.Empty
                                );
                                break;
                            case "Target":
                                values = items.Select(i =>
                                    i.Target?.Name ?? i.Target?.ToString() ?? string.Empty
                                );
                                break;
                            case "Ability":
                                values = items.Select(i => i.Ability ?? string.Empty);
                                break;
                            case "Effect":
                                values = items.Select(i => i.Effect.EffectName ?? string.Empty);
                                break;
                            case "Type":
                                values = items.Select(i =>
                                    (
                                        i.Value?.EventType != DamageType.none
                                            ? i.Value.EventType.ToString()
                                            : i.Effect?.EffectType.ToString()
                                    ) ?? string.Empty
                                );
                                break;
                            case "Time":
                                values = items.Select(i => i.SecondsSinceCombatStart.ToString());
                                break;
                            case "Value":
                                values = items.Select(i => i.Value?.ToString() ?? string.Empty);
                                break;
                            default:
                                var propInfo = typeof(DisplayableLogEntry).GetProperty(
                                    HeaderText,
                                    BindingFlags.Public
                                        | BindingFlags.Instance
                                        | BindingFlags.IgnoreCase
                                );
                                if (propInfo != null)
                                    values = items.Select(i =>
                                        propInfo.GetValue(i)?.ToString() ?? string.Empty
                                    );
                                break;
                        }

                        var distinct = values
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Distinct()
                            .OrderBy(s => s);
                        var distinctList = distinct.ToList();
                        // build options preserving prior IsEnabled state when possible
                        var newOptions = distinctList
                            .Select(v => new FilterOption
                            {
                                Value = v,
                                IsEnabled = previous.TryGetValue(v, out var was) ? was : true,
                            })
                            .ToList();

                        // Replace Options on UI thread
                        Dispatcher.UIThread.Post(() =>
                            Options = new ObservableCollection<FilterOption>(newOptions)
                        );
                    }

                    break;
                }

                node = node.Parent as Control;
            }
        }

        private void HookOptions(ObservableCollection<FilterOption>? opts)
        {
            // Unhook previous
            if (_hookedOptions != null)
            {
                _hookedOptions.CollectionChanged -= Options_CollectionChanged;
                foreach (var o in _hookedOptions)
                    o.PropertyChanged -= Option_PropertyChanged;
            }

            _hookedOptions = opts;

            if (_hookedOptions != null)
            {
                _hookedOptions.CollectionChanged += Options_CollectionChanged;
                foreach (var o in _hookedOptions)
                    o.PropertyChanged += Option_PropertyChanged;
            }

            // update the tri-state to reflect the new collection
            UpdateToggleAllState();
        }

        private void Options_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (FilterOption oldItem in e.OldItems)
                    oldItem.PropertyChanged -= Option_PropertyChanged;
            if (e.NewItems != null)
                foreach (FilterOption newItem in e.NewItems)
                    newItem.PropertyChanged += Option_PropertyChanged;

            UpdateToggleAllState();
        }

        private void Option_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterOption.IsEnabled))
            {
                if (_suppressOptionPropertyChanged)
                    return;

                // Ensure we read the up-to-date value from the option and then notify parent.
                // Run on UI thread to avoid races.
                if (Dispatcher.UIThread.CheckAccess())
                {
                    UpdateToggleAllState();
                    OnOptionChanged();
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateToggleAllState();
                        OnOptionChanged();
                    });
                }
            }
        }

        private void UpdateFilteringVisual()
        {
            // compute from Options if not already up-to-date
            IsFiltering = Options != null && Options.Any() && Options.Any(o => !o.IsEnabled);

            // add/remove class on the header button so XAML styles can target it
            var btn = this.FindControl<Button>("RootButton");
            if (btn != null)
            {
                if (IsFiltering)
                {
                    if (!btn.Classes.Contains("filtering"))
                        btn.Classes.Add("filtering");
                }
                else
                {
                    btn.Classes.Remove("filtering");
                }
            }
        }

        private void UpdateToggleAllState()
        {
            if (_toggleAll == null)
                return;

            if (_hookedOptions == null || _hookedOptions.Count == 0)
            {
                _suppressToggleAllEvents = true;
                _toggleAll.IsChecked = false;
                _lastToggleAllState = false;
                _suppressToggleAllEvents = false;
                return;
            }

            int total = _hookedOptions.Count;
            int enabled = _hookedOptions.Count(o => o.IsEnabled);

            bool? state = enabled == 0 ? false : (enabled == total ? true : null);

            // avoid firing handlers while we set programmatically
            _suppressToggleAllEvents = true;
            _toggleAll.IsChecked = state;
            _lastToggleAllState = state;
            _suppressToggleAllEvents = false;
        }

        private void ToggleAll_Click(object? sender, RoutedEventArgs e)
        {
            // Prevent the event from bubbling up and allowing the user to enter the indeterminate
            // state themselves by clicking on a checked checkbox.
            if (e != null)
                e.Handled = true;

            if (_toggleAll == null || _hookedOptions == null)
            {
                return;
            }
            if (_suppressToggleAllEvents)
            {
                return;
            }

            // Prevent normal Checked/Unchecked handlers while we update
            _suppressToggleAllEvents = true;
            try
            {
                var prev = _lastToggleAllState;
                _suppressOptionPropertyChanged = true;
                if (prev == true || prev == null)
                {
                    // was checked or mixed -> uncheck all children
                    foreach (var o in _hookedOptions)
                        o.IsEnabled = false;
                }
                else
                {
                    // was unchecked -> check all children
                    foreach (var o in _hookedOptions)
                        o.IsEnabled = true;
                }
                _suppressOptionPropertyChanged = false;

                // recompute tri-state from children (will set IsChecked programmatically)
                UpdateToggleAllState();

                // Update flyout button color
                UpdateFilteringVisual();

                // Notify once after batch change
                if (Dispatcher.UIThread.CheckAccess())
                    OnOptionChanged();
                else
                    Dispatcher.UIThread.Post(OnOptionChanged);
            }
            finally
            {
                _suppressToggleAllEvents = false;
            }

            e.Handled = true;
        }

        // private void ToggleAll_Checked(object? sender, RoutedEventArgs e)
        // {
        //     if (_suppressToggleAllEvents)
        //         return;
        //     if (_hookedOptions == null)
        //         return;

        //     _suppressOptionPropertyChanged = true;
        //     foreach (var o in _hookedOptions)
        //         o.IsEnabled = true;
        //     _suppressOptionPropertyChanged = false;

        //     // notify once after batch change
        //     if (Dispatcher.UIThread.CheckAccess())
        //         OnOptionChanged();
        //     else
        //         Dispatcher.UIThread.Post(OnOptionChanged);
        // }

        // private void ToggleAll_Unchecked(object? sender, RoutedEventArgs e)
        // {
        //     if (_suppressToggleAllEvents)
        //         return;
        //     if (_hookedOptions == null)
        //         return;

        //     _suppressOptionPropertyChanged = true;
        //     foreach (var o in _hookedOptions)
        //         o.IsEnabled = false;
        //     _suppressOptionPropertyChanged = false;

        //     // notify once after batch change
        //     if (Dispatcher.UIThread.CheckAccess())
        //         OnOptionChanged();
        //     else
        //         Dispatcher.UIThread.Post(OnOptionChanged);
        // }

        private void ToggleAll_Indeterminate(object? sender, RoutedEventArgs e)
        {
            // user set tri-state to indeterminate; do nothing (state represents mixed selection)
            // ensure we don't react to programmatic sets
            if (_suppressToggleAllEvents)
                return;
        }

        private void OnOptionChanged()
        {
            // gather enabled values and notify subscribers
            var enabled =
                Options?.Where(o => o.IsEnabled).Select(o => o.Value).ToList()
                ?? new List<string>();
            FilterChanged?.Invoke(this, new FilterChangedEventArgs(enabled));

            UpdateFilteringVisual();
        }
    }

    // Simple option with change notification (helps two-way binding)
    public class FilterOption : INotifyPropertyChanged
    {
        private bool _isEnabled;
        public string Value { get; set; } = "";
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class FilterChangedEventArgs : EventArgs
    {
        public FilterChangedEventArgs(IList<string> enabledValues)
        {
            EnabledValues = enabledValues;
        }

        public IList<string> EnabledValues { get; }
    }
}
