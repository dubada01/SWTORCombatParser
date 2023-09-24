using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Home_View_Models
{
    public class ParticipantSelectionViewModel : INotifyPropertyChanged
    {
        private bool viewEnemies;

        public event Action<Entity> ParticipantSelected = delegate { };
        public event Action<int> ViewEnemiesToggled = delegate { };
        public List<ParticipantViewModel> AvailableParticipants { get; set; } = new List<ParticipantViewModel>();
        public int Rows { get; set; }
        public int Columns { get; set; }
        public Combat SelectedCombat { get; set; }
        public Entity SelectedParticipant { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public ParticipantSelectionViewModel()
        {
            ParticipantSelectionHandler.SelectionUpdated += SetSelection;
        }
        public bool ViewEnemies
        {
            get => viewEnemies; set
            {
                viewEnemies = value;
                SetParticipants(SelectedCombat);
                UpdateParticipantsData(SelectedCombat);
                var entitiesToShow = ViewEnemies ? SelectedCombat.AllEntities.Where(e => e.IsBoss || e.IsCharacter).ToList() : SelectedCombat.CharacterParticipants;
                ViewEnemiesToggled(entitiesToShow.Count);
                if (!viewEnemies && SelectedParticipant.IsBoss)
                    SetSelection(entitiesToShow.First(e => e.IsLocalPlayer));
            }
        }
        public void SelectLocalPlayer()
        {
            var uiElement = AvailableParticipants.FirstOrDefault(p => p.Entity.IsLocalPlayer);
            if (uiElement == null)
                return;
            SelectParticipant(uiElement, true);
        }
        public List<Entity> SetParticipants(Combat combat)
        {
            SelectedCombat = combat;
            var entitiesToShow = ViewEnemies ? combat.AllEntities.Where(e => e.IsBoss || e.IsCharacter).ToList() : combat.CharacterParticipants;
            var participants = entitiesToShow.Select(e => GenerateInstance(e));
            AvailableParticipants = new List<ParticipantViewModel>(participants);
            foreach (var participant in AvailableParticipants)
            {
                participant.SelectionChanged += SelectParticipant;
            }
            UpdateLayout();
            if (ParticipantSelectionHandler.CurrentlySelectedParticpant == null)
            {
                SelectLocalPlayer();
            }
            OnPropertyChanged("AvailableParticipants");
            return entitiesToShow;
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
            if (isSelected)
                SetSelection(obj.Entity);
        }
        private void SetSelection(Entity obj)
        {
            if (!AvailableParticipants.Any(part => part.Entity == obj))
                return;
            SelectedParticipant = obj;
            var previouslySelected = AvailableParticipants.Where(p => p.Entity.Id != obj.Id).FirstOrDefault(p => p.IsSelected);
            if (previouslySelected != null)
                previouslySelected.ToggleSelection();
            var currentSelection = AvailableParticipants.First(p => p.Entity.Id == obj.Id);
            if (!currentSelection.IsSelected)
                currentSelection.ToggleSelection();
            ParticipantSelected(obj);
            ParticipantSelectionHandler.UpdateSelection(obj);
        }
        private ParticipantViewModel GenerateInstance(Entity e)
        {
            ParticipantViewModel viewModel = new ParticipantViewModel();
            viewModel.Entity = e;
            viewModel.PlayerName = e.Name;
            viewModel.IsLocalPlayer = e.IsLocalPlayer;
            viewModel.RoleImageSource = Path.Combine(Environment.CurrentDirectory, "resources/question-mark.png");
            viewModel.IsSelected = ParticipantSelectionHandler.CurrentlySelectedParticpant?.LogId == 0 ?
                ParticipantSelectionHandler.CurrentlySelectedParticpant?.Id == viewModel.Entity?.Id :
                ParticipantSelectionHandler.CurrentlySelectedParticpant?.LogId == viewModel.Entity?.LogId;
            return viewModel;
        }
        public List<Entity> UpdateParticipantsData(Combat info)
        {
            AvailableParticipants.Clear();
            var entitiesToView = ViewEnemies ? info.AllEntities.Where(e => e.IsBoss || e.IsCharacter).ToList() : info.CharacterParticipants;
            foreach (var participant in entitiesToView)
            {
                ParticipantViewModel participantViewModel = GenerateInstance(participant);
                participantViewModel.SelectionChanged += SelectParticipant;
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
            }
            AvailableParticipants = new List<ParticipantViewModel>(AvailableParticipants.OrderBy(p => p.RoleOrdering));
            UpdateLayout();
            OnPropertyChanged("AvailableParticipants");
            return entitiesToView;
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
