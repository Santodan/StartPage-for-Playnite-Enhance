# Run -STA. Notifications panel opened from the StartPage toolbar closes like a flyout: outside clicks, Dismiss all and
# dismissing the last notification close it; clicks inside it or on either toggle do not; detaching stops it.
# The closer is attached before the toggle is in a window (as the toolbar does) and follows the toggle's checked state.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
Add-Type -Path (Join-Path $root 'source\StartPage\bin\Release\StartPagePlugin.dll')
function Flush { $frame = [Windows.Threading.DispatcherFrame]::new(); [Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle,[Action]{ $frame.Continue = $false }) | Out-Null; [Windows.Threading.Dispatcher]::PushFrame($frame) }
function Press($element) {
    $args = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left)
    $args.RoutedEvent = [Windows.UIElement]::PreviewMouseDownEvent; $element.RaiseEvent($args); Flush
}
$window = [Windows.Window]::new(); $window.Width = 600; $window.Height = 400; $window.ShowInTaskbar = $false; $window.Left = 60; $window.Top = 60
$layout = [Windows.Controls.StackPanel]::new(); $window.Content = $layout
$panel = [Windows.Controls.Border]::new(); $panel.Name = 'PART_Notifications'; $panel.Height = 120
$panelContent = [Windows.Controls.StackPanel]::new(); $panel.Child = $panelContent
$dismiss = [Windows.Controls.Button]::new(); $dismiss.Name = 'PART_ButtonDismissAll'; $dismiss.Content = 'Dismiss all'; [void]$panelContent.Children.Add($dismiss)
$message = [Windows.Controls.TextBlock]::new(); $message.Text = 'A notification'; [void]$panelContent.Children.Add($message)
$ownToggle = [Windows.Controls.Primitives.ToggleButton]::new(); $ownToggle.Content = 'StartPage toggle'
$nativeToggle = [Windows.Controls.CheckBox]::new(); $nativeToggle.Name = 'PART_ToggleNotifications'; $nativeToggle.Content = 'Playnite toggle'
$outside = [Windows.Controls.Button]::new(); $outside.Content = 'Somewhere else'
foreach ($child in @($panel,$nativeToggle,$outside)) { [void]$layout.Children.Add($child) }
$messages = [Collections.ObjectModel.ObservableCollection[object]]::new()
$script:closed = 0
$close = [Action]{ $script:closed++; $ownToggle.IsChecked = $false; $panel.Visibility = 'Collapsed' }
function Open { $panel.Visibility = 'Visible'; $ownToggle.IsChecked = $true; Flush }
$closer = [LandingPage.Controls.NotificationPanelCloser]::new()
try {
    $window.Show(); Flush
    # Attached while the toggle is not in any window yet and the panel is already open (checked).
    $ownToggle.IsChecked = $true
    $closer.Attach([Func[Windows.Window]]{ [Windows.Window]::GetWindow($ownToggle) },$ownToggle,$close,$messages)
    if ($closer.IsWatching) { throw 'The closer watched before its toggle was in a window' }
    [void]$layout.Children.Add($ownToggle); Flush
    if (!$closer.IsWatching -or ![object]::ReferenceEquals($closer.Panel,$panel)) { throw 'The closer did not start watching once its toggle was loaded into the window' }
    $ownToggle.IsChecked = $false; Flush
    if ($closer.IsWatching) { throw 'Unchecking the toggle did not stop watching' }
    Open
    if (!$closer.IsWatching) { throw 'Checking the toggle (opening the panel) did not start watching' }
    'PASS: the closer attaches before the toggle is in a window and follows the toggle state.'

    foreach ($target in @($message,$dismiss,$ownToggle,$nativeToggle)) { Press $target }
    if ($script:closed -ne 0) { throw 'Clicking inside the panel or on a notifications toggle closed the panel' }
    Press $outside
    if ($script:closed -ne 1 -or $panel.Visibility -ne 'Collapsed' -or $closer.IsWatching) { throw 'Clicking outside did not close the notifications panel' }
    'PASS: clicks outside close the panel; clicks inside it or on either toggle do not.'

    Open
    $dismiss.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent)); Flush
    if ($script:closed -ne 2 -or $panel.Visibility -ne 'Collapsed') { throw 'Dismiss all did not close the notifications panel' }
    Open
    $messages.Add('one'); $messages.Add('two'); Flush
    $messages.RemoveAt(0); Flush
    if ($script:closed -ne 2) { throw 'Dismissing one of several notifications closed the panel' }
    $messages.RemoveAt(0); Flush
    if ($script:closed -ne 3 -or $panel.Visibility -ne 'Collapsed') { throw 'Dismissing the last notification did not close the panel' }
    'PASS: Dismiss all and dismissing the last notification close the panel.'

    Open
    $closer.Detach(); Press $outside
    $messages.Add('again'); $messages.Clear(); Flush
    if ($script:closed -ne 3 -or $closer.IsWatching) { throw 'A detached closer still closed the panel' }
    'PASS: detaching stops all closing behaviour.'
} finally { $closer.Detach(); $window.Close() }
