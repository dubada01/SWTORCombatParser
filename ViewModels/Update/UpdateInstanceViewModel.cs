using Prism.Commands;
using SWTORCombatParser.DataStructures.Updates;
using SWTORCombatParser.Model.CloudRaiding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Update
{
    internal class UpdateInstanceViewModel
    {
        public UpdateInstanceViewModel(UpdateMessage message)
        {
            MessageId = message.MessageId;
            HeaderText = $"{message.CreationTime.ToString("MM/dd/yyyy")} {message.UpdateMessageHeader}";
            ContentText = message.UpdateMessageBody;
            HasAction = message.IsSoftwareUpdateMessage;
            if (message.IsSoftwareUpdateMessage)
            {
                CustomAction = VersionChecker.OpenMicrosoftStoreToAppPage;
            }
            else
            {
                CustomAction = () => { };
            }
        }
        public Guid MessageId { get; set; }
        public event Action<UpdateInstanceViewModel> OnClear = delegate { };
        public string HeaderText { get; set; }
        public string ContentText { get; set; }
        public ICommand ClearCommand => new DelegateCommand(Clear);
        public void Clear()
        {
            OnClear(this);
        }
        public bool HasAction { get; set; }
        public string ActionText { get; set; } = "Update";
        public ICommand ActionCommand => new DelegateCommand(CustomAction);
        public Action CustomAction { get; set; }
    }
}
