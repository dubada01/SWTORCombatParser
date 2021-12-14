using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Home_View_Models
{
    public class ParticipantSelectionViewModel:INotifyPropertyChanged
    {
        public event Action<Entity> ParticipantSelected = delegate { };
        public ObservableCollection<ParticipantViewModel> AvailableParticipants { get; set; } = new ObservableCollection<ParticipantViewModel>();
        public int Rows { get; set; }
        public int Columns { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void SelectParticipant(Entity participant)
        {
            var uiElement = AvailableParticipants.FirstOrDefault(p => p.Entity.Id == participant.Id);
            if (uiElement == null)
                return;
            uiElement.ToggleSelection();
        }
        public void SelectLocalPlayer()
        {
            var uiElement = AvailableParticipants.FirstOrDefault(p => p.Entity.IsLocalPlayer);
            if (uiElement == null)
                return;
            uiElement.ToggleSelection();
        }
        public void SetParticipants(List<Entity> availableEntities)
        {
            var participants = availableEntities.Select(e => GenerateInstance(e));
            AvailableParticipants = new ObservableCollection<ParticipantViewModel>(participants);
            foreach(var participant in AvailableParticipants)
            {
                participant.SelectionChanged += SelectParticipant;
            }
            if (AvailableParticipants.Count <= 4)
            { 
                Columns = 4;
                Rows = 1;
            }
            if (AvailableParticipants.Count > 4)
            {
                Columns = 4;
                Rows = 2;
            }
            if (AvailableParticipants.Count > 8)
            {
                Columns = 8;
                Rows = 2;
            }
            OnPropertyChanged("AvailableParticipants");
            OnPropertyChanged("Rows");
            OnPropertyChanged("Columns");
        }

        private void SelectParticipant(ParticipantViewModel obj)
        {
            var previouslySelected = AvailableParticipants.Where(p=>p.Entity.Id!=obj.Entity.Id).FirstOrDefault(p => p.IsSelected);
            if (previouslySelected != null)
                previouslySelected.ToggleSelection();

            ParticipantSelected(obj.Entity);
        }

        private ParticipantViewModel GenerateInstance(Entity e)
        {
            ParticipantViewModel viewModel = new ParticipantViewModel();
            viewModel.Entity = e;
            viewModel.PlayerName = e.Name;
            viewModel.IsLocalPlayer = e.IsLocalPlayer;
            return viewModel;
        }
        public void UpdateParticipantsData(Combat info)
        {
            foreach (var participant in info.CharacterParticipants)
            {
                var participantVM = AvailableParticipants.FirstOrDefault(p => p.PlayerName == participant.Name);
                if (participantVM == null)
                    continue;
                var imagePath = "../../resources/question-mark.png";
                if (participant.IsCompanion)
                    imagePath = "../../resources/LocalPlayerIcon.png";
                if (info.CharacterClases.ContainsKey(participant))
                {
                    var swtorClass = info.CharacterClases[participant];
                    imagePath = GetRoleImage(swtorClass);
                    participantVM.RoleOrdering = swtorClass.Role == Role.Tank ? 0 : swtorClass.Role == Role.Healer ? 1 : 2;
                }
                participantVM.SetValues(info.EDPS[participant], info.EHPS[participant], info.EDTPS[participant], imagePath);
            }
            AvailableParticipants = new ObservableCollection<ParticipantViewModel>(AvailableParticipants.OrderBy(p => p.RoleOrdering));
            OnPropertyChanged("AvailableParticipants");
        }

        private string GetRoleImage(SWTORClass sWTORClass)
        {
            if(sWTORClass == null)
                return "../../resources/question-mark.png";
            switch (sWTORClass.Role)
            {
                case Role.DPS:
                    return "../../resources/dpsIcon.png";
                case Role.Healer:
                    return "../../resources/healingIcon.png";
                case Role.Tank:
                    return "../../resources/tankIcon.png";
                default:
                    return "../../resources/question-mark.png";
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
