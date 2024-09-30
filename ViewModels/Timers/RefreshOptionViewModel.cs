using System;
using System.Reactive;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class RefreshOptionViewModel:ReactiveObject
    {
        public event Action<RefreshOptionViewModel> RemoveRequested = delegate { };
        public string Name { get; set; }
        public ReactiveCommand<Unit,Unit> RemoveCommand => ReactiveCommand.Create(Remove);

        private void Remove()
        {
            RemoveRequested(this);
        }
    }
}
