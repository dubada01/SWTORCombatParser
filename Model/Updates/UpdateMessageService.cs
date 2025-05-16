using SWTORCombatParser.DataStructures.Updates;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Updates
{
    internal static class UpdateMessageService
    {
        internal static async Task<List<UpdateMessage>> GetUpdateMessages()
        {
            var allMessages = await MessageFetcher.GetMessages();
            var clearedMessages = Settings.GetListSetting<string>("cleared_messages");
            return allMessages.Where(m => !clearedMessages.Contains(m.MessageId.ToString())).ToList();
        }
        internal static async Task<List<UpdateMessage>> GetAllUpdateMessages()
        {
            return await MessageFetcher.GetMessages();
        }
        internal static void Reset()
        {
            var clearedMessages =  new List<string>();
            Settings.WriteSetting("cleared_messages", clearedMessages);
        }
        internal static void ClearMessage(Guid messageToClear)
        {
            var clearedMessages = Settings.GetListSetting<string>("cleared_messages");
            if (clearedMessages == null)
                clearedMessages = new List<string>();
            clearedMessages.Add(messageToClear.ToString());
            Settings.WriteSetting("cleared_messages", clearedMessages);
        }
    }
}
