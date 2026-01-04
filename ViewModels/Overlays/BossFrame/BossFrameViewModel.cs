using System;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Views.Overlay.BossFrame;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class BossFrameViewModel:ReactiveObject
    {
        private DotModuleViewModel dotModuleViewModel;
        private HPModuleViewModel _hpVM;
        private MechanicsTimersModuleViewModel _mechsVM;
        private double _scale;
        private HPModule _hpContent;
        private DotModuleView _dotsContent;
        private MechanicsTimersModule _mechanicsModule;
        public Entity CurrentBoss { get; set; }
        public BossFrameViewModel(EntityInfo bossInfo, bool isDuplicate, double scale)
        {
            _scale = scale;

            CurrentBoss = bossInfo.Entity;

            HPContent = new HPModule();
            _hpVM = new HPModuleViewModel(bossInfo, isDuplicate, _scale);
            HPContent.DataContext = _hpVM;

            DOTSContent = new DotModuleView();
            dotModuleViewModel = new DotModuleViewModel(bossInfo, _scale);
            DOTSContent.DataContext = dotModuleViewModel;

            MechanicsModule = new MechanicsTimersModule();
            _mechsVM = new MechanicsTimersModuleViewModel(bossInfo, _scale);
            MechanicsModule.DataContext = _mechsVM;
        }

        public void LogWithBoss(EntityInfo bossInfo, DateTime timeStamp)
        {
            UpdateUI(bossInfo,timeStamp);
        }

        private void UpdateUI(EntityInfo bossInfo, DateTime timeStamp)
        {
            _hpVM.UpdateHP(bossInfo.CurrentHP);
            _hpVM.UpdateTarget(CombatLogStateBuilder.CurrentState.GetEnemyTargetAtTime(bossInfo.Entity, timeStamp).Entity.Name);
        }
    
        internal void UpdateBossFrameScale(double currentScale)
        {
            _scale = currentScale;
            _hpVM.UpdateScale(_scale);
            _mechsVM.SetScale(_scale);
            dotModuleViewModel.SetScale(_scale);
        }

        public HPModule HPContent
        {
            get => _hpContent;
            set => this.RaiseAndSetIfChanged(ref _hpContent, value);
        }

        public DotModuleView DOTSContent
        {
            get => _dotsContent;
            set => this.RaiseAndSetIfChanged(ref _dotsContent, value);
        }

        public MechanicsTimersModule MechanicsModule
        {
            get => _mechanicsModule;
            set => this.RaiseAndSetIfChanged(ref _mechanicsModule, value);
        }
    }
}
