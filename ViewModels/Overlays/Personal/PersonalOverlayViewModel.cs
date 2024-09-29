﻿using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Views.Overlay.Personal;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;

namespace SWTORCombatParser.ViewModels.Overlays.Personal
{
    public class PersonalOverlayViewModel : BaseOverlayViewModel
    {
        private PersonalOverlayWindow _window;
        private bool overlaysMoveable;
        private double rows;
        private double _currentScale;
        private string _currentOwner;
        public PersonalOverlayViewModel()
        {
            _currentScale = 1;
            _window = new PersonalOverlayWindow(this);
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType("Personal");
            Active = defaults.Acive;
            _window.SetSizeAndLocation(new Point(defaults.Position.X, defaults.Position.Y), new Point(defaults.WidtHHeight.X, defaults.WidtHHeight.Y));

            DefaultPersonalOverlaysManager.NewDefaultSelected += UpdateMetrics;
            CombatLogStreamer.NewLineStreamed += CheckForConversation;
            UpdateMetrics("Damage");
        }
        private bool _conversationActive;
        private void CheckForConversation(ParsedLogEntry obj)
        {
            if (!obj.Source.IsLocalPlayer)
                return;
            if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Apply && Active)
            {
                _conversationActive = true;
                HideOverlayWindow();
            }

            if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Remove && Active)
            {
                _conversationActive = false;
                ShowOverlayWindow();
            }
        }
        private void UpdateMetrics(string defaultName)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                PersonalOverlayInstances.Clear();
                _currentOwner = defaultName;
                var dpsSettings = DefaultPersonalOverlaysManager.GetSettingsForOwner(defaultName);
                var intitialCells = dpsSettings.CellInfos.Select(m => new PersonalOverlayInstanceViewModel(OverlaysMoveable, _currentScale, m));
                foreach (var cell in intitialCells)
                {
                    cell.CellRemoved += RemoveCell;
                    cell.CellUpdated += UpdateDefaults;
                    cell.CellChangedFromNone += AddNewBlank;
                    PersonalOverlayInstances.Add(cell);
                }
                AddNewBlank();
            });
        }

        private void AddNewBlank()
        {
            var initialCell = new PersonalOverlayInstanceViewModel(OverlaysMoveable, _currentScale);
            initialCell.CellRemoved += RemoveCell;
            initialCell.CellUpdated += UpdateDefaults;
            initialCell.CellChangedFromNone += AddNewBlank;
            PersonalOverlayInstances.Add(initialCell);
            if (OverlaysMoveable)
                Rows = (int)Math.Ceiling(PersonalOverlayInstances.Count / 2d);
            else
                Rows = (int)Math.Floor(PersonalOverlayInstances.Count / 2d);
        }

        private void RemoveCell(PersonalOverlayInstanceViewModel obj)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                PersonalOverlayInstances.Remove(obj);
                UpdateDefaults();
                Rows = (int)Math.Ceiling(PersonalOverlayInstances.Count / 2d);
            });
        }
        private void UpdateDefaults()
        {
            DefaultPersonalOverlaysManager.SetSettingsForOwner(_currentOwner, new PersonalOverlaySettings { CellInfos = PersonalOverlayInstances.Where(c => c.SelectedMetric != OverlayType.None).Select(i => i.CurrentCellInfo).ToList() });
        }
        public ObservableCollection<PersonalOverlayInstanceViewModel> PersonalOverlayInstances { get; set; } = new ObservableCollection<PersonalOverlayInstanceViewModel>();
        public void UpdateScale(double scale)
        {
            _currentScale = scale;
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var cell in PersonalOverlayInstances)
                {
                    cell.UpdateScale(scale);
                }
            });
        }
        public new bool OverlaysMoveable
        {
            get => overlaysMoveable; internal set
            {
                if (overlaysMoveable == value)
                    return;
                overlaysMoveable = value;
                foreach (var instance in PersonalOverlayInstances)
                {
                    instance.OverlayUnlocked = overlaysMoveable;
                }
                if (!OverlaysMoveable)
                {
                    if (PersonalOverlayInstances.Count % 2 != 0)
                    {
                        Rows = Rows - 1;
                    }
                }
                else
                {
                    if (PersonalOverlayInstances.Count % 2 != 0)
                    {
                        Rows = Rows + 1;
                    }
                }
                OnPropertyChanged();
            }
        }
        public double Rows
        {
            get => rows; set
            {
                rows = value;
                OnPropertyChanged();
            }
        }
    }
}
