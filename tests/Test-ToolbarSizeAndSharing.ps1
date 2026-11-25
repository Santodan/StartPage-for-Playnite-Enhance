# Run -STA. Toolbar window size button and shared toolbar: size presets and persistence, fit/resize math on a real
# window, the toolbar button/menu and custom size dialog, shared toolbar resolution, profile round-trip, the editor host
# and the toolbar widget rendering the shared toolbar. Reuses the in-memory API/window fixture.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$toolbarTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $toolbarTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    function Pump-For($milliseconds) {
        $until = [datetime]::UtcNow.AddMilliseconds($milliseconds)
        do { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background); Start-Sleep -Milliseconds 10 } while ([datetime]::UtcNow -lt $until)
        $window.UpdateLayout()
    }
    foreach($type in @([Windows.Controls.TextBox],[Windows.Controls.Expander],[Windows.Controls.CheckBox],[Windows.Controls.ComboBox],[Windows.Controls.Button],[Windows.Controls.ContextMenu],[Windows.Controls.MenuItem],[Windows.Controls.Separator])) { if(!$app.Resources.Contains($type)) { $app.Resources.Add($type,[Windows.Style]::new($type)) } }
    $json = { param($value) [Newtonsoft.Json.JsonConvert]::SerializeObject($value) }
    $toolbarType = [LandingPage.Views.ToolbarWidgetView]; $internal = [Reflection.BindingFlags]'Instance,NonPublic,Public'
    function Render-Toolbar($target) { [void]$toolbarType.GetMethod('RenderToolbar',$internal).Invoke($target,@()) }
    function Size-Menu($target) { $toolbarType.GetMethod('WindowSizeMenu',$internal).Invoke($target,@()) }

    # Size presets: built-ins, clamping, names, persistence, older configs.
    $defaults = @([LandingPage.Settings.WindowSizePreset]::Defaults)
    if (($defaults | ForEach-Object { "$($_.Name)=$($_.Width)x$($_.Height)" }) -join ',' -ne '4K=3840x2160,2K=2560x1440,1080p=1920x1080,720p=1280x720') { throw 'Built-in window sizes are not 4K, 2K, 1080p and 720p' }
    $odd = [LandingPage.Settings.WindowSizePreset]::Create('',50,99999)
    if ($odd.Width -ne 320 -or $odd.Height -ne 16384 -or $odd.Name -ne "320 $([char]0x00D7) 16384") { throw 'Window size limits or default name are wrong' }
    $settings = [LandingPage.LandingPageSettings]::new()
    (Get-Setting $settings 'CustomWindowSizes').Add([LandingPage.Settings.WindowSizePreset]::Create('Ultrawide',3440,1440))
    $copy = [Newtonsoft.Json.JsonConvert]::DeserializeObject((& $json $settings),[LandingPage.LandingPageSettings])
    $saved = @(Get-Setting $copy 'CustomWindowSizes')
    if ($saved.Count -ne 1 -or $saved[0].Name -ne 'Ultrawide' -or $saved[0].Width -ne 3440) { throw 'Custom window sizes did not persist' }
    if ((& $json $saved[0]) -match '"Label"|"SizeText"') { throw 'Runtime-only window size labels were serialized' }
    $legacy = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{"DashboardTransition":1}',[LandingPage.LandingPageSettings])
    $legacySizes = [LandingPage.LandingPageSettings].GetProperty('CustomWindowSizes').GetValue($legacy); $legacySharing = [LandingPage.LandingPageSettings].GetProperty('UseSharedToolbar').GetValue($legacy)
    if ($legacySizes.Count -ne 0 -or $legacySharing) { throw "Older configs do not load without custom sizes and sharing (sizes=$($legacySizes.Count); sharing=$legacySharing)" }
    'PASS: built-in sizes, limits, default names, persistence and legacy load.'

    # Fit math: shrink to the work area, keep the center, stay inside the work area.
    $work = [Windows.Rect]::new(0,0,1920,1040)
    $fit = [LandingPage.Utils.WindowSizer]::Fit([Windows.Rect]::new(400,200,800,600),[Windows.Size]::new(1280,720),$work)
    if ($fit.Width -ne 1280 -or $fit.Height -ne 720 -or $fit.Left -ne 160 -or $fit.Top -ne 140) { throw "Fit did not keep the window centered: $fit" }
    $big = [LandingPage.Utils.WindowSizer]::Fit([Windows.Rect]::new(400,200,800,600),[Windows.Size]::new(3840,2160),$work)
    if ($big.Width -ne 1920 -or $big.Height -ne 1040 -or $big.Left -ne 0 -or $big.Top -ne 0) { throw "Oversized request was not reduced to the work area: $big" }
    $edge = [LandingPage.Utils.WindowSizer]::Fit([Windows.Rect]::new(1700,900,200,100),[Windows.Size]::new(800,600),$work)
    if ($edge.Right -gt 1920 -or $edge.Bottom -gt 1040) { throw "A window near the edge was not kept on screen: $edge" }
    # Real window: physical pixels, restore from maximized, shrink when too large.
    $test = [Windows.Window]::new(); $test.Width = 800; $test.Height = 600; $test.Left = 100; $test.Top = 100; $test.ShowInTaskbar = $false; $test.WindowStyle = 'None'; $test.Show(); Pump-For 50
    $shrunk = $false
    [void][LandingPage.Utils.WindowSizer]::Apply($test,1000,700,[ref]$shrunk); Pump-For 80
    $pixels = [LandingPage.Utils.WindowSizer]::CurrentPixels($test)
    if ([Math]::Abs($pixels.Width - 1000) -gt 2 -or [Math]::Abs($pixels.Height - 700) -gt 2 -or $shrunk) { throw "Window was not resized to 1000 x 700 pixels: $pixels" }
    $test.WindowState = 'Maximized'; Pump-For 80
    [void][LandingPage.Utils.WindowSizer]::Apply($test,900,600,[ref]$shrunk); Pump-For 80
    if ($test.WindowState -ne 'Normal' -or [Math]::Abs([LandingPage.Utils.WindowSizer]::CurrentPixels($test).Width - 900) -gt 2) { throw 'A maximized window was not restored and resized' }
    [void][LandingPage.Utils.WindowSizer]::Apply($test,16000,12000,[ref]$shrunk); Pump-For 80
    if (!$shrunk) { throw 'An oversized window size was not reported as reduced to fit' }
    'PASS: fit math, pixel-accurate resizing, restore from maximized and shrink to fit.'

    # Toolbar: the window size item, its menu and applying a size.
    $options = [LandingPage.Controls.NativeToolbarCatalog]::Discover($null,$null)
    if (!($options | Where-Object Id -eq 'window.size')) { throw 'The toolbar catalog has no window size item' }
    $sizeToolbar = [LandingPage.Views.ToolbarWidgetView]::new()
    $sizeEntry = [LandingPage.Settings.ToolbarEntry]::new(); $sizeEntry.Id = 'window.size'; $sizeEntry.GroupId = 'main'
    $sizeToolbar.Settings.Items.Clear(); $sizeToolbar.Settings.Items.Add($sizeEntry)
    $sizeHost = [Windows.Controls.Grid]::new(); [void]$sizeHost.Children.Add($sizeToolbar); $test.Content = $sizeHost; Pump-For 150
    Render-Toolbar $sizeToolbar; Pump-For 50
    function Get-Descendants($node) { $count = [Windows.Media.VisualTreeHelper]::GetChildrenCount($node); for($i = 0; $i -lt $count; $i++) { $child = [Windows.Media.VisualTreeHelper]::GetChild($node,$i); $child; Get-Descendants $child } }
    $buttons = @(Get-Descendants $sizeToolbar | Where-Object { $_ -is [Windows.Controls.Button] -and $_.ToolTip -eq 'Playnite window size' })
    if ($buttons.Count -ne 1 -or !$buttons[0].IsEnabled) { throw 'The window size toolbar button was not rendered' }
    $globalSizes = Get-Setting $settingsModel.Settings 'CustomWindowSizes'; $globalSizes.Clear(); $globalSizes.Add([LandingPage.Settings.WindowSizePreset]::Create('Small',1100,650))
    $menu = Size-Menu $sizeToolbar
    $labels = @($menu.Items | Where-Object { $_ -is [Windows.Controls.MenuItem] } | ForEach-Object Header)
    if ($labels.Count -ne 6 -or $labels[0] -notmatch '^4K' -or $labels[4] -notmatch '^Small' -or $labels[5] -notmatch 'custom sizes') { throw "Window size menu items are wrong: $($labels -join ' | ')" }
    $small = @($menu.Items | Where-Object { $_ -is [Windows.Controls.MenuItem] -and $_.Header -match '^Small' })[0]
    $small.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.MenuItem]::ClickEvent)); Pump-For 80
    if ([Math]::Abs([LandingPage.Utils.WindowSizer]::CurrentPixels($test).Width - 1100) -gt 2) { throw 'Choosing a custom size from the menu did not resize the window' }
    if (!(@((Size-Menu $sizeToolbar).Items | Where-Object { $_ -is [Windows.Controls.MenuItem] -and $_.Header -match '^Small' })[0].IsChecked)) { throw 'The current size is not checked in the menu' }
    $globalSizes.Clear()
    'PASS: window size toolbar button, built-in and custom menu entries, applying a size and marking the current one.'

    # Custom size dialog: add, reject duplicates, use the current size, remove.
    $list = [Collections.ObjectModel.ObservableCollection[LandingPage.Settings.WindowSizePreset]]::new(); $script:saves = 0
    $dialog = [LandingPage.Views.WindowSizeDialog]::new($list,[Func[Windows.Window]]{ $test },[Action]{ $script:saves++ })
    $dialog.FindName('NameInput').Text = 'Stream'; $dialog.FindName('WidthInput').Value = 1600; $dialog.FindName('HeightInput').Value = 900
    $added = $dialog.AddFromForm()
    if ($list.Count -ne 1 -or $added.Name -ne 'Stream' -or $added.Height -ne 900 -or $script:saves -ne 1) { throw 'Adding a custom size did not save it' }
    $dialog.FindName('NameInput').Text = 'Stream'; $dialog.FindName('WidthInput').Value = 1600; $dialog.FindName('HeightInput').Value = 900
    if ($dialog.AddFromForm() -or $list.Count -ne 1) { throw 'A duplicate custom size was added' }
    $dialog.FindName('NameInput').Text = ''
    [LandingPage.Views.WindowSizeDialog].GetMethod('UseCurrent_Click',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($dialog,@($null,[Windows.RoutedEventArgs]::new()))
    $currentPixels = [LandingPage.Utils.WindowSizer]::CurrentPixels($test)
    if ($dialog.FindName('WidthInput').Value -ne [int]$currentPixels.Width -or $dialog.FindName('NameInput').Text -ne 'Current size') { throw 'Use current size did not fill in the window size' }
    $removeSender = [Windows.Controls.Button]::new(); $removeSender.DataContext = $added
    [LandingPage.Views.WindowSizeDialog].GetMethod('RemoveSize_Click',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($dialog,@($removeSender,[Windows.RoutedEventArgs]::new()))
    if ($list.Count -ne 0 -or $script:saves -ne 2) { throw 'Removing a custom size did not save' }
    $test.Close()
    'PASS: custom size dialog adds, rejects duplicates, uses the current size and removes sizes.'

    # Shared toolbar resolution across pages.
    $shared = [LandingPage.LandingPageSettings]::new(); $first = [LandingPage.Models.DashboardPages]::Ensure($shared)
    $second = [LandingPage.Models.DashboardPages]::Add($shared,'Second')
    function New-Toolbar($label) {
        $props = [LandingPage.Models.Layout.ViewProperties]::new(); $props.ViewId = 'ToolbarWidget'; $props.InstanceId = [guid]::NewGuid()
        $props.ToolbarSettings.Items.Clear(); $entry = [LandingPage.Settings.ToolbarEntry]::new(); $entry.Id = 'page.add'; $entry.Label = $label; $entry.GroupId = 'main'; $props.ToolbarSettings.Items.Add($entry)
        $props
    }
    $sourceProps = New-Toolbar 'source'; $otherProps = New-Toolbar 'other'
    $first.Layout.ViewProperties = $sourceProps; $second.Layout.ViewProperties = $otherProps
    $resolve = { param($props) [LandingPage.Models.SharedToolbar]::Effective($shared,$props.ToolbarSettings,$props.InstanceId) }
    if (![object]::ReferenceEquals((& $resolve $otherProps),$otherProps.ToolbarSettings)) { throw 'A toolbar follows another while sharing is off' }
    Set-Setting $shared 'UseSharedToolbar' $true; Set-Setting $shared 'SharedToolbarId' $sourceProps.InstanceId
    if (![object]::ReferenceEquals((& $resolve $otherProps),$sourceProps.ToolbarSettings)) { throw 'A dashboard does not show the shared toolbar' }
    if (![object]::ReferenceEquals((& $resolve $sourceProps),$sourceProps.ToolbarSettings)) { throw 'The shared toolbar does not show its own settings' }
    $page = $null; $found = [LandingPage.Models.SharedToolbar]::FindSource($shared,[ref]$page)
    if (![object]::ReferenceEquals($found,$sourceProps) -or $page.Id -ne $first.Id) { throw 'The shared toolbar source or its page was not found' }
    $otherProps.ToolbarSettings.KeepOwnToolbar = $true
    if (![object]::ReferenceEquals((& $resolve $otherProps),$otherProps.ToolbarSettings)) { throw 'Keeping its own toolbar did not take precedence' }
    $otherProps.ToolbarSettings.KeepOwnToolbar = $false
    Set-Setting $shared 'SharedToolbarId' ([guid]::NewGuid())
    if (![object]::ReferenceEquals((& $resolve $otherProps),$otherProps.ToolbarSettings)) { throw 'A missing shared toolbar did not fall back to the own toolbar' }
    Set-Setting $shared 'SharedToolbarId' $sourceProps.InstanceId
    $profile = [LandingPage.Models.DashboardProfiles]::Capture($shared,'Shared toolbar')
    $restored = [LandingPage.LandingPageSettings]::new(); [void][LandingPage.Models.DashboardPages]::Ensure($restored)
    [LandingPage.Models.DashboardProfiles]::ApplyData($restored,[LandingPage.Models.DashboardProfiles]::Clone($profile.Snapshot))
    if (!(Get-Setting $restored 'UseSharedToolbar') -or (Get-Setting $restored 'SharedToolbarId') -ne $sourceProps.InstanceId) { throw 'Dashboard profiles did not carry the shared toolbar setting' }
    'PASS: sharing off, shared source, followers, keep-own opt-out, missing source fallback, source page and profile round-trip.'

    # Editor host: states, sharing from a follower and keeping its own toolbar.
    $editor = [LandingPage.Controls.SharedToolbarEditor]::new(); $editor.GlobalSettings = $shared; $editor.OwnSettings = $otherProps.ToolbarSettings; $editor.InstanceId = $otherProps.InstanceId
    [void]$view.Content.Children.Add($editor); Pump-For 80
    if (![object]::ReferenceEquals($editor.Editor.DataContext,$sourceProps.ToolbarSettings) -or $editor.Status.Text -notmatch 'shared toolbar from "Main"' -or $editor.KeepOwnToggle.Visibility -ne 'Visible') { throw 'A following toolbar editor does not edit the shared toolbar' }
    $editor.KeepOwnToggle.IsChecked = $true; $editor.KeepOwnToggle.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent)); Pump-For 30
    if (!$otherProps.ToolbarSettings.KeepOwnToolbar -or ![object]::ReferenceEquals($editor.Editor.DataContext,$otherProps.ToolbarSettings)) { throw 'Keeping the own toolbar did not switch the editor back' }
    $editor.MakeSource(); Pump-For 30
    if ((Get-Setting $shared 'SharedToolbarId') -ne $otherProps.InstanceId -or $otherProps.ToolbarSettings.KeepOwnToolbar -or $editor.ShareButton.Visibility -ne 'Collapsed' -or $editor.Status.Text -notmatch 'shared with every dashboard') { throw 'Making this the shared toolbar did not update the source' }
    $editor.StopButton.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent)); Pump-For 30
    if ((Get-Setting $shared 'UseSharedToolbar') -or $editor.Status.Text -notmatch 'own toolbar') { throw 'Stop sharing did not turn sharing off' }
    [void]$view.Content.Children.Remove($editor)
    'PASS: editor host shows the shared toolbar state, edits the shared toolbar, keeps its own and changes the source.'

    # Toolbar widget renders the shared toolbar's items.
    $global = $settingsModel.Settings
    $remotePage = [LandingPage.Models.DashboardPages]::Add($global,'Shared source'); $remoteProps = New-Toolbar 'Shared button'
    $remoteProps.ToolbarSettings.Items[0].Id = 'page.manage'; $remotePage.Layout.ViewProperties = $remoteProps
    Set-Setting $global 'UseSharedToolbar' $true; Set-Setting $global 'SharedToolbarId' $remoteProps.InstanceId; Pump-For 100
    $effective = [LandingPage.Views.ToolbarWidgetView].GetMethod('EffectiveSettings',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($toolbar,@())
    if (![object]::ReferenceEquals($effective,$remoteProps.ToolbarSettings)) { throw 'The dashboard toolbar does not use the shared toolbar settings' }
    Render-Toolbar $toolbar; Pump-For 50
    $rendered = @(Get-Descendants $toolbar | Where-Object { $_ -is [Windows.Controls.Button] })
    if ($rendered.Count -ne 1 -or $rendered[0].ToolTip -ne 'Shared button') { throw "The dashboard toolbar did not render the shared toolbar's items" }
    Set-Setting $global 'UseSharedToolbar' $false; Pump-For 100
    if (![object]::ReferenceEquals([LandingPage.Views.ToolbarWidgetView].GetMethod('EffectiveSettings',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($toolbar,@()),$toolbar.Settings)) { throw 'Turning sharing off did not restore the dashboard toolbar' }
    'PASS: the toolbar widget renders the shared toolbar and returns to its own when sharing is off.'

'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
