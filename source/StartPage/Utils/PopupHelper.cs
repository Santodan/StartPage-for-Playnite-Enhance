using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LandingPage.Utils
{
    public static class PopupHelper
    {
        public static readonly DependencyProperty IsNotTopmostProperty =
            DependencyProperty.RegisterAttached(
                "IsNotTopmost",
                typeof(bool),
                typeof(PopupHelper),
                new PropertyMetadata(false, OnIsNotTopmostChanged));

        public static void SetIsNotTopmost(DependencyObject element, bool value)
            => element.SetValue(IsNotTopmostProperty, value);

        public static bool GetIsNotTopmost(DependencyObject element)
            => (bool)element.GetValue(IsNotTopmostProperty);

        private static void OnIsNotTopmostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is Popup popup))
                return;

            if ((bool)e.NewValue)
            {
                popup.Opened -= PopupOnOpened;
                popup.Opened += PopupOnOpened;
            }
            else
            {
                popup.Opened -= PopupOnOpened;
            }
        }

        private static void PopupOnOpened(object sender, EventArgs e)
        {
            if (!(sender is Popup popup))
                return;

            var owner = Window.GetWindow(popup.PlacementTarget);
            if (owner == null) return;

            EventHandler update = (s, args) => popup.Dispatcher.BeginInvoke(
                DispatcherPriority.Background, new Action(() => UpdateWindow(popup, owner)));
            EventHandler closed = null;
            closed = (s, args) =>
            {
                owner.Activated -= update;
                owner.Deactivated -= update;
                popup.Closed -= closed;
            };
            owner.Activated += update;
            owner.Deactivated += update;
            popup.Closed += closed;
            update(null, EventArgs.Empty);
        }

        private static void UpdateWindow(Popup popup, Window owner)
        {
            if (!popup.IsOpen || popup.Child == null) return;
            var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
            if (source == null) return;
            var hwnd = source.Handle;
            const int flags = 0x0001 | 0x0002 | 0x0010; // No size, move, or activation.
            // WPF can promote a popup again after opening. Reset its z-order on owner
            // activation changes, and give it an owner so it follows Playnite behind other apps.
            SetWindowLongPtr(hwnd, -8, new WindowInteropHelper(owner).Handle);
            SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0, flags);
            if (owner.IsActive) SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, flags);
            SetWindowPos(hwnd, new IntPtr(-2), 0, 0, 0, 0, flags);
        }

        private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
            => IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, index, value)
                : new IntPtr(SetWindowLong32(hwnd, index, value.ToInt32()));

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            int uFlags);
    }


}
