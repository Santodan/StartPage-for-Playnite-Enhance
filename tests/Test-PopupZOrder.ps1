# Run -STA. Shelf popups (PopupHelper.IsNotTopmost) must never change the z-order of Playnite's window:
# not sink it behind every app, not raise it above the app that covers it.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
Add-Type -Path (Join-Path $root 'source\StartPage\bin\Release\StartPagePlugin.dll')
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class PopupZOrderProbe {
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint c);
    [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr h);
    public static int Index(IntPtr target) { int i = 0; for (var h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, 2)) { if (h == target) return i; i++; } return -1; }
}
'@
function Flush-ZOrder { $frame = [Windows.Threading.DispatcherFrame]::new(); [Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle,[Action]{ $frame.Continue = $false }) | Out-Null; [Windows.Threading.Dispatcher]::PushFrame($frame) }
function Get-ZIndex($window) { [PopupZOrderProbe]::Index(([Windows.Interop.WindowInteropHelper]::new($window)).Handle) }
function New-ZWindow($title,$left) { $w = [Windows.Window]::new(); $w.Title = $title; $w.Width = 400; $w.Height = 300; $w.Left = $left; $w.Top = 100; $w.ShowInTaskbar = $false; $w }

$below = New-ZWindow 'Below' 100; $owner = New-ZWindow 'Playnite' 150; $cover = New-ZWindow 'Other app' 200
$popup = $null
try {
    $target = [Windows.Controls.Border]::new(); $owner.Content = $target
    $below.Show(); Flush-ZOrder; $owner.Show(); Flush-ZOrder; $cover.Show(); $cover.Activate() | Out-Null; Flush-ZOrder
    $popup = [Windows.Controls.Primitives.Popup]::new()
    $popup.Child = [Windows.Controls.Border]::new(); $popup.Child.Width = 100; $popup.Child.Height = 100
    $popup.PlacementTarget = $target; $popup.Placement = 'Center'; $popup.StaysOpen = $true
    [LandingPage.Utils.PopupHelper]::SetIsNotTopmost($popup,$true)
    $popup.IsOpen = $true; Flush-ZOrder; Flush-ZOrder
    $popupIndex = { [PopupZOrderProbe]::Index(([Windows.PresentationSource]::FromVisual($popup.Child)).Handle) }

    $c = Get-ZIndex $cover; $o = Get-ZIndex $owner; $b = Get-ZIndex $below; $p = & $popupIndex
    if(!($c -lt $o -and $o -lt $b)) { throw "Opening a shelf popup behind another app moved Playnite (cover=$c owner=$o below=$b)" }
    if(!($c -lt $p -and $p -lt $o)) { throw "Background shelf popup is not between the covering app and Playnite (cover=$c popup=$p owner=$o)" }
    Write-Host 'PASS: background shelf popup keeps the window order'

    $owner.Activate() | Out-Null; Flush-ZOrder; Flush-ZOrder
    $c = Get-ZIndex $cover; $o = Get-ZIndex $owner; $p = & $popupIndex
    if(!($p -lt $o -and $o -lt $c)) { throw "Shelf popup is not above the active Playnite window (popup=$p owner=$o cover=$c)" }
    Write-Host 'PASS: shelf popup stays above active Playnite'

    $cover.Activate() | Out-Null; Flush-ZOrder; Flush-ZOrder
    $c = Get-ZIndex $cover; $o = Get-ZIndex $owner; $b = Get-ZIndex $below; $p = & $popupIndex
    if(!($c -lt $p -and $p -lt $o -and $o -lt $b)) { throw "Deactivating Playnite with a shelf popup open changed the window order (cover=$c popup=$p owner=$o below=$b)" }
    Write-Host 'PASS: deactivating Playnite keeps the popup behind the other app'
} finally {
    if($popup) { $popup.IsOpen = $false }
    $cover.Close(); $owner.Close(); $below.Close()
}
