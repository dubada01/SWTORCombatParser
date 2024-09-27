using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Views.Overlay.BossFrame;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class BossFrameViewModel : INotifyPropertyChanged
    {
        private DotModuleViewModel dotModuleViewModel;
        private HPModuleViewModel _hpVM;
        private MechanicsTimersModuleViewModel _mechsVM;
        private double _scale;
        public Entity CurrentBoss { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public BossFrameViewModel(EntityInfo bossInfo, bool dotTrackingEnabled, bool mechTrackingEnabled, bool isDuplicate, double scale)
        {
            _scale = scale;
            Dispatcher.UIThread.Invoke(() =>
            {
                CurrentBoss = bossInfo.Entity;

                HPContent = new HPModule();
                _hpVM = new HPModuleViewModel(bossInfo, isDuplicate, _scale);
                HPContent.DataContext = _hpVM;

                DOTSContent = new DotModuleView();
                dotModuleViewModel = new DotModuleViewModel(bossInfo, dotTrackingEnabled, _scale);
                DOTSContent.DataContext = dotModuleViewModel;

                MechanicsModule = new MechanicsTimersModule();
                _mechsVM = new MechanicsTimersModuleViewModel(bossInfo, mechTrackingEnabled, _scale);
                MechanicsModule.DataContext = _mechsVM;
            });
        }
        public void UpdateBossFrameState(bool showDots, bool showMechs)
        {
            dotModuleViewModel.SetActive(showDots);
            _mechsVM.SetActive(showMechs);
        }
        public void LogWithBoss(EntityInfo bossInfo)
        {
            UpdateUI(bossInfo);
        }

        private void UpdateUI(EntityInfo bossInfo)
        {
            _hpVM.UpdateHP(bossInfo.CurrentHP);
        }

        internal void UpdateBossFrameScale(double currentScale)
        {
            _scale = currentScale;
            _hpVM.UpdateScale(_scale);
            _mechsVM.SetScale(_scale);
            dotModuleViewModel.SetScale(_scale);
        }

        public HPModule HPContent { get; set; }
        public DotModuleView DOTSContent { get; set; }
        public MechanicsTimersModule MechanicsModule { get; set; }
    }
}
