using SWTORCombatParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SWTORCombatParser
{
    /// <summary>
    /// Interaction logic for CombatMetaDataView.xaml
    /// </summary>
    public partial class CombatMetaDataView : UserControl
    {
        public CombatMetaDataView(CombatMetaDataViewModel dataContext)
        {
            DataContext = dataContext;
            InitializeComponent();
        }
    }
}
