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
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
            ContextMenu.Opened += DashboardMenu_Opened;
            PreviewMouseDown += (sender, args) => PrepareDropdown(args.OriginalSource as DependencyObject);
            PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.F4 || args.Key == Key.Down) PrepareDropdown(Keyboard.FocusedElement as DependencyObject);
            };
            Unloaded += (sender, args) => { CloseQuickPageDeletion(); CloseAppearance(); ClosePages(); CloseProfiles(); WatchPageModel(false); WatchNotificationClicks(false); };
            NotificationGrid.IsVisibleChanged += (sender, args) => WatchNotificationClicks(NotificationGrid.IsVisible);
            Loaded += (sender, args) =>
            {
                insetWindow = Window.GetWindow(this);
                insetToolbar = Controls.NativeToolbarCatalog.FindNativeTopPanel(insetWindow);
                if (insetToolbar != null && Window.GetWindow(insetToolbar) != insetWindow) insetToolbar = null;
                UpdateDashboardTopInset();
                WatchPageModel(true);
            };
            LayoutUpdated += (sender, args) => { UpdateDashboardTopInset(); PositionNotifications(); };
            DataContextChanged += (sender, args) => WatchPageModel(true);
            HookStartupIntro();
            HookStartupAnimation();
        }

        private Window insetWindow;
        private FrameworkElement insetToolbar;
        private void UpdateDashboardTopInset()
        {
            if (!IsLoaded || insetWindow == null || PresentationSource.FromVisual(this) == null) return;
            var top = TranslatePoint(new Point(), insetWindow).Y;
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(insetWindow);
            // Hidden-title-bar themes still reveal their caption controls on hover.
            var protectedBottom = chrome != null ? Math.Max(32, chrome.CaptionHeight) : 0;
            if (insetToolbar != null && insetToolbar.IsVisible && insetToolbar.ActualHeight > 0 && insetToolbar.ActualHeight <= 160)
            {
                var toolbarTop = insetToolbar.TranslatePoint(new Point(), insetWindow).Y;
                protectedBottom = Math.Max(protectedBottom, toolbarTop + insetToolbar.ActualHeight + 4);
            }
            var inset = Math.Max(0, Math.Min(160, protectedBottom - top));
            if (Math.Abs(DashboardSurface.Margin.Top - inset) < 0.5) return;
            DashboardSurface.Margin = new Thickness(0, inset, 0, 0);
            ExtensionDiagnostics.Record(LandingPageExtension.Instance,
                $"Dashboard top clearance: inset={inset:0.#}; dashboardTop={top:0.#}; protectedBottom={protectedBottom:0.#}.");
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ScreenPoint { public int X; public int Y; }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out ScreenPoint point);

        private void Dashboard_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (e.Handled || ContextMenu == null) return;
            if (e.CursorLeft >= 0 && e.CursorTop >= 0 && GetCursorPos(out var cursor))
                PositionDashboardContextMenuScreen(new Point(cursor.X, cursor.Y));
            else PositionDashboardContextMenu(new Point(24, 24));
        }

        private void PositionDashboardContextMenu(Point point)
            => PositionDashboardContextMenuScreen(PresentationSource.FromVisual(this) == null ? point : PointToScreen(point));

        private void PositionDashboardContextMenuScreen(Point screen)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var point = new Point(screen.X / dpi.DpiScaleX, screen.Y / dpi.DpiScaleY);
            var placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
            ContextMenuService.SetPlacement(this, placement);
            ContextMenuService.SetPlacementTarget(this, this);
            ContextMenuService.SetHorizontalOffset(this, point.X);
            ContextMenuService.SetVerticalOffset(this, point.Y);
            ContextMenu.Placement = placement;
            ContextMenu.PlacementTarget = this;
            ContextMenu.PlacementRectangle = Rect.Empty;
            ContextMenu.HorizontalOffset = point.X;
            ContextMenu.VerticalOffset = point.Y;
            var window = Window.GetWindow(this);
            if (window != null)
            {
                ContextMenu.MaxWidth = Math.Max(160, window.ActualWidth - 16);
                ContextMenu.MaxHeight = Math.Max(100, window.ActualHeight - 16);
            }
        }

        private void DashboardMenu_Opened(object sender, RoutedEventArgs args)
        {
            var window = Window.GetWindow(this);
            if (window == null) return;
            var dpi = VisualTreeHelper.GetDpi(window);
            var origin = window.PointToScreen(new Point());
            var left = origin.X / dpi.DpiScaleX + 8;
            var top = origin.Y / dpi.DpiScaleY + 8;
            ContextMenu.HorizontalOffset = Math.Max(left, Math.Min(ContextMenu.HorizontalOffset, left + window.ActualWidth - 16 - ContextMenu.ActualWidth));
            ContextMenu.VerticalOffset = Math.Max(top, Math.Min(ContextMenu.VerticalOffset, top + window.ActualHeight - 16 - ContextMenu.ActualHeight));
        }

        private void PrepareDropdown(DependencyObject source)
        {
            while (source != null && source != this && !(source is ComboBox))
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            if (!(source is ComboBox box)) return;
            box.ApplyTemplate();
            box.UpdateLayout();
            if (PresentationSource.FromVisual(box) == null) return;
            if (!(box.Template?.FindName("PART_Popup", box) is System.Windows.Controls.Primitives.Popup popup)) return;
            popup.PlacementTarget = box;
            popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            popup.HorizontalOffset = 0;
            popup.VerticalOffset = 0;
            popup.PlacementRectangle = Rect.Empty;
            ExtensionDiagnostics.Record(LandingPageExtension.Instance,
                $"Dashboard dropdown anchored: bounds={box.ActualWidth:0.#}x{box.ActualHeight:0.#}; screen={box.PointToScreen(new Point())}.");
        }

        private ViewModels.StartPageViewModel observedPageModel;

        private void WatchPageModel(bool watch)
        {
            if (observedPageModel != null)
            {
                observedPageModel.PropertyChanged -= PageModelChanged;
                observedPageModel.PageTransitionStarting -= BeginDashboardTransition;
                observedPageModel.PageTransitionFinishing -= CompleteDashboardTransition;
                observedPageModel.PageLayoutReady -= ArrangeDashboardPage;
                observedPageModel.QuickPageDeletionRequested -= ShowQuickPageDeletion;
            }
            ResetDashboardTransition();
            observedPageModel = watch ? DataContext as ViewModels.StartPageViewModel : null;
            if (observedPageModel != null)
            {
                observedPageModel.PropertyChanged += PageModelChanged;
                observedPageModel.PageTransitionStarting += BeginDashboardTransition;
                observedPageModel.PageTransitionFinishing += CompleteDashboardTransition;
                observedPageModel.PageLayoutReady += ArrangeDashboardPage;
                observedPageModel.QuickPageDeletionRequested += ShowQuickPageDeletion;
            }
            WatchPageTransitions(observedPageModel);
        }

        private Models.DashboardPage quickDeletePage;
        private void ShowQuickPageDeletion(Models.DashboardPage page)
        {
            quickDeletePage = page;
            QuickDeletePageText.Text = $"Delete dashboard page '{page.Name}' and its widgets?";
            QuickDeletePageOverlay.Visibility = Visibility.Visible;
            QuickDeletePageOverlay.Focus();
        }
        private void CloseQuickPageDeletion()
        {
            quickDeletePage = null;
            QuickDeletePageOverlay.Visibility = Visibility.Collapsed;
        }
        private void CancelQuickPageDeletion_Click(object sender, RoutedEventArgs args) => CloseQuickPageDeletion();
        private async void ConfirmQuickPageDeletion_Click(object sender, RoutedEventArgs args)
        {
            if (!(DataContext is ViewModels.StartPageViewModel model) || quickDeletePage == null) return;
            QuickDeletePageOverlay.IsEnabled = false;
            try { await model.RemovePageQuickAsync(quickDeletePage); CloseQuickPageDeletion(); }
            catch (Exception ex)
            {
                LandingPageExtension.logger.Error(ex, "Unable to quick-remove a dashboard page.");
                LandingPageExtension.Instance?.PlayniteApi.Dialogs?.ShowErrorMessage("The page could not be removed. Please check the debug log.", "Dashboard pages");
            }
            finally { QuickDeletePageOverlay.IsEnabled = true; }
        }
        private void QuickDeletePageOverlay_PreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape) { CloseQuickPageDeletion(); args.Handled = true; }
        }
        private void QuickDeletePageOverlay_MouseDown(object sender, MouseButtonEventArgs args)
        {
            if (args.OriginalSource == QuickDeletePageOverlay) { CloseQuickPageDeletion(); args.Handled = true; }
        }

        private async Task ArrangeDashboardPage()
        {
            await Dispatcher.Yield(DispatcherPriority.Loaded);
            if (!IsLoaded) return;
            var root = StartPageScrollViewer.Content as GridNodeView;
            root?.RefreshContentLayout();
            StartPageScrollViewer.InvalidateMeasure();
            StartPageScrollViewer.InvalidateArrange();
            StartPageScrollViewer.UpdateLayout();
            var screen = root != null && PresentationSource.FromVisual(root) != null ? root.PointToScreen(new Point()).ToString() : "detached";
            ExtensionDiagnostics.Record(LandingPageExtension.Instance,
                $"Dashboard arranged: page={observedPageModel?.SelectedPage?.Id}; root={root?.ActualWidth:0.#}x{root?.ActualHeight:0.#}; screen={screen}.");
        }

        internal Func<bool> DashboardAnimationsEnabled = () => SystemParameters.ClientAreaAnimation;
        private TaskCompletionSource<bool> transitionCompletion;
        private int transitionGeneration;
        // Plays the effects and the GIF/video between the old-page snapshot and the new page (shared with the editor preview).
        private Controls.TransitionPlayer transitionPlayer;
        internal Controls.TransitionPlayer Player => transitionPlayer ?? (transitionPlayer = new Controls.TransitionPlayer(DashboardTransitionImage, DashboardScene, DashboardTransitionMedia));

        private void ResetDashboardTransition()
        {
            ++transitionGeneration;
            Player.Reset();
            DashboardTransitionImage.Source = null;
            DashboardTransitionImage.Visibility = Visibility.Collapsed;
            DashboardScene.IsHitTestVisible = true;
            var completion = transitionCompletion; transitionCompletion = null;
            completion?.TrySetResult(true);
        }

        private void BeginDashboardTransition(object sender, ViewModels.DashboardTransitionEventArgs args)
        {
            CancelStartupAnimation();
            ResetDashboardTransition();
            var settings = observedPageModel?.Settings.Settings;
            if (!IsLoaded || settings == null) return;
            // The destination page's own transition when it has one, else the global transition (previews pass the edited one).
            var transition = args.Transition ?? Models.DashboardPages.EffectiveTransition(settings, args.Destination);
            // A GIF/video can play during the switch even when the transition effect is None.
            var media = transition.Media;
            // The destination page's own intro takes priority over the transition media.
            var pageIntroDue = args.Destination != null && args.Destination.Intro.Enabled && PageIntroDue(args.Destination);
            var mediaFile = media.Enabled && !pageIntroDue ? media.ResolveMedia() : null;
            if ((transition.Transition == DashboardTransition.None && mediaFile == null)
                || (settings.DashboardRespectWindowsAnimations && !DashboardAnimationsEnabled()) || DashboardScene.ActualWidth < 1 || DashboardScene.ActualHeight < 1) return;
            try
            {
                var bounds = new Rect(0, 0, DashboardScene.ActualWidth, DashboardScene.ActualHeight);
                var drawing = new DrawingVisual();
                using (var context = drawing.RenderOpen())
                {
                    // Dashboard panels/artwork may be translucent. Flatten them onto
                    // an opaque theme surface so the incoming page cannot bleed through
                    // while its widgets are still preparing beneath the snapshot.
                    context.DrawRectangle(Brushes.Black, null, bounds);
                    var surfaceBrush = TryFindResource("WindowBackgroundBrush") as Brush ?? Window.GetWindow(this)?.Background;
                    if (surfaceBrush != null) context.DrawRectangle(surfaceBrush, null, bounds);
                    // The global background is outside StartPage's visual subtree.
                    // Include its current viewport so artwork and widgets transition together.
                    var global = LandingPageExtension.Instance?.GlobalBackgroundView;
                    if (settings.EnableGlobalBackground && global?.IsVisible == true)
                    {
                        var viewport = DashboardScene.TransformToVisual(global).TransformBounds(bounds);
                        context.DrawRectangle(new VisualBrush(global) { ViewboxUnits = BrushMappingMode.Absolute,
                            Viewbox = viewport, Stretch = Stretch.Fill }, null, bounds);
                    }
                    context.DrawRectangle(new VisualBrush(DashboardScene), null, bounds);
                }
                var scale = Math.Min(1, 2048 / Math.Max(bounds.Width, bounds.Height));
                var bitmap = new RenderTargetBitmap(Math.Max(1, (int)(bounds.Width * scale)), Math.Max(1, (int)(bounds.Height * scale)),
                    96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(drawing); bitmap.Freeze();
                Player.Configure(transition, args.Forward, mediaFile);
                DashboardTransitionImage.Source = bitmap;
                DashboardTransitionImage.Visibility = Visibility.Visible;
                DashboardScene.IsHitTestVisible = false;
            }
            catch (Exception ex)
            {
                LandingPageExtension.logger?.Debug(ex, "Unable to capture dashboard transition; switching without animation.");
                ResetDashboardTransition();
            }
        }

        private async Task CompleteDashboardTransition()
        {
            if (DashboardTransitionImage.Source == null) return;
            var generation = transitionGeneration;
            var presentationWatch = System.Diagnostics.Stopwatch.StartNew();
            // Let layout/Loaded work run without waiting for the dispatcher to become idle.
            await Dispatcher.Yield(DispatcherPriority.Loaded);
            ExtensionDiagnostics.Record(LandingPageExtension.Instance, $"Transition presentation wait: {presentationWatch.ElapsedMilliseconds} ms.");
            if (generation != transitionGeneration) return;
            if (!IsLoaded || DashboardTransitionImage.Source == null) { ResetDashboardTransition(); return; }
            transitionCompletion = new TaskCompletionSource<bool>();
            var aborted = transitionCompletion.Task;
            try { await Player.PlayAsync(aborted, () => generation != transitionGeneration); }
            catch (Exception ex) { LandingPageExtension.logger?.Debug(ex, "Dashboard transition failed; showing the page."); }
            // A newer switch (or leaving StartPage) already reset everything and owns the scene.
            if (generation == transitionGeneration) ResetDashboardTransition();
        }

        private void PageModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewModels.StartPageViewModel.RootNodeViewModel)) return;
            if (AppearanceOverlay.Visibility == Visibility.Visible) CloseAppearance();
            StartPageScrollViewer.ScrollToTop();
        }

        private void ManagePages_Click(object sender, RoutedEventArgs e) => ShowPages();

        public void BeginChartEditing()
        {
            if(DataContext is ViewModels.StartPageViewModel model && !model.RootNodeViewModel.EditModeEnabled) model.EnterEditModeCommand.Execute(null);
            CloseAppearance();
        }
        private bool pagesClosing;
        public void ShowPages()
        {
            if (pagesClosing || !(DataContext is ViewModels.StartPageViewModel model) || model.IsSwitchingPage) return;
            if (AppearanceOverlay.Visibility == Visibility.Visible) CloseAppearance();
            model.BeginPageEditing();
            UpdateTransitionEditor();
            PagesOverlay.Visibility = Visibility.Visible;
            PagesOverlay.Focus();
        }

        private async void ClosePages(bool save = false)
        {
            if (PagesOverlay.Visibility != Visibility.Visible || pagesClosing) return;
            pagesClosing = true;
            PagesOverlay.Focus();
            PagesOverlay.IsEnabled = false;
            LandingPageExtension.logger.Info($"Dashboard page window closing: save={save}; loaded={IsLoaded}.");
            try
            {
                if (DataContext is ViewModels.StartPageViewModel model)
                {
                    model.ConfirmPageDeletion = false;
                    while (model.IsSwitchingPage) await Task.Delay(16);
                    await model.EndPageEditingAsync(save);
                }
                PagesOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LandingPageExtension.logger.Error(ex,"Unable to finish dashboard page editing.");
                LandingPageExtension.Instance?.PlayniteApi.Dialogs.ShowErrorMessage("The dashboard pages could not be saved. Your pending changes are still open. Please try Done again and check the debug log.", "Dashboard pages");
            }
            finally { PagesOverlay.IsEnabled = true; pagesClosing = false; }
        }

        private void ClosePages_Click(object sender, RoutedEventArgs e) => ClosePages();
        private void SavePages_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            LandingPageExtension.logger.Info("Dashboard page Done button clicked.");
            ClosePages(true);
        }
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
            model.RefreshStarterProfiles();
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
        private void PagesToDelete_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.StartPageViewModel model && sender is ListBox list)
                model.SetPagesSelectedForDeletion(list.SelectedItems.Cast<Models.DashboardPage>().ToArray());
        }
        private void SavedProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is LandingPage.ViewModels.StartPageViewModel model && sender is ListBox list)
                model.SetSelectedProfiles(list.SelectedItems.Cast<LandingPage.Models.DashboardProfile>().ToArray());
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

        internal void BeginFeaturedSectionEditing(LandingPage.Settings.FeaturedGameSettings settings)
        {
            if (DataContext is ViewModels.StartPageViewModel model) model.RootNodeViewModel.EditModeEnabled = true;
            settings.EditSectionLayout = true;
            CloseAppearance();
            FindFeaturedSectionEditor(this,settings)?.FocusSectionEditor();
        }
        private static FeaturedGameView FindFeaturedSectionEditor(DependencyObject root, LandingPage.Settings.FeaturedGameSettings settings)
        {
            if (root is FeaturedGameView featured && ReferenceEquals(featured.Settings,settings)) return featured;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
            {
                var found = FindFeaturedSectionEditor(VisualTreeHelper.GetChild(root,i),settings);
                if (found != null) return found;
            }
            return null;
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

        private readonly List<WeakReference> notificationAnchors = new List<WeakReference>();
        private WeakReference notificationAnchor;

        internal void RegisterNotificationAnchor(FrameworkElement anchor)
        {
            notificationAnchors.RemoveAll(reference => !(reference.Target is FrameworkElement element) || !element.IsLoaded || ReferenceEquals(element, anchor));
            notificationAnchors.Add(new WeakReference(anchor));
            PositionNotifications();
        }

        internal void ToggleNotificationsAt(FrameworkElement anchor)
        {
            notificationAnchor = new WeakReference(anchor);
            PositionNotifications();
            (DataContext as ViewModels.StartPageViewModel)?.ToggleNotificationsCommand.Execute(null);
        }

        private void PositionNotifications()
        {
            if (!IsLoaded || NotificationGrid.Visibility != Visibility.Visible) return;
            var anchor = notificationAnchor?.Target as FrameworkElement;
            if (anchor == null || !anchor.IsLoaded || !anchor.IsVisible)
                anchor = notificationAnchors.Select(reference => reference.Target as FrameworkElement)
                    .FirstOrDefault(element => element?.IsLoaded == true && element.IsVisible);
            if (anchor == null) return;
            try
            {
                var point = anchor.TranslatePoint(new Point(0, anchor.ActualHeight), this);
                const double edgePadding = 8;
                var width = Math.Min(380, Math.Max(0, ActualWidth - 2 * edgePadding));
                var centeredX = point.X + anchor.ActualWidth / 2 - width / 2;
                var x = Math.Max(edgePadding, Math.Min(centeredX, ActualWidth - width - edgePadding));
                var y = Math.Max(0, point.Y + 6);
                var margin = new Thickness(x, y, 0, 0);
                if (NotificationGrid.Margin != margin) NotificationGrid.Margin = margin;
                if (NotificationGrid.Width != width) NotificationGrid.Width = width;
                var height = Math.Max(0, ActualHeight - y - 8);
                if (NotificationGrid.MaxHeight != height) NotificationGrid.MaxHeight = height;
                var listHeight = Math.Min(300, Math.Max(0, height - 130));
                if (NotificationItemsControl.MaxHeight != listHeight) NotificationItemsControl.MaxHeight = listHeight;
            }
            catch (InvalidOperationException) { } // Anchor is detaching during a page change.
        }

        private void ToggleNotifications_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ViewModels.StartPageViewModel)?.ToggleNotificationsCommand.Execute(null);
        }

        // The notifications flyout behaves like a flyout: a click anywhere outside it (and outside the buttons that
        // toggle it, which toggle it themselves) closes it.
        private Window notificationClickWindow;
        private MouseButtonEventHandler notificationClickHandler;

        private void WatchNotificationClicks(bool watch)
        {
            if (notificationClickWindow != null && notificationClickHandler != null)
                notificationClickWindow.RemoveHandler(PreviewMouseDownEvent, notificationClickHandler);
            notificationClickWindow = null;
            if (!watch) return;
            notificationClickWindow = Window.GetWindow(this);
            if (notificationClickWindow == null) return;
            if (notificationClickHandler == null) notificationClickHandler = NotificationWindowMouseDown;
            notificationClickWindow.AddHandler(PreviewMouseDownEvent, notificationClickHandler, true);
        }

        private void NotificationWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            if (IsWithin(source, NotificationGrid)) return;
            if (notificationAnchors.Any(reference => IsWithin(source, reference.Target as DependencyObject))) return;
            if (IsWithin(source, notificationAnchor?.Target as DependencyObject)) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (DataContext is ViewModels.StartPageViewModel model && model.NotificationsOpen) model.NotificationsOpen = false;
            }));
        }

        private static bool IsWithin(DependencyObject node, DependencyObject container)
        {
            if (node == null || container == null) return false;
            for (var current = node; current != null; current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
                if (ReferenceEquals(current, container)) return true;
            return false;
        }
    }
}
