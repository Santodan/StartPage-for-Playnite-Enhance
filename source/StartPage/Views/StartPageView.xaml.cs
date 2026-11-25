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
        private DashboardTransition activeTransition;
        private Models.DashboardTransitionPreset activePreset;
        private int transitionGeneration;
        private DashboardSlideDirection activeDirection;
        private int transitionMilliseconds;
        private bool transitionForward;

        private void ResetDashboardTransition()
        {
            ++transitionGeneration;
            DashboardScene.BeginAnimation(OpacityProperty, null);
            DashboardScene.Opacity = 1;
            DashboardTransitionImage.Clip = null;
            DashboardTransitionImage.BeginAnimation(OpacityProperty, null);
            DashboardTransitionImage.RenderTransform = Transform.Identity;
            DashboardTransitionImage.Opacity = 1;
            DashboardTransitionImage.Source = null;
            DashboardTransitionImage.Visibility = Visibility.Collapsed;
            DashboardScene.RenderTransform = Transform.Identity;
            DashboardScene.IsHitTestVisible = true;
            var completion = transitionCompletion; transitionCompletion = null;
            completion?.TrySetResult(true);
        }

        private void BeginDashboardTransition(object sender, ViewModels.DashboardTransitionEventArgs args)
        {
            CancelStartupAnimation();
            ResetDashboardTransition();
            var settings = observedPageModel?.Settings.Settings;
            if (!IsLoaded || settings == null || settings.DashboardTransition == DashboardTransition.None
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
                activeTransition = settings.DashboardTransition;
                activePreset = activeTransition == DashboardTransition.Custom ? Models.TransitionPresets.Clone(settings.DashboardCustomTransition) : null;
                activeDirection = settings.DashboardSlideDirection;
                transitionMilliseconds = settings.DashboardTransitionMilliseconds;
                transitionForward = args.Forward;
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
            var pending = transitionCompletion.Task;
            var duration = TimeSpan.FromMilliseconds(transitionMilliseconds);
            if (activeTransition == DashboardTransition.Wipe)
            {
                var direction = ResolveTransitionDirection();
                var width = DashboardScene.ActualWidth; var height = DashboardScene.ActualHeight;
                var end = direction == DashboardSlideDirection.Left ? new Rect(0,0,0,height)
                    : direction == DashboardSlideDirection.Right ? new Rect(width,0,0,height)
                    : direction == DashboardSlideDirection.Up ? new Rect(0,0,width,0) : new Rect(0,height,width,0);
                var clip = new RectangleGeometry(new Rect(0,0,width,height));
                DashboardTransitionImage.Clip = clip;
                var wipe = new RectAnimation(clip.Rect, end, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
                wipe.Completed += (sender, args) => { if (generation == transitionGeneration) ResetDashboardTransition(); };
                clip.BeginAnimation(RectangleGeometry.RectProperty, wipe);
            }
            else if (activeTransition == DashboardTransition.Custom || activeTransition == DashboardTransition.SlideFade
                || activeTransition == DashboardTransition.ZoomIn || activeTransition == DashboardTransition.ZoomOut)
            {
                var preset = activePreset ?? BuiltInMotionPreset();
                AnimateMotionPreset(preset, generation);
            }
            else if (activeTransition == DashboardTransition.Fade)
            {
                var fade = new DoubleAnimation(1, 0, duration) { FillBehavior = FillBehavior.Stop };
                fade.Completed += (sender, args) => ResetDashboardTransition();
                DashboardTransitionImage.BeginAnimation(OpacityProperty, fade);
            }
            else
            {
                var direction = activeDirection == DashboardSlideDirection.Automatic
                    ? (transitionForward ? DashboardSlideDirection.Left : DashboardSlideDirection.Right) : activeDirection;
                var vertical = direction == DashboardSlideDirection.Up || direction == DashboardSlideDirection.Down;
                var offset = (vertical ? DashboardScene.ActualHeight : DashboardScene.ActualWidth)
                    * (direction == DashboardSlideDirection.Right || direction == DashboardSlideDirection.Down ? 1 : -1);
                var outgoing = new TranslateTransform(); var incoming = new TranslateTransform();
                DashboardTransitionImage.RenderTransform = outgoing; DashboardScene.RenderTransform = incoming;
                var property = vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty;
                var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
                var leave = new DoubleAnimation(0, offset, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
                var enter = new DoubleAnimation(-offset, 0, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
                enter.Completed += (sender, args) => ResetDashboardTransition();
                outgoing.BeginAnimation(property, leave); incoming.BeginAnimation(property, enter);
            }
            await pending;
        }

        private DashboardSlideDirection ResolveTransitionDirection() => activeDirection == DashboardSlideDirection.Automatic
            ? (transitionForward ? DashboardSlideDirection.Left : DashboardSlideDirection.Right) : activeDirection;

        private Models.DashboardTransitionPreset BuiltInMotionPreset()
        {
            var preset = new Models.DashboardTransitionPreset { DurationMs = transitionMilliseconds, ReverseOnPrevious = false,
                Incoming = new Models.TransitionMotion(), Outgoing = new Models.TransitionMotion { EndOpacity = 0 } };
            if (activeTransition == DashboardTransition.ZoomIn) preset.Incoming.StartScale = 0.90;
            else if (activeTransition == DashboardTransition.ZoomOut) preset.Outgoing.EndScale = 1.10;
            else
            {
                var direction = ResolveTransitionDirection();
                var offset = direction == DashboardSlideDirection.Left || direction == DashboardSlideDirection.Up ? -15 : 15;
                if (direction == DashboardSlideDirection.Up || direction == DashboardSlideDirection.Down)
                { preset.Outgoing.EndY = offset; preset.Incoming.StartY = -offset; }
                else { preset.Outgoing.EndX = offset; preset.Incoming.StartX = -offset; }
                preset.Incoming.StartOpacity = 0;
            }
            return preset;
        }

        private void AnimateMotionPreset(Models.DashboardTransitionPreset preset, int generation)
        {
            Models.TransitionPresets.Validate(preset);
            var sign = preset.ReverseOnPrevious && !transitionForward ? -1 : 1;
            IEasingFunction easing = null;
            if (preset.Easing == Models.TransitionEasing.Quadratic) easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
            if (preset.Easing == Models.TransitionEasing.Cubic) easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
            if (preset.Easing == Models.TransitionEasing.Sine) easing = new SineEase { EasingMode = EasingMode.EaseInOut };
            Action<FrameworkElement, Models.TransitionMotion, bool> animate = (element, motion, completes) =>
            {
                var scale = new ScaleTransform(); var translation = new TranslateTransform();
                var transforms = new TransformGroup(); transforms.Children.Add(scale); transforms.Children.Add(translation);
                element.RenderTransformOrigin = new Point(0.5,0.5); element.RenderTransform = transforms;
                Func<double,double,DoubleAnimation> make = (from,to) => new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(preset.DurationMs))
                    { BeginTime = TimeSpan.FromMilliseconds(motion.DelayMs), EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };
                var opacity = make(motion.StartOpacity,motion.EndOpacity);
                // Explicit start values also cover the delay before an animation starts.
                element.Opacity = motion.StartOpacity;
                scale.ScaleX = scale.ScaleY = motion.StartScale;
                translation.X = sign * motion.StartX * DashboardScene.ActualWidth / 100;
                translation.Y = sign * motion.StartY * DashboardScene.ActualHeight / 100;
                if (completes) opacity.Completed += (sender,args) => { if (generation == transitionGeneration) ResetDashboardTransition(); };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,make(motion.StartScale,motion.EndScale));
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,make(motion.StartScale,motion.EndScale));
                translation.BeginAnimation(TranslateTransform.XProperty,make(translation.X,sign * motion.EndX * DashboardScene.ActualWidth / 100));
                translation.BeginAnimation(TranslateTransform.YProperty,make(translation.Y,sign * motion.EndY * DashboardScene.ActualHeight / 100));
                element.BeginAnimation(OpacityProperty,opacity);
            };
            var outgoingLast = preset.Outgoing.DelayMs > preset.Incoming.DelayMs;
            animate(DashboardTransitionImage,preset.Outgoing,outgoingLast);
            animate(DashboardScene,preset.Incoming,!outgoingLast);
        }

        private void TransitionAction(Action action)
        {
            try { action(); }
            catch (Exception ex) { TransitionPresetStatus.Text = ex.Message; LandingPageExtension.logger.Warn(ex,"Transition preset operation failed."); }
        }
        private async void PreviewTransition_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is ViewModels.StartPageViewModel model) || !model.CanChangePages || transitionCompletion != null) return;
            try
            {
                if (model.Settings.Settings.DashboardTransition == DashboardTransition.Custom) Models.TransitionPresets.Validate(model.Settings.Settings.DashboardCustomTransition);
                BeginDashboardTransition(this,new ViewModels.DashboardTransitionEventArgs { Forward = true });
                if (DashboardTransitionImage.Source == null) { TransitionPresetStatus.Text = "Choose a transition and enable animations to preview."; return; }
                await CompleteDashboardTransition();
                TransitionPresetStatus.Text = "Preview complete. Switch pages to test with different content.";
            }
            catch (Exception ex) { ResetDashboardTransition(); TransitionPresetStatus.Text = ex.Message; }
        }
        private void SaveTransitionPreset_Click(object sender, RoutedEventArgs e) => TransitionAction(() =>
        {
            var settings = ((ViewModels.StartPageViewModel)DataContext).Settings.Settings;
            var copy = Models.TransitionPresets.Clone(settings.DashboardCustomTransition);
            var name = copy.Name;
            for (var suffix = 2; settings.DashboardTransitionPresets.Any(item => item.Name == copy.Name); ++suffix) copy.Name = name + " (" + suffix + ")";
            settings.DashboardTransitionPresets.Add(copy); TransitionPresetPicker.SelectedItem = copy;
            TransitionPresetStatus.Text = "Saved an independent preset: " + copy.Name;
            ((ViewModels.StartPageViewModel)DataContext).SaveCurrentPage();
        });
        private void LoadTransitionPreset_Click(object sender, RoutedEventArgs e) => TransitionAction(() =>
        {
            if (!(TransitionPresetPicker.SelectedItem is Models.DashboardTransitionPreset selected)) throw new ArgumentException("Select a saved preset.");
            var settings = ((ViewModels.StartPageViewModel)DataContext).Settings.Settings;
            settings.DashboardCustomTransition = Models.TransitionPresets.Clone(selected); settings.DashboardTransition = DashboardTransition.Custom;
            TransitionPresetStatus.Text = "Loaded " + selected.Name;
        });
        private void DeleteTransitionPreset_Click(object sender, RoutedEventArgs e) => TransitionAction(() =>
        {
            if (TransitionPresetPicker.SelectedItem is Models.DashboardTransitionPreset selected)
            {
                ((ViewModels.StartPageViewModel)DataContext).Settings.Settings.DashboardTransitionPresets.Remove(selected);
                ((ViewModels.StartPageViewModel)DataContext).SaveCurrentPage();
                TransitionPresetStatus.Text = "Deleted saved preset. Current transition is unchanged.";
            }
        });
        private void ExportTransitionPreset_Click(object sender, RoutedEventArgs e) => TransitionAction(() =>
        {
            var json = Models.TransitionPresets.Export(((ViewModels.StartPageViewModel)DataContext).Settings.Settings.DashboardCustomTransition);
            var path = LandingPageExtension.Instance.PlayniteApi.Dialogs.SaveFile("Transition preset|*.json");
            if (!string.IsNullOrWhiteSpace(path)) { System.IO.File.WriteAllText(path,json); TransitionPresetStatus.Text = "Exported " + path; }
        });
        private void ImportTransitionPreset_Click(object sender, RoutedEventArgs e) => TransitionAction(() =>
        {
            var path = LandingPageExtension.Instance.PlayniteApi.Dialogs.SelectFile("Transition preset|*.json");
            if (string.IsNullOrWhiteSpace(path)) return;
            if (new System.IO.FileInfo(path).Length > 65536) throw new ArgumentException("Choose a JSON file smaller than 64 KiB.");
            var preset = Models.TransitionPresets.Import(System.IO.File.ReadAllText(path));
            var model = (ViewModels.StartPageViewModel)DataContext;
            var settings = model.Settings.Settings;
            var name = preset.Name;
            for (var suffix = 2; settings.DashboardTransitionPresets.Any(item => string.Equals(item.Name, preset.Name, StringComparison.OrdinalIgnoreCase)); ++suffix)
                preset.Name = name + " (" + suffix + ")";
            settings.DashboardTransitionPresets.Add(preset);
            settings.DashboardCustomTransition = Models.TransitionPresets.Clone(preset);
            settings.DashboardTransition = DashboardTransition.Custom;
            TransitionPresetPicker.SelectedItem = preset;
            model.SaveCurrentPage();
            TransitionPresetStatus.Text = "Imported and saved preset: " + preset.Name;
        });

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
