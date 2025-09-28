using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Death_Review
{
    public class DeathPlayerListViewModel : ReactiveObject
    {
        private ObservableCollection<ParticipantViewModel> _availableParticipants = new ObservableCollection<ParticipantViewModel>();
        private int _rows;
        private int _columns;
        private Combat _selectedCombat;
        private ObservableCollection<Entity> _selectedParticipants = new ObservableCollection<Entity>();
        public event Action<List<Entity>> ParticipantSelected = delegate { };

        public ObservableCollection<ParticipantViewModel> AvailableParticipants
        {
            get => _availableParticipants;
            set => this.RaiseAndSetIfChanged(ref _availableParticipants, value);
        }

        public int Rows
        {
            get => _rows;
            set => this.RaiseAndSetIfChanged(ref _rows, value);
        }

        public int Columns
        {
            get => _columns;
            set => this.RaiseAndSetIfChanged(ref _columns, value);
        }

        public Combat SelectedCombat
        {
            get => _selectedCombat;
            set => _selectedCombat = value;
        }

        public ObservableCollection<Entity> SelectedParticipants
        {
            get => _selectedParticipants;
            set => this.RaiseAndSetIfChanged(ref _selectedParticipants, value);
        }
        
        public DeathPlayerListViewModel()
        {

        }
        private void UpdateLayout()
        {
            if (AvailableParticipants.Count <= 8)
            {
                Columns = 8;
                Rows = 1;
            }
            if (AvailableParticipants.Count > 8)
            {
                Columns = 8;
                Rows = 2;
            }
        }

        private void SelectParticipant(ParticipantViewModel obj, bool isSelected)
        {
            SetSelection(obj.Entity, isSelected);
        }
        private void SetSelection(Entity obj, bool isSelected)
        {
            if (!AvailableParticipants.Any(part => part.Entity == obj))
                return;
            if (isSelected && !SelectedParticipants.Contains(obj))
                SelectedParticipants.Add(obj);
            if (!isSelected && SelectedParticipants.Contains(obj))
                SelectedParticipants.Remove(obj);
            ParticipantSelected(SelectedParticipants.ToList());
        }
        private ParticipantViewModel GenerateInstance(Entity e, bool diedNatrually)
        {
            ParticipantViewModel viewModel = new ParticipantViewModel();
            viewModel.Entity = e;
            viewModel.PlayerName = e.Name;
            viewModel.IsLocalPlayer = e.IsLocalPlayer;
            viewModel.RoleImageSource = IconFactory._unknownIcon;
            viewModel.DiedNatrually = diedNatrually;
            return viewModel;
        }
        public void Reset()
        {
            AvailableParticipants.Clear();
        }
        public ObservableCollection<Entity> UpdateParticipantsData(Combat info, List<Entity> playersDiedNatrually)
        {
            AvailableParticipants.Clear();
            SelectedParticipants.Clear();
            var entitiesToView = info.CharacterParticipants;
            foreach (var participant in entitiesToView)
            {
                ParticipantViewModel participantViewModel = GenerateInstance(participant, playersDiedNatrually.Contains(participant));

                var imagePath = IconFactory._unknownIcon;
                if (participant.IsCompanion)
                    imagePath = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/LocalPlayerIcon.png")));
                if (info.CharacterClases.ContainsKey(participant))
                {
                    var swtorClass = info.CharacterClases[participant];
                    imagePath = GetRoleImage(swtorClass);
                    participantViewModel.RoleOrdering = swtorClass.Role == Role.Tank ? 0 : swtorClass.Role == Role.Healer ? 1 : 2;
                }
                participantViewModel.SetValues(info.EDPS[participant], info.EHPS[participant], info.EDTPS[participant], imagePath);
                AvailableParticipants.Add(participantViewModel);
                participantViewModel.SelectionChanged += SelectParticipant;
            }
            AvailableParticipants = new ObservableCollection<ParticipantViewModel>(AvailableParticipants.OrderBy(p => p.RoleOrdering));
            var initiallySelectedPlayer =  AvailableParticipants.FirstOrDefault(a => playersDiedNatrually.Contains(a.Entity));
            if (initiallySelectedPlayer != null)
            {
                initiallySelectedPlayer.IsSelected = true;
                SelectParticipant(initiallySelectedPlayer, true);
            }
            UpdateLayout();
            return SelectedParticipants;
        }
        public void SetEntityHPS(List<EntityInfo> entityInfo)
        {
            foreach (var participant in AvailableParticipants)
            {
                var info = entityInfo.FirstOrDefault(e => e.Entity.LogId == participant.Entity.LogId);
                if (info != null)
                {
                    participant.HPPercent = info.CurrentHP / (double)info.MaxHP;
                }
            }
        }
        private Bitmap GetRoleImage(SWTORClass sWTORClass)
        {
            if (sWTORClass == null)
                return IconFactory._unknownIcon;
            switch (sWTORClass.Role)
            {
                case Role.DPS:
                    return new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/dpsIcon.png")));
                case Role.Healer:
                    return new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/healingIcon.png")));
                case Role.Tank:
                    return new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/tankIcon.png")));
                default:
                    return IconFactory._unknownIcon;
            }
        }
    }
}
