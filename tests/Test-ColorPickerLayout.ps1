# powershell.exe -NoProfile -STA -File tests/Test-ColorPickerLayout.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source\StartPage\bin\Release'
[void][Reflection.Assembly]::LoadFrom((Join-Path $build 'StartPagePlugin.dll'))
$app = [Windows.Application]::new()
$app.Resources.Add('TextBrush',[Windows.Media.Brushes]::White)
$grid = [Windows.Controls.Primitives.UniformGrid]::new(); $grid.Columns = 3
foreach ($name in @('Progress','Completed / goal reached','Progress track','Card background','Card border')) {
    $picker = [LandingPage.Controls.PopupColorPicker]::new(); $picker.Label = $name
    $picker.Margin = [Windows.Thickness]::new(0,4,8,4)
    [void]$grid.Children.Add($picker)
}
foreach ($width in @(320,360,500,800)) {
    $grid.Measure([Windows.Size]::new($width,[double]::PositiveInfinity))
    $grid.Arrange([Windows.Rect]::new(0,0,$width,$grid.DesiredSize.Height)); $grid.UpdateLayout()
    foreach ($picker in $grid.Children) {
        $row = $picker.Content.Children[0]; $label = $row.Children[0]; $button = $row.Children[1]
        $position = $button.TranslatePoint([Windows.Point]::new(0,0),$picker)
        if ($position.X -lt 0 -or $position.X+$button.ActualWidth -gt $picker.ActualWidth+.5) { throw "Clipped swatch at width $width for $($picker.Label)" }
        if ($label.ActualWidth -le 0 -or $label.ActualHeight -lt $label.DesiredSize.Height-.5) { throw "Clipped label at width $width for $($picker.Label)" }
    }
}
'PASS: all five color labels and swatches fit three-column rows at widths 320, 360, 500 and 800.'

# Per-statistic popup toggles invert the existing inheritance flags in both directions.
$inverse = [LandingPage.Views.Settings.InverseGlobalColorConverter]::new()
foreach ($global in @($true,$false)) {
    $custom = $inverse.Convert($global,[bool],$null,[Globalization.CultureInfo]::InvariantCulture)
    if ($custom -eq $global -or $inverse.ConvertBack($custom,[bool],$null,[Globalization.CultureInfo]::InvariantCulture) -ne $global) { throw 'Global/custom color inheritance does not round-trip' }
}
# Every built-in view should use the compact control, not the expanding color editor.
$views = Join-Path (Split-Path $PSScriptRoot -Parent) 'source\StartPage\Views'
foreach ($file in Get-ChildItem -LiteralPath $views -Recurse -File -Include *.xaml,*.cs) {
    if ((Get-Content -LiteralPath $file.FullName -Raw) -match 'controls:ColorPicker|new ColorPicker\(') { throw ("Legacy expanding color editor remains: "+$file.FullName) }
}
'PASS: built-in editors use compact popups and global/custom inheritance round-trips.'
