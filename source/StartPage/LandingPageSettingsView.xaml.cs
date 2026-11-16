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

namespace LandingPage
{
    public partial class LandingPageSettingsView : UserControl
    {
        private void ImportOriginalSettings_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as LandingPageSettingsViewModel)?.ImportOriginalSettings();
        }

        private void DebugLogging_Click(object sender, RoutedEventArgs e)
        {
            LandingPageExtension.logger.Info("StartPage debug logging preference changed.");
        }
        private void OpenDebugLogFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is LandingPageSettingsViewModel model)) return;
            try
            {
                var folder = System.IO.Path.GetDirectoryName(model.DebugLogPath);
                System.IO.Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception ex) { LandingPageExtension.logger.Error(ex, "Unable to open StartPage debug log folder."); }
        }

        public LandingPageSettingsView()
        {
            InitializeComponent();
        }
    }
}