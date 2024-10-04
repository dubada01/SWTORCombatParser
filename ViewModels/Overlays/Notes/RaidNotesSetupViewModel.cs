using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.AbilityList;
using SWTORCombatParser.Views.Overlay.Notes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace SWTORCombatParser.ViewModels.Overlays.Notes
{
    public class RaidNotesSetupViewModel
    {
        private bool inInstance = false;
        private RaidNotesViewModel _viewModel;
        private RaidNotesView _view;
        private bool raidNotesEnabled;
        public event Action<bool> OnEnabledChanged = delegate { };
        public RaidNotesSetupViewModel()
        {
            _viewModel = new RaidNotesViewModel();
            _viewModel.OverlayName = "RaidNotes";
            _viewModel.OnClosing += Disable;
            _viewModel.OnInInstanceChanged += InInstanceChanged;
            _view = new RaidNotesView(_viewModel);
            CombatLogStreamer.NewLineStreamed += CheckForConverstaion;
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType(_viewModel.OverlayName);
            _view.SetSizeAndLocation(new Point(defaults.Position.X,defaults.Position.Y), new Point(defaults.WidtHHeight.X, defaults.WidtHHeight.Y));
            if (defaults.Acive)
                RaidNotesEnabled = true;
        }

        private void CheckForConverstaion(ParsedLogEntry entry)
        {
            Dispatcher.UIThread.Invoke(() => {

                if (entry.Effect.EffectId == _7_0LogParsing.InConversationEffectId && entry.Effect.EffectType == EffectType.Apply && entry.Source.IsLocalPlayer)
                {
                    _view.Hide();
                }
                if (entry.Effect.EffectId == _7_0LogParsing.InConversationEffectId && entry.Effect.EffectType == EffectType.Remove && entry.Source.IsLocalPlayer)
                {
                    if (_viewModel.IsEnabled && _viewModel.InInstance)
                    {
                        _view.Show();
                    }
                }
            });

        }

        private void InInstanceChanged(bool instanceStatus)
        {
            inInstance = instanceStatus;
            SetVisibilityForInstanceState();

        }
        private void SetVisibilityForInstanceState()
        {
            Dispatcher.UIThread.Invoke(() => {
                if (inInstance && RaidNotesEnabled)
                {
                    _view.Show();
                }
                if (!inInstance)
                {
                    _view.Hide();
                }
            });

        }
        public bool RaidNotesEnabled
        {
            get => raidNotesEnabled; set
            {
                raidNotesEnabled = value; 
                if (raidNotesEnabled)
                {
                    if(inInstance)
                        _view.Show();
                    _viewModel.IsEnabled = true;
                }
                else
                {
                    _view.Hide();
                    _viewModel.IsEnabled = false;
                }
                DefaultGlobalOverlays.SetActive(_viewModel.OverlayName, raidNotesEnabled);
            }
        }
        private void Disable()
        {
            RaidNotesEnabled = false;
            OnEnabledChanged(false);
        }
        internal void UpdateLock(bool overlaysLocked)
        {
            if (overlaysLocked)
            {
                _viewModel.LockOverlays();
                SetVisibilityForInstanceState();
            }
            else
            {
                _viewModel.UnlockOverlays();
                if(RaidNotesEnabled)
                    _view?.Show();
            }
        }
    }
}
