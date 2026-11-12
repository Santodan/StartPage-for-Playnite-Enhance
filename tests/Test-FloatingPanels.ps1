$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll'))) }
$app = [Windows.Application]::new()
$app.Resources.Add('TextBrush',[Windows.Media.Brushes]::White)
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$stateProperty = [LandingPage.Controls.FloatingPanel].GetField('StateProperty',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
function Pump { $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle) }
function Begin-Drag($state,$x,$y,$edges) {
    $method = $state.GetType().GetMethod('Begin',$flags)
    $mode = [Enum]::ToObject($method.GetParameters()[1].ParameterType,$edges)
    [void]$method.Invoke($state,@([Windows.Point]::new($x,$y),$mode))
}
function Move-Drag($state,$x,$y) { [void]$state.GetType().GetMethod('Update',$flags).Invoke($state,@([Windows.Point]::new($x,$y))); Pump }
try {
    $window = [Windows.Window]::new(); $window.Width = 900; $window.Height = 700
    $window.Opacity = 0; $window.ShowActivated = $false
    $grid = [Windows.Controls.Grid]::new(); $window.Content = $grid
    $frame = [Windows.Controls.Border]::new(); $frame.Width = 320; $frame.Height = 400
    $frame.Padding = [Windows.Thickness]::new(8); $frame.Margin = [Windows.Thickness]::new(16,64,16,24)
    $frame.HorizontalAlignment = [Windows.HorizontalAlignment]::Right
    $field = [Windows.Controls.TextBox]::new(); $field.Text = 'Keep edits while moving'
    $frame.Child = $field
    [LandingPage.Controls.FloatingPanel]::SetEnabled($frame,$true)
    [void]$grid.Children.Add($frame); $window.Show(); Pump
    $state = $frame.GetValue($stateProperty)
    $before = $frame.TranslatePoint([Windows.Point]::new(0,0),$grid)
    Begin-Drag $state 100 100 0
    Move-Drag $state 40 140
    $after = $frame.TranslatePoint([Windows.Point]::new(0,0),$grid)
    if ([Math]::Abs($after.X-$before.X+60) -gt 1 -or [Math]::Abs($after.Y-$before.Y-40) -gt 1) { throw 'Overlay title drag did not move by the expected offset' }
    Begin-Drag $state 0 0 10
    Move-Drag $state 50 60
    if ($frame.Width -ne 370 -or $frame.Height -ne 460 -or $field.Text -ne 'Keep edits while moving') { throw 'Resizing lost geometry or editor state' }
    Begin-Drag $state 0 0 0
    Move-Drag $state 10000 10000
    if ($frame.Margin.Left+$frame.ActualWidth -gt $grid.ActualWidth-7 -or $frame.Margin.Top+$frame.ActualHeight -gt $grid.ActualHeight-7) { throw 'Dragged editor escaped its host' }
    $window.Width = 600; $window.Height = 500; Pump
    if ($frame.Margin.Left+$frame.ActualWidth -gt $grid.ActualWidth-7 -or $frame.Margin.Top+$frame.ActualHeight -gt $grid.ActualHeight-7) { throw 'Owner resize stranded the editor off screen' }
    $button = [Windows.Controls.Button]::new(); $button.Width = 32; $button.Height = 24
    $button.HorizontalAlignment = [Windows.HorizontalAlignment]::Left; $button.VerticalAlignment = [Windows.VerticalAlignment]::Top
    $button.Margin = [Windows.Thickness]::new(80,50,0,0); [void]$grid.Children.Add($button); Pump
    $popup = [Windows.Controls.Primitives.Popup]::new(); $popup.PlacementTarget = $button
    $popup.Placement = [Windows.Controls.Primitives.PlacementMode]::Bottom; $popup.StaysOpen = $true
    $popupFrame = [Windows.Controls.Border]::new(); $popupFrame.Width = 260; $popupFrame.Height = 240
    $popupFrame.Child = [Windows.Controls.TextBox]::new()
    [LandingPage.Controls.FloatingPanel]::SetEnabled($popupFrame,$true)
    $popup.Child = $popupFrame; $popup.IsOpen = $true; Pump
    $popupState = $popupFrame.GetValue($stateProperty)
    if (!$popupState.GetType().GetField('popup',$flags).GetValue($popupState)) { throw 'Popup editor was not recognized' }
    Begin-Drag $popupState 100 100 0
    Move-Drag $popupState 120 125
    if ([Math]::Abs($popup.HorizontalOffset-20) -gt 1 -or [Math]::Abs($popup.VerticalOffset-25) -gt 1 -or !$popup.IsOpen) { throw 'Popup drag failed or dismissed the editor' }
    Begin-Drag $popupState 0 0 10
    Move-Drag $popupState 40 30
    if ($popupFrame.Width -ne 300 -or $popupFrame.Height -ne 270) { throw 'Popup resizing failed' }
    $popup.IsOpen = $false
    # Popup bindings/arrangement can arrive after Loaded. Exercise an editor
    # with no explicit Height and no arranged size at initialization.
    $autoFrame = [Windows.Controls.Border]::new(); $autoFrame.Width = 300
    $autoFrame.Child = [Windows.Controls.ScrollViewer]::new()
    $autoFrame.Child.Content = [Windows.Controls.StackPanel]::new()
    [LandingPage.Controls.FloatingPanel]::SetEnabled($autoFrame,$true)
    $popup.Child = $autoFrame
    $autoState = $autoFrame.GetValue($stateProperty)
    [void]$autoState.GetType().GetMethod('Loaded',$flags).Invoke($autoState,@($autoFrame,[Windows.RoutedEventArgs]::new()))
    if ($autoFrame.Height -lt 140) { throw 'Unarranged popup was frozen at zero height' }
    $popup.IsOpen = $true; Pump
    if ($autoFrame.ActualHeight -lt 140 -or !$popup.IsOpen) { throw 'Auto-height settings popup failed to remain visible' }
    $popup.IsOpen = $false
    [LandingPage.Controls.FloatingPanel]::SetEnabled($frame,$false)
    if (![object]::ReferenceEquals($frame.Child,$field)) { throw 'Detaching chrome did not restore the original editor' }
    $xml = [xml](Get-Content (Join-Path $root 'source/StartPage/Views/ShelveView.xaml') -Raw)
    $namespaces = [Xml.XmlNamespaceManager]::new($xml.NameTable); $namespaces.AddNamespace('w','http://schemas.microsoft.com/winfx/2006/xaml/presentation')
    $toggle = $xml.SelectSingleNode('//w:CheckBox[@Content="Show game details popup on hover"]',$namespaces)
    $section = $toggle.ParentNode.ParentNode
    if ($section.LocalName -ne 'Expander' -or !$section.InnerXml.Contains('Shelf appearance')) { throw 'Popup option is outside Shelf appearance' }
    'PASS: overlay dragging/resizing, host bounds, owner resize, popup dragging/resizing, preserved editor state, detach cleanup, and popup option placement.'
} finally { if ($popup) { $popup.IsOpen = $false }; if ($window) { $window.Close() }; $app.Shutdown() }
