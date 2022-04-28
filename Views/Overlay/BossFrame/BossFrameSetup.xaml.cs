using SWTORCombatParser.ViewModels.Overlays.BossFrame;
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

namespace SWTORCombatParser.Views.Overlay
{
    /// <summary>
    /// Interaction logic for RaidHOTsSteup.xaml
    /// </summary>
    public partial class BossFrameSetup : UserControl
    {
        public BossFrameSetup(BossFrameConfigViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
