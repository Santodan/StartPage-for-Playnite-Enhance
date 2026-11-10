using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace LandingPage.Views
{
    public partial class AdvancedShelfRulesView : UserControl
    {
        public const string PatternHelpUrl = "https://github.com/Santodan/StartPage-for-Playnite/blob/master/docs/SHELF-PATTERNS.md";

        public AdvancedShelfRulesView() { InitializeComponent(); }

        private void PatternHelp_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            try { Process.Start(new ProcessStartInfo(PatternHelpUrl) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                LandingPageExtension.logger.Error(ex, "Unable to open shelf pattern guide.");
                LandingPageExtension.Instance.PlayniteApi.Dialogs.ShowErrorMessage(
                    "Unable to open the shelf pattern guide.\n" + PatternHelpUrl, "Shelf patterns");
            }
        }
    }
}
