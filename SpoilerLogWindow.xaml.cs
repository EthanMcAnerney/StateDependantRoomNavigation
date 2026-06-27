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
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;

namespace RoomNavigationAlgorithm
{
    public partial class SpoilerLogWindow : Window
    {
        private string _route100;
        private string _routeSteps;
        private string _routeLocks;
        private string _routeExplorable;


        public SpoilerLogWindow(string logContent)
        {
            InitializeComponent();
            TabsPanel.Visibility = Visibility.Collapsed;
            LogTextBox.Text = logContent;
        }

        public SpoilerLogWindow(string route100, string routeSteps, string routeLocks, string routeExplorable)
        {
            InitializeComponent();
            _routeExplorable = routeExplorable;
            _route100 = route100;
            _routeSteps = routeSteps;
            _routeLocks = routeLocks;


            LogTextBox.Text = _routeExplorable;
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            Button clicked = sender as Button;
            if (clicked == null) return;

            var dark = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333333"));//dark gray
            BtnRoute100.Background = dark;
            BtnRouteSteps.Background = dark;
            BtnRouteLocks.Background = dark;
            BtnRouteExplorable.Background = dark;

            clicked.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#673AB7"));//purple actiuve tab

            if (clicked == BtnRoute100) LogTextBox.Text = _route100;
            else if (clicked == BtnRouteExplorable) LogTextBox.Text = _routeExplorable;
            else if (clicked == BtnRouteSteps) LogTextBox.Text = _routeSteps;
            else if (clicked == BtnRouteLocks) LogTextBox.Text = _routeLocks;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Save Spoiler Log",
                FileName = "SpoilerLog.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, LogTextBox.Text);
                MessageBox.Show("Spoiler Log saved successfully!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
