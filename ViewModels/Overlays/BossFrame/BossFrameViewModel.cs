using SWTORCombatParser.Views.Overlay.BossFrame;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class BossFrameViewModel : INotifyPropertyChanged
    {
        private DotModuleViewModel dotModuleViewModel;
        private HPModuleViewModel _hpVM;
        private MechanicsTimersModuleViewModel _mechsVM;

        public Entity CurrentBoss { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public BossFrameViewModel(EntityInfo bossInfo, bool dotTrackingEnabled, bool mechTrackingEnabled)
        {        
            App.Current.Dispatcher.Invoke(() => {
                CurrentBoss = bossInfo.Entity;

                HPContent = new HPModule();
                _hpVM = new HPModuleViewModel(bossInfo);
                HPContent.DataContext = _hpVM;

                DOTSContent = new DotModuleView();
                dotModuleViewModel = new DotModuleViewModel(bossInfo,dotTrackingEnabled);
                DOTSContent.DataContext = dotModuleViewModel;

                MechanicsModule = new MechanicsTimersModule();
                _mechsVM = new MechanicsTimersModuleViewModel(bossInfo,mechTrackingEnabled);
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

        public HPModule HPContent { get; set; }
        public DotModuleView DOTSContent { get; set; }
        public MechanicsTimersModule MechanicsModule { get; set; }
    }
}
