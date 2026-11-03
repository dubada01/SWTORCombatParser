using SWTORCombatParser.DataStructures.Updates;
using SWTORCombatParser.Model.CloudRaiding;
using System;
using System.Reactive;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Update
{
    internal class UpdateInstanceViewModel:ReactiveObject
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
        public ReactiveCommand<Unit,Unit> ClearCommand => ReactiveCommand.Create(Clear);
        public void Clear()
        {
            OnClear(this);
        }
        public bool HasAction { get; set; }
        public string ActionText { get; set; } = "Update";
        public ReactiveCommand<Unit,Unit> ActionCommand => ReactiveCommand.Create(CustomAction);
        public Action CustomAction { get; set; }
    }
}
