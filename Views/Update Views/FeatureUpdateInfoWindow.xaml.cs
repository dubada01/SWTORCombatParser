﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Shapes;

namespace SWTORCombatParser.Views
{

    /// <summary>
    /// Interaction logic for FeatureUpdateInfoWindow.xaml
    /// </summary>
    public partial class FeatureUpdateInfoWindow : Window
    {
        public FeatureUpdateInfoWindow()
        {
            InitializeComponent();
        }
        private void Close(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}