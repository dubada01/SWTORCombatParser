using SWTORCombatParser.DataStructures.Updates;
using SWTORCombatParser.Model.Updates;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.ViewModels.Update
{
    internal class FeatureUpdatesViewModel
    {
        public event Action OnEmpty = delegate { };
        public List<UpdateInstanceViewModel> CurrentUpdateMessages { get; set; }
        public FeatureUpdatesViewModel(List<UpdateMessage> newMessages)
        {
            CurrentUpdateMessages = newMessages.Select(newMessages => new UpdateInstanceViewModel(newMessages)).ToList();
            CurrentUpdateMessages.ForEach(u => u.OnClear += RemoveAndClear);
        }

        private void RemoveAndClear(UpdateInstanceViewModel message)
        {
            UpdateMessageService.ClearMessage(message.MessageId);
            CurrentUpdateMessages?.Remove(message);
            if(CurrentUpdateMessages.Count == 0)
            {
                OnEmpty();
            }
        }
    }
}
