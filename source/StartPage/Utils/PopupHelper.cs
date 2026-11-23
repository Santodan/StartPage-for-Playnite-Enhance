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
            var ownerHwnd = new WindowInteropHelper(owner).Handle;
            if (ownerHwnd == IntPtr.Zero) return;
            // Find the window directly above Playnite before touching anything, so the popup can be
            // slotted between it and Playnite. Any z-order call on an owned window moves its owner
            // along with it: HWND_BOTTOM sank Playnite behind every app, HWND_TOP/NOTOPMOST raised it.
            var above = owner.IsActive ? IntPtr.Zero : WindowAboveOwner(ownerHwnd, hwnd);
            SetWindowLongPtr(hwnd, -8, ownerHwnd);
            // Inserting a topmost window after a non-topmost one also clears its topmost bit, in one
            // move that keeps Playnite where it is. Without such a window Playnite already tops the
            // normal band, so lifting the popup to the top of that band changes nothing visible.
            SetWindowPos(hwnd, above != IntPtr.Zero ? above : new IntPtr(-2), 0, 0, 0, 0, flags);
        }

        // Non-topmost window directly above the owner (skipping the popup itself), or zero when the
        // owner is the top non-topmost window.
        private static IntPtr WindowAboveOwner(IntPtr ownerHwnd, IntPtr popupHwnd)
        {
            var above = GetWindow(ownerHwnd, 3); // GW_HWNDPREV
            while (above != IntPtr.Zero && (above == popupHwnd || GetWindow(above, 4) == ownerHwnd)) above = GetWindow(above, 3); // Skip windows owned by Playnite (GW_OWNER).
            if (above == IntPtr.Zero || (GetWindowLong(above, -20) & 0x8) != 0) return IntPtr.Zero; // WS_EX_TOPMOST
            return above;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

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
