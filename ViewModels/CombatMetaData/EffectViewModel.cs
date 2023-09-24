using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.CombatMetaData
{
    public class EffectViewModel : INotifyPropertyChanged
    {
        private bool selected;

        public bool Selected
        {
            get => selected; set
            {
                selected = value;
                OnPropertyChanged();
            }
        }
        public string Name { get; set; }
        public string Source { get; set; }
        public double Duration { get; set; }
        public int Count { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
