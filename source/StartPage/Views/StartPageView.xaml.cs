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
            Unloaded += (sender, args) => { CloseAppearance(); ClosePages(); CloseProfiles(); WatchPageModel(false); };
            Loaded += (sender, args) => WatchPageModel(true);
            DataContextChanged += (sender, args) => WatchPageModel(true);
        }

        private ViewModels.StartPageViewModel observedPageModel;

        private void WatchPageModel(bool watch)
        {
            if (observedPageModel != null) observedPageModel.PropertyChanged -= PageModelChanged;
            observedPageModel = watch ? DataContext as ViewModels.StartPageViewModel : null;
            if (observedPageModel != null) observedPageModel.PropertyChanged += PageModelChanged;
        }

        private void PageModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewModels.StartPageViewModel.RootNodeViewModel)) return;
            if (AppearanceOverlay.Visibility == Visibility.Visible) CloseAppearance();
            StartPageScrollViewer.ScrollToTop();
        }

        private void ManagePages_Click(object sender, RoutedEventArgs e) => ShowPages();

        public void ShowPages()
        {
            if (!(DataContext is ViewModels.StartPageViewModel model) || model.IsSwitchingPage) return;
            if (AppearanceOverlay.Visibility == Visibility.Visible) CloseAppearance();
            PagesOverlay.Visibility = Visibility.Visible;
            PagesOverlay.Focus();
        }

        private void ClosePages()
        {
            if (PagesOverlay.Visibility != Visibility.Visible) return;
            PagesOverlay.Focus(); // Commit any final name/path edit before saving.
            PagesOverlay.Visibility = Visibility.Collapsed;
            if (DataContext is ViewModels.StartPageViewModel model)
            {
                model.ConfirmPageDeletion = false;
                if (model.CanChangePages) model.SaveCurrentPage();
            }
        }

        private void ClosePages_Click(object sender, RoutedEventArgs e) => ClosePages();
        private void PagesOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { ClosePages(); e.Handled = true; }
        }
        private void PagesOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == PagesOverlay) { ClosePages(); e.Handled = true; }
        }
        private void ManageProfiles_Click(object sender, RoutedEventArgs e) => ShowProfiles();
        public void ShowProfiles()
        {
            if (!(DataContext is ViewModels.StartPageViewModel model) || model.IsSwitchingPage) return;
            if (AppearanceOverlay.Visibility == Visibility.Visible) CloseAppearance();
            ClosePages();
            ProfilesOverlay.Visibility = Visibility.Visible;
            ProfilesOverlay.Focus();
        }
        private void CloseProfiles()
        {
            if (ProfilesOverlay.Visibility != Visibility.Visible) return;
            ProfilesOverlay.Focus();
            ProfilesOverlay.Visibility = Visibility.Collapsed;
            if (DataContext is ViewModels.StartPageViewModel model)
            {
                model.ConfirmDeleteProfile = false;
                if (model.CanChangePages) model.SaveCurrentPage();
            }
        }
        private void CloseProfiles_Click(object sender, RoutedEventArgs e) => CloseProfiles();
        private void ProfilesOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { CloseProfiles(); e.Handled = true; }
        }
        private void ProfilesOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == ProfilesOverlay) { CloseProfiles(); e.Handled = true; }
        }

        private void PageBackgroundPath_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox box) box.Tag = box.Text;
        }
        private void PageBackgroundPath_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox box) || string.Equals(box.Tag as string, box.Text, StringComparison.Ordinal) || !(DataContext is ViewModels.StartPageViewModel model)) return;
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            box.Tag = box.Text;
            model.SetPageImage(box.Text);
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
