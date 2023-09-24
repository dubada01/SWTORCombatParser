using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Death_Review
{
    public class DeathPlayerListViewModel : INotifyPropertyChanged
    {
        public event Action<List<Entity>> ParticipantSelected = delegate { };
        public List<ParticipantViewModel> AvailableParticipants { get; set; } = new List<ParticipantViewModel>();
        public int Rows { get; set; }
        public int Columns { get; set; }
        public Combat SelectedCombat { get; set; }
        public List<Entity> SelectedParticipants { get; set; } = new List<Entity>();

        public event PropertyChangedEventHandler PropertyChanged;
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
            OnPropertyChanged("Rows");
            OnPropertyChanged("Columns");
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
            ParticipantSelected(SelectedParticipants);
        }
        private ParticipantViewModel GenerateInstance(Entity e, bool diedNatrually)
        {
            ParticipantViewModel viewModel = new ParticipantViewModel();
            viewModel.Entity = e;
            viewModel.PlayerName = e.Name;
            viewModel.IsLocalPlayer = e.IsLocalPlayer;
            viewModel.RoleImageSource = Path.Combine(Environment.CurrentDirectory, "resources/question-mark.png");
            viewModel.DiedNatrually = diedNatrually;
            return viewModel;
        }
        public void Reset()
        {
            AvailableParticipants.Clear();
        }
        public List<Entity> UpdateParticipantsData(Combat info, List<Entity> playersDiedNatrually)
        {
            AvailableParticipants.Clear();
            SelectedParticipants.Clear();
            var entitiesToView = info.CharacterParticipants;
            foreach (var participant in entitiesToView)
            {
                ParticipantViewModel participantViewModel = GenerateInstance(participant, playersDiedNatrually.Contains(participant));

                var imagePath = Path.Combine(Environment.CurrentDirectory, "resources/question-mark.png");
                if (participant.IsCompanion)
                    imagePath = Path.Combine(Environment.CurrentDirectory, "resources/LocalPlayerIcon.png");
                if (info.CharacterClases.ContainsKey(participant))
                {
                    var swtorClass = info.CharacterClases[participant];
                    imagePath = GetRoleImage(swtorClass);
                    participantViewModel.RoleOrdering = swtorClass.Role == Role.Tank ? 0 : swtorClass.Role == Role.Healer ? 1 : 2;
                }
                participantViewModel.SetValues(info.EDPS[participant], info.EHPS[participant], info.EDTPS[participant], imagePath);
                AvailableParticipants.Add(participantViewModel);
                if (playersDiedNatrually.Contains(participant) || playersDiedNatrually.Count == 0)
                {
                    participantViewModel.IsSelected = true;
                    SelectedParticipants.Add(participant);
                }
                participantViewModel.SelectionChanged += SelectParticipant;
            }
            AvailableParticipants = new List<ParticipantViewModel>(AvailableParticipants.OrderBy(p => p.RoleOrdering));
            UpdateLayout();
            OnPropertyChanged("AvailableParticipants");
            return SelectedParticipants;
        }
        public void SetEntityHPS(List<EntityInfo> entityInfo)
        {
            foreach (var participant in AvailableParticipants)
            {
                var info = entityInfo.FirstOrDefault(e => e.Entity.LogId == participant.Entity.LogId);
                if (info != null)
                {
                    participant.HPPercent = info.CurrentHP / info.MaxHP;
                }
            }
        }
        private string GetRoleImage(SWTORClass sWTORClass)
        {
            if (sWTORClass == null)
                return Path.Combine(Environment.CurrentDirectory, "resources/question-mark.png");
            switch (sWTORClass.Role)
            {
                case Role.DPS:
                    return Path.Combine(Environment.CurrentDirectory, "resources/dpsIcon.png");
                case Role.Healer:
                    return Path.Combine(Environment.CurrentDirectory, "resources/healingIcon.png");
                case Role.Tank:
                    return Path.Combine(Environment.CurrentDirectory, "resources/tankIcon.png");
                default:
                    return Path.Combine(Environment.CurrentDirectory, "resources/question-mark.png");
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
