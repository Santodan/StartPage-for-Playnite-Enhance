param([string]$PlayniteDirectory = 'E:\Programs\Playnite')
$ErrorActionPreference = 'Stop'
if ([Environment]::Is64BitProcess) {
    & "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -STA -File $PSCommandPath -PlayniteDirectory $PlayniteDirectory
    exit $LASTEXITCODE
}
Add-Type -AssemblyName PresentationFramework
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
[void][Reflection.Assembly]::LoadFrom((Join-Path $PlayniteDirectory 'Playnite.SDK.dll'))
foreach ($name in @('Newtonsoft.Json','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name+'.dll'))) }
[void][Reflection.Assembly]::LoadFrom((Join-Path $PlayniteDirectory 'Playnite.dll'))
$app = [Windows.Application]::new()
$app.Resources.Add('TextBrush',[Windows.Media.Brushes]::White)
$app.Resources.Add('GlyphColor',[Windows.Media.Colors]::Blue)
$flags = [Reflection.BindingFlags]'Static,NonPublic,Public'
try {
    $type = [LandingPage.Views.FeaturedGameView].Assembly.GetType('LandingPage.Controls.LibraryDescriptionRenderer')
    $create = $type.GetMethod('Create',$flags)
    $html = '<h1>Heading</h1><p><b>Bold</b> and <i>italic</i><br>Next line</p><ul><li>First</li><li>Second</li></ul>'
    $formatted = $create.Invoke($null,@($html,18.0,[Windows.Media.FontFamily]::new('Segoe UI'),[Windows.Media.Brushes]::Orange))
    if (!$formatted -or $formatted.Content.GetType().FullName -ne 'Playnite.Controls.HtmlTextView') { throw ('Native HTML renderer failed: ' + $type.GetProperty('LastError',$flags).GetValue($null)) }
    $native = $formatted.Content
    if ($native.HtmlText -ne $html -or $native.HtmlFontSize -ne 18 -or $native.PartialLoadEnabled) { throw 'HTML, font sizing or unlimited content was not passed through to Playnite' }
    $window = [Windows.Window]::new(); $window.Width = 500; $window.Height = 500; $window.Opacity = 0; $window.ShowActivated = $false; $window.Content = $formatted
    $window.Show(); $window.UpdateLayout()
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($native.Children.Count -eq 0 -or $native.ActualHeight -le 0 -or $native.HtmlForeground -ne [Windows.Media.Colors]::Orange) { throw 'Library HTML failed to render formatted content or use custom text color' }
    $formatted.Foreground = [Windows.Media.Brushes]::Lime
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($native.HtmlForeground -ne [Windows.Media.Colors]::Lime) { throw 'Section/panel text color did not reach the HTML renderer live' }
    $richHeight = $native.ActualHeight
    $native.HtmlText = 'Heading Bold and italic Next line First Second'
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $window.UpdateLayout()
    if ($richHeight -le $native.ActualHeight) { throw 'Heading, paragraph and list formatting did not affect the rendered layout' }
    'PASS: real x86 Playnite HtmlTextView, intact headings/paragraphs/lists/emphasis, full-content mode, layout and live theme/custom text colors.'
} finally { if ($window) { $window.Close() }; $app.Shutdown() }
