using SWTORCombatParser.Utilities;
using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels
{
    public class TabInstance
    {
        public event Action<TabInstance> RequestTabClose = delegate { };
        public ICommand CloseTabCommand => new CommandHandler(CloseTab);

        private void CloseTab(object obj)
        {
            RequestTabClose(this);
        }
        public Guid HistoryID { get; set; }
        public bool IsHistoricalTab { get; set; }
        public string HeaderText { get; set; }
        public UserControl TabContent { get; set; }
    }
}
