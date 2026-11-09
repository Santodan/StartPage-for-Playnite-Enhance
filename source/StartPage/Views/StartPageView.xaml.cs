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

namespace LandingPage.Views
{
    /// <summary>
    /// Interaktionslogik für StartPageView.xaml
    /// </summary>
    public partial class StartPageView : UserControl
    {
        public StartPageView()
        {
            InitializeComponent();
            Unloaded += (sender, args) => CloseAppearance();
        }

        private IInputElement appearancePreviousFocus;

        internal void ShowAppearance(ViewModels.Layout.GridNodeViewModel model)
        {
            appearancePreviousFocus = Keyboard.FocusedElement;
            AppearanceEditor.DataContext = model;
            AppearanceOverlay.Visibility = Visibility.Visible;
            AppearanceOverlay.Focus();
        }

        private void CloseAppearance()
        {
            // Move focus first so the last edited color/text field commits its binding.
            AppearanceOverlay.Focus();
            AppearanceOverlay.Visibility = Visibility.Collapsed;
            AppearanceEditor.DataContext = null;
            if (appearancePreviousFocus != null) Keyboard.Focus(appearancePreviousFocus);
            appearancePreviousFocus = null;
        }

        private void CloseAppearance_Click(object sender, RoutedEventArgs e) => CloseAppearance();

        private void AppearanceOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseAppearance();
                e.Handled = true;
            }
        }

        private void AppearanceOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == AppearanceOverlay)
            {
                CloseAppearance();
                e.Handled = true;
            }
        }

        private void ToggleNotifications_Click(object sender, RoutedEventArgs e)
        {
            if (NotificationsBorder.Tag is Visibility vis)
            {
                if (vis == Visibility.Visible)
                {
                    NotificationsBorder.Tag = Visibility.Collapsed;
                }
                else
                {
                    NotificationsBorder.SetBinding(TagProperty, new Binding("Notifications") { Converter = (IValueConverter)Resources["IEnumerableNullOrEmptyToVisibilityConverter"], Mode = BindingMode.OneWay });
                }
            }
        }
    }
}
