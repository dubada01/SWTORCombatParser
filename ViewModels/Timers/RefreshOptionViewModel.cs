using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class RefreshOptionViewModel
    {
        public event Action<RefreshOptionViewModel> RemoveRequested = delegate { };
        public string Name { get; set; }
        public ICommand RemoveCommand => new CommandHandler(Remove);

        private void Remove(object obj)
        {
            RemoveRequested(this);
        }
    }
}
