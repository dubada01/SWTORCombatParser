using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Home_View_Models;


namespace SWTORCombatParser.Views.Home_Views
{
    /// <summary>
    /// Interaction logic for ParticipantInstanceView.xaml
    /// </summary>
    public partial class ParticipantInstanceView : UserControl
    {
        public ParticipantInstanceView()
        {
            InitializeComponent();
        }

        private void SelectParticipant(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is ParticipantViewModel participant && !participant.IsSelected) participant.ToggleSelection();
        }
    }
}
