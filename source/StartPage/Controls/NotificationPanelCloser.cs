using System;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LandingPage.Controls
{
    // Makes Playnite's notifications panel behave like a flyout while the StartPage toolbar toggle shows it open:
    // a click outside the panel (and outside both notification toggles), "Dismiss all", or dismissing the last
    // notification closes it. The click itself still reaches what was clicked.
    // The toolbar is built before it is placed in Playnite's window, so the window and panel are looked up each time
    // the toggle becomes checked (it mirrors Playnite's own toggle, so opening from either one is covered).
    public sealed class NotificationPanelCloser
    {
        private Func<Window> hostResolver;
        private ToggleButton ownToggle;
        private Action close;
        private INotifyCollectionChanged messages;
        private Window window;
        private FrameworkElement panel;
        private UIElement nativeToggle;
        private Button dismissAll;
        private bool watching;
        private readonly MouseButtonEventHandler mouseHandler;

        public NotificationPanelCloser() { mouseHandler = WindowMouseDown; }
        public bool IsWatching => watching;
        public FrameworkElement Panel => panel;

        public void Attach(Func<Window> host,ToggleButton toggle,Action closePanel,INotifyCollectionChanged notificationMessages)
        {
            Detach();
            if (host == null || toggle == null || closePanel == null) return;
            hostResolver = host; ownToggle = toggle; close = closePanel; messages = notificationMessages;
            ownToggle.Checked += ToggleChanged;
            ownToggle.Unchecked += ToggleChanged;
            ownToggle.Loaded += ToggleChanged;
            Sync();
        }

        public void Detach()
        {
            Unwatch();
            if (ownToggle != null) { ownToggle.Checked -= ToggleChanged; ownToggle.Unchecked -= ToggleChanged; ownToggle.Loaded -= ToggleChanged; }
            hostResolver = null; ownToggle = null; close = null; messages = null;
        }

        private void ToggleChanged(object sender,RoutedEventArgs args) => Sync();

        private void Sync() { if (ownToggle != null && ownToggle.IsChecked == true) Watch(); else Unwatch(); }

        private void Watch()
        {
            if (watching) return;
            Window host = null;
            try { host = hostResolver?.Invoke(); } catch { }
            if (host == null) return; // not in a window yet; Loaded retries
            window = host;
            panel = FindByName(window,"PART_Notifications") ?? FindByTypeName(window,"NotificationPanel");
            // Playnite's own top-bar toggle opens/closes the panel itself; treating it as "outside" would reopen the panel.
            nativeToggle = FindByName(window,"PART_ToggleNotifications");
            watching = true;
            window.AddHandler(UIElement.PreviewMouseDownEvent,mouseHandler,true);
            dismissAll = panel == null ? null : FindByName(panel,"PART_ButtonDismissAll") as Button;
            if (dismissAll != null) dismissAll.Click += DismissAllClicked;
            if (messages != null) messages.CollectionChanged += MessagesChanged;
        }

        private void Unwatch()
        {
            if (!watching) return;
            watching = false;
            window?.RemoveHandler(UIElement.PreviewMouseDownEvent,mouseHandler);
            if (dismissAll != null) dismissAll.Click -= DismissAllClicked;
            if (messages != null) messages.CollectionChanged -= MessagesChanged;
            dismissAll = null; window = null; panel = null; nativeToggle = null;
        }

        private void WindowMouseDown(object sender,MouseButtonEventArgs args)
        {
            var source = args.OriginalSource as DependencyObject;
            if (Inside(source,panel) || Inside(source,ownToggle) || Inside(source,nativeToggle)) return;
            CloseSoon();
        }
        private void DismissAllClicked(object sender,RoutedEventArgs args) => CloseSoon();
        private void MessagesChanged(object sender,NotifyCollectionChangedEventArgs args) { if (messages is ICollection collection && collection.Count == 0) CloseSoon(); }

        // Close after the current input finishes, so the click/dismiss is handled first.
        private void CloseSoon()
        {
            var action = close; var dispatcher = ownToggle?.Dispatcher ?? window?.Dispatcher;
            if (action == null || dispatcher == null) return;
            dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(() => { if (watching) { try { action(); } catch (Exception ex) { LandingPageExtension.logger?.Debug(ex,"Unable to close the notifications panel."); } } }));
        }

        private static bool Inside(DependencyObject node,DependencyObject container)
        {
            if (node == null || container == null) return false;
            for (var current = node; current != null; current = current is Visual || current is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
                if (ReferenceEquals(current,container)) return true;
            return false;
        }

        public static FrameworkElement FindByName(DependencyObject root,string name) => Find(root,element => element.Name == name);
        public static FrameworkElement FindByTypeName(DependencyObject root,string typeName) => Find(root,element => element.GetType().Name == typeName);

        private static FrameworkElement Find(DependencyObject root,Func<FrameworkElement,bool> match)
        {
            if (root == null) return null;
            if (root is FrameworkElement element && match(element)) return element;
            var count = root is Visual || root is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(root) : 0;
            for (var index = 0; index < count; index++) { var found = Find(VisualTreeHelper.GetChild(root,index),match); if (found != null) return found; }
            return null;
        }
    }
}
