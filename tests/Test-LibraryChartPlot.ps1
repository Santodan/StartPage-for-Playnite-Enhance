$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
foreach($name in @('Newtonsoft.Json','Playnite.SDK','LiveCharts','LiveCharts.Wpf','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name+'.dll'))) }
Add-Type -ReferencedAssemblies @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml',(Join-Path $build 'StartPagePlugin.dll'),(Join-Path $build 'LiveCharts.dll'),(Join-Path $build 'LiveCharts.Wpf.dll')) -TypeDefinition @'
using System;
using System.Windows.Controls;
using System.Windows.Media;
using LandingPage.Controls;
using LandingPage.Settings;
public static class PlotFixture {
 public static LibraryChartPlot Make(StatisticsChartStyle type) {
  var settings = LibraryStatisticsSettings.CreateChartSettings(); settings.ChartStyle = type; settings.CustomSegmentBorderColor = true; settings.SegmentBorderColor = "#11AA33"; settings.SliceSeparatorWidth = 2.5;
  return new LibraryChartPlot(settings,new [] {"Steam","Epic","GOG"},new [] {60d,25d,15d},
   i => new SolidColorBrush(new [] {Colors.DodgerBlue,Colors.Orange,Colors.MediumSeaGreen}[i]),
   i => new [] {"Steam 60","Epic 25","GOG 15"}[i],i => new TextBlock { FontSize = 14 },(text,i) => {}) { Height = 240 };
 }
}
'@
$window = [Windows.Window]::new(); $window.Width = 900; $window.Height = 600
$window.Background = [Windows.Media.Brushes]::Black
$window.Resources['TextBrush'] = [Windows.Media.Brushes]::LightSteelBlue
$grid = [Windows.Controls.Primitives.UniformGrid]::new(); $grid.Columns = 2
$plots = @()
foreach($style in [enum]::GetValues([LandingPage.Settings.StatisticsChartStyle])) {
 $plot = [PlotFixture]::Make($style); $plots += $plot; [void]$grid.Children.Add($plot)
}
$window.Content = $grid
try {
 $window.Show()
 $deadline = [datetime]::UtcNow.AddSeconds(2)
 while([datetime]::UtcNow -lt $deadline) { $window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 20 }
 foreach($plot in $plots) {
  if($plot.Chart.Series.Count -ne $(if($plot.Chart -is [LiveCharts.Wpf.PieChart]) { 3 } else { 1 }) -or !$plot.Chart.DisableAnimations -or $plot.ActualHeight -lt 200) { throw 'Chart renderer settings or measurement failed' }
  if($plot.Chart -is [LiveCharts.Wpf.CartesianChart]) {
   if($plot.Chart.AxisX[0].Foreground.Color -ne [Windows.Media.Colors]::LightSteelBlue -or @($plot.Chart.Series[0].ChartPoints).Count -ne 3) { throw 'Theme axis color or linear bar point count failed' }
   $fills = @($plot.Chart.Series[0].ChartPoints | ForEach-Object { $_.Fill.GradientStops[1].Color.ToString() })
   if(@($fills | Select-Object -Unique).Count -ne 3) { throw 'Per-category bar colors were lost' }
  }
  if($plot.Chart.Series[0].Stroke.Color.ToString() -ne '#FF11AA33' -or $plot.Chart.Series[0].StrokeThickness -ne 2.5) { throw 'Segment border color/width did not apply' }
  foreach($series in $plot.Chart.Series) { if($series.Fill -isnot [Windows.Media.LinearGradientBrush]) { throw 'Glossy series fill missing' }; if(@($series.ChartPoints).Count -eq 0) { throw 'Chart has no rendered data points' } }
 }
 $pie = $plots[2]; $originalSeries = $pie.Chart.Series[0]
 $native = @($originalSeries.ChartPoints)[0].View.GetType().GetProperty('Slice').GetValue(@($originalSeries.ChartPoints)[0].View)
 $beforeAngle = $native.RotationAngle
 foreach($angle in @(90,-90,360)) {
  $pie.UpdateRotation($angle)
  $until = [datetime]::UtcNow.AddMilliseconds(250)
  while([datetime]::UtcNow -lt $until) { $window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
  $native = @($originalSeries.ChartPoints)[0].View.GetType().GetProperty('Slice').GetValue(@($originalSeries.ChartPoints)[0].View)
  $expected = (($angle % 360)+360)%360
  if([Math]::Abs($native.RotationAngle-$expected) -gt .1 -or ![object]::ReferenceEquals($originalSeries,$pie.Chart.Series[0])) { throw ('Live rotation did not update actual slice geometry in place: expected='+$expected+', actual='+$native.RotationAngle) }
 }
 $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(900,600,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $bitmap.Render($grid); $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $path = Join-Path $env:TEMP 'startpage-livecharts-preview.png'; $stream = [IO.File]::Create($path); try { $encoder.Save($stream) } finally { $stream.Dispose() }
 $doughnut = $plots[1]; $doughnut.Width = 80; $window.UpdateLayout()
 $window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
 if($doughnut.Chart.InnerRadius -ge 40) { throw 'Doughnut hole did not shrink to fit a narrow widget' }
 "PASS: all four LiveCharts styles rendered real series with glossy fills and bounded height. Preview: $path"
} finally { $window.Close() }
