using SWTORCombatParser.ViewModels.Overlays.Room;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.Overlay.Room
{
    /// <summary>
    /// Interaction logic for RoomSetup.xaml
    /// </summary>
    public partial class RoomSetup : UserControl
    {
        public RoomSetup(RoomOverlayViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
