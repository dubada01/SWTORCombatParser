using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays.AbilityList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace SWTORCombatParser.Views.Overlay.AbilityList
{
    /// <summary>
    /// Interaction logic for AbilityListView.xaml
    /// </summary>
    public partial class AbilityListView : BaseOverlayWindow
    {
        private AbilityListViewModel viewModel;
        public AbilityListView(AbilityListViewModel vm):base(vm)
        {
            viewModel = vm;
            DataContext = vm;
            InitializeComponent();
        }
    }
}
