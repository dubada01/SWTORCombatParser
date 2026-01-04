using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class DotModuleViewModel : ReactiveObject
    {
        private EntityInfo _bossInfo;
        private bool isActive;
        private double _currentScale;
        private ObservableCollection<TimerInstanceViewModel> _activeDots = new ObservableCollection<TimerInstanceViewModel>();
        private bool _personalDotsAreEnabled;

        public ObservableCollection<TimerInstanceViewModel> ActiveDOTS
        {
            get => _activeDots;
            set => this.RaiseAndSetIfChanged(ref _activeDots, value);
        }

        public bool PersonalDOTSAreEnabled
        {
            get => _personalDotsAreEnabled;
            set => this.RaiseAndSetIfChanged(ref  _personalDotsAreEnabled, value);
        }

        public DotModuleViewModel(EntityInfo bossInfo, double scale)
        {
            _currentScale = scale;
            isActive = true;
            _bossInfo = bossInfo;
            TimerController.TimerExpired += RemoveTimer;
            TimerController.TimerTriggered += AddTimerVisual;
            TimerController.ReorderRequested += ReorderTimers;
            Settings.SettingsUpdated += CheckForPersonalDotEnabled;
            PersonalDOTSAreEnabled = Settings.ReadSettingOfType<bool>(Settings.BossFrameDOTVisibilitySetting);
        }

        private void CheckForPersonalDotEnabled(string settingName)
        {
            if (settingName != Settings.BossFrameDOTVisibilitySetting)
                return;
            PersonalDOTSAreEnabled = Settings.ReadSettingOfType<bool>(Settings.BossFrameDOTVisibilitySetting);
        }

        public void SetActive(bool state)
        {
            isActive = state;
        }
        public void SetScale(double scale)
        {
            _currentScale = scale;
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var timer in ActiveDOTS)
                {
                    timer.Scale = scale;
                }
            });
        }
        private void RemoveTimer(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ActiveDOTS.Remove(obj);
            });
            callback(obj);
        }

        private void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (!isActive)
                return;
            if (obj.TargetId == _bossInfo.Entity.Id && !obj.SourceTimer.IsMechanic && !obj.SourceTimer.IsSubTimer && obj.TimerValue > 0)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    obj.Scale = _currentScale;
                    ActiveDOTS.Add(obj);
                });
            }
            callback(obj);
        }

        private void ReorderTimers(string id)
        {
            if(ActiveDOTS.All(t => t.SourceTimer.Id != id))
                return;
            var currentTimers = ActiveDOTS.OrderBy(v => v.TimerValue);
            ActiveDOTS = new ObservableCollection<TimerInstanceViewModel>(currentTimers);
        }
    }
}
