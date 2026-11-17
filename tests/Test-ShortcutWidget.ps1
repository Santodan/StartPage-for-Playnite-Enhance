# Run -STA. Reuse only the in-memory API/window setup and cleanup, not the full dashboard suite.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$shortcutTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $shortcutTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    foreach($type in @([Windows.Controls.TextBox],[Windows.Controls.Expander],[Windows.Controls.CheckBox],[Windows.Controls.ComboBox],[Windows.Controls.Button])) {
        if(!$app.Resources.Contains($type)) { $app.Resources.Add($type,[Windows.Style]::new($type)) }
    }
    function Flush-Shortcuts { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout() }
    $catalog = @($plugin.GetAvailableStartPageViews().Views | Where-Object ViewId -eq 'ShortcutWidget')
    if($catalog.Count -ne 1 -or !$catalog[0].AllowMultipleInstances) { throw 'Shortcut widget missing or single-instance only' }
    $properties = [LandingPage.Models.Layout.ViewProperties]::new(); $properties.ViewId = 'ShortcutWidget'
    $shortcuts = $properties.ShortcutSettings
    $website = [LandingPage.Settings.ShortcutEntry]::new(); $website.Kind = 'Website'; $website.Label = 'Project'; $website.Target = 'https://example.com/path'
    $folder = [LandingPage.Settings.ShortcutEntry]::new(); $folder.Kind = 'Folder'; $folder.Label = 'Folder'; $folder.Target = $tempRoot
    $missing = [LandingPage.Settings.ShortcutEntry]::new(); $missing.Kind = 'Application'; $missing.Target = (Join-Path $tempRoot 'missing.exe')
    $shortcuts.Items.Add($website); $shortcuts.Items.Add($folder); $shortcuts.Items.Add($missing)
    $shortcuts.CustomBackground = $true; $shortcuts.CustomBorder = $true; $shortcuts.CustomTextColor = $true; $shortcuts.TextColor = '#45AABB'; $shortcuts.FontSize = 23
    # Use a missing explicit image to avoid network access in this general UI fixture.
    # Website fetching is exercised with an injected HTTP handler in Test-WebsiteIcons.ps1.
    $website.IconPath = Join-Path $tempRoot 'missing-icon.png'
    $widget = $plugin.GetStartPageView('ShortcutWidget',[guid]::NewGuid())
    $node = [LandingPage.Models.Layout.GridNode]::new(); $node.ViewProperties = $properties
    $model = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($node); $model.IsLeaf = $true; $model.HasView = $true
    [void]$model.View.Children.Add($widget)
    $hostPanel = [LandingPage.Views.GridNodeView]::new(); $hostPanel.DataContext = $model; [void]$layout.Children.Add($hostPanel); Flush-Shortcuts
    if(![object]::ReferenceEquals($widget.Settings,$shortcuts)) { throw 'Shortcut ancestor settings binding failed' }
    $buttons = @([ToolbarTestVisuals]::Buttons($widget))
    if($buttons.Count -ne 3 -or !$buttons[0].IsEnabled -or !$buttons[1].IsEnabled -or $buttons[2].IsEnabled) { throw 'Targets not validated or missing targets remain active' }
    $folderImage = $buttons[1].Content.Child.Children[0]
    if($folderImage -isnot [Windows.Controls.Image] -or ![object]::ReferenceEquals($folderImage.Source,[LandingPage.Utils.ShortcutIcons]::Folder)) { throw 'Folder shortcut does not render the Windows folder icon' }
    $text = @([ToolbarTestVisuals]::Text($widget) | Where-Object Text -eq 'Project')[0]
    if($text.FontSize -ne 23 -or $text.Foreground.Color.ToString() -ne '#FF45AABB') { throw 'Shortcut typography or custom color ignored' }
    $iconFile = Join-Path $tempRoot 'shortcut.png'
    Copy-Item -LiteralPath (Join-Path $root 'source\StartPage\icon.png') -Destination $iconFile
    $website.IconPath = $iconFile; Flush-Shortcuts
    # Bitmap decoding must close its stream so editing/replacing an icon works.
    $iconStream = [IO.File]::Open($iconFile,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None); $iconStream.Dispose()
    $website.IconPath = Join-Path $tempRoot 'missing-icon.png'; Flush-Shortcuts
    [IO.File]::WriteAllText($iconFile,'not an image'); $website.IconPath = $iconFile; Flush-Shortcuts
    if(@([ToolbarTestVisuals]::Buttons($widget)).Count -ne 3) { throw 'Invalid custom icon broke shortcut rendering' }
    $website.IconPath = Join-Path $tempRoot 'missing-icon.png'; Flush-Shortcuts
    foreach($mode in @('Horizontal','Vertical','Wrap')) {
        $shortcuts.Layout = $mode; $shortcuts.IconAbove = $true; Flush-Shortcuts
        if(@([ToolbarTestVisuals]::Buttons($widget)).Count -ne 3) { throw 'Layout change lost shortcuts' }
    }
    # Invoking the actual click handler in Edit Mode must not open a website or process.
    $model.EditModeEnabled = $true
    $buttons = @([ToolbarTestVisuals]::Buttons($widget)); $buttons[0].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $model.EditModeEnabled = $false
    $copy = [LandingPage.Models.DashboardProfiles]::Clone($properties)
    $copy.ShortcutSettings.Items[0].Label = 'Changed copy'
    if($website.Label -ne 'Project' -or $copy.ShortcutSettings.Items.Count -ne 3 -or $copy.ShortcutSettings.TextColor -ne '#45AABB') { throw 'Profile clone lost data or shares shortcut objects' }
    $shortcuts.ShowIcons = $false; $shortcuts.ShowLabels = $false; Flush-Shortcuts
    if(!@([ToolbarTestVisuals]::Text($widget) | Where-Object Text -eq 'Project').Count) { throw 'Hiding both content types produced invisible controls' }
    'PASS: registration, real ancestor binding, all layouts, custom colors/styles, missing targets, edit-mode click suppression and independent profile snapshots.'

    $editor = [LandingPage.Views.Settings.ShortcutWidgetSettingsView]::new(); $editor.DataContext = $shortcuts; [void]$view.Content.Children.Add($editor); Flush-Shortcuts
    $editor.FindName('EntrySelector').SelectedIndex = 0
    $click = [Windows.RoutedEventArgs]::new()
    $editor.GetType().GetMethod('Duplicate_Click',$flags).Invoke($editor,@($null,$click)); Flush-Shortcuts
    if($shortcuts.Items.Count -ne 4 -or $shortcuts.Items[0].Id -eq $shortcuts.Items[1].Id) { throw 'Duplicate does not make an independent shortcut' }
    $duplicate = $editor.FindName('EntrySelector').SelectedItem
    $editor.GetType().GetMethod('Down_Click',$flags).Invoke($editor,@($null,$click)); Flush-Shortcuts
    if($shortcuts.Items[2] -ne $duplicate) { throw 'Reordering shortcut failed' }
    $editor.GetType().GetMethod('Remove_Click',$flags).Invoke($editor,@($null,$click)); Flush-Shortcuts
    if($shortcuts.Items.Count -ne 3) { throw 'Remove shortcut failed' }
    $editor.GetType().GetMethod('Add_Click',$flags).Invoke($editor,@($null,$click)); Flush-Shortcuts
    $editor.FindName('EntrySelector').SelectedItem.Kind = 'Website'; Flush-Shortcuts
    if($editor.FindName('ApplicationFields').Visibility -ne 'Collapsed' -or $editor.FindName('BrowseTargetButton').Visibility -ne 'Collapsed') { throw 'Website editor exposes application-only controls' }
    $replacement = [LandingPage.Settings.ShortcutWidgetSettings]::new(); $properties.ShortcutSettings = $replacement; Flush-Shortcuts
    if(![object]::ReferenceEquals($widget.Settings,$replacement)) { throw 'Settings reset failed to refresh shortcuts' }
    [void]$view.Content.Children.Remove($editor); [void]$layout.Children.Remove($hostPanel); Flush-Shortcuts
    if($widget.GetType().GetField('observed',$flags).GetValue($widget) -or $editor.GetType().GetField('observed',$flags).GetValue($editor)) { throw 'Hidden shortcut widget/editor retains settings listeners' }
    'PASS: real editor add/duplicate/reorder/remove, contextual fields, settings reset and unload cleanup.'

    $info = $null; $errorText = $null
    $application = [LandingPage.Settings.ShortcutEntry]::new(); $application.Kind = 'Application'
    $application.Target = Join-Path $tempRoot 'application with spaces.exe'; [IO.File]::WriteAllText($application.Target,'fixture - never executed')
    $application.Arguments = '--name "a b"'; $application.WorkingDirectory = $tempRoot
    if(![LandingPage.Utils.ShortcutLauncher]::TryCreateStartInfo($application,[ref]$info,[ref]$errorText) -or $info.FileName -ne $application.Target -or $info.Arguments -ne $application.Arguments -or $info.WorkingDirectory -ne $tempRoot) { throw 'Application path/arguments were combined or changed' }
    foreach($target in @('javascript:alert(1)','file:///C:/test','mailto:test@example.com','relative-url')) {
        $website.Target = $target
        if([LandingPage.Utils.ShortcutLauncher]::TryCreateStartInfo($website,[ref]$info,[ref]$errorText)) { throw ('Invalid website accepted: '+$target) }
    }
    $website.Target = 'https://example.com'; $website.Enabled = $false
    if([LandingPage.Utils.ShortcutLauncher]::TryCreateStartInfo($website,[ref]$info,[ref]$errorText)) { throw 'Disabled shortcut can launch' }
    $folder.Target = 'relative-folder'
    if([LandingPage.Utils.ShortcutLauncher]::TryCreateStartInfo($folder,[ref]$info,[ref]$errorText)) { throw 'Relative folder accepted' }
    'PASS: launch-info validation, separate application arguments, unsupported website protocols, disabled entries and absolute paths; no processes launched.'
'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
