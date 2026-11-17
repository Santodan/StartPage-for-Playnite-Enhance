# Run -STA against temporary settings and cached in-memory widgets only.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$mergeTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $mergeTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    function Merge-Cell($name,$weight=1) {
        $node = [LandingPage.Models.Layout.GridNode]::new(); $node.Size = [Windows.GridLength]::new($weight,[Windows.GridUnitType]::Star)
        if($name) {
            $node.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $node.ViewProperties.PluginId = $plugin.Id; $node.ViewProperties.ViewId = 'TextWidget'; $node.ViewProperties.TextSettings.Text = $name
            $control = [Windows.Controls.TextBlock]::new(); $control.Text = $name
            [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($node.ViewProperties,$control)
        }; $node
    }
    function Merge-Group($orientation,$children,$weight=1) {
        $node = [LandingPage.Models.Layout.GridNode]::new(); $node.Orientation = $orientation; $node.Size = [Windows.GridLength]::new($weight,[Windows.GridUnitType]::Star)
        foreach($child in $children) { $node.Children.Add($child) }; $node
    }
    function Find-MergeModel($model,$node) {
        if($model.GridNode -eq $node) { return $model }
        foreach($child in $model.View.Children) { if($child -is [LandingPage.Views.GridNodeView]) { $found = Find-MergeModel $child.DataContext $node; if($found) { return $found } } }
    }
    $source = Merge-Cell 'Keep divider'; $empty = Merge-Cell $null
    $lowerLeft = Merge-Group 'Horizontal' @((Merge-Cell 'Left A'),(Merge-Cell 'Left B')) 3
    $lowerRight1 = Merge-Cell 'Right A' 2; $lowerRight2 = Merge-Cell 'Right B'
    $left = Merge-Group 'Vertical' @($source,$lowerLeft) 2
    $right = Merge-Group 'Vertical' @($empty,$lowerRight1,$lowerRight2) 3
    $unaffected = Merge-Cell 'Third column' 2
    $columns = Merge-Group 'Horizontal' @($left,$right,$unaffected)
    $toolbar = Merge-Cell 'Fixed toolbar'; $toolbar.Size = [Windows.GridLength]::Auto
    $rootNode = Merge-Group 'Vertical' @($toolbar,$columns)
    $keptNodes = @($source,$lowerLeft,$lowerLeft.Children[0],$lowerLeft.Children[1],$lowerRight1,$lowerRight2,$unaffected,$toolbar)
    $controls = @($keptNodes | Where-Object ViewProperties | ForEach-Object { [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).GetValue($_.ViewProperties) })
    $rootModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($rootNode); $rootModel.EditModeEnabled = $true; Wait-Task ($rootModel.InititalizeGrid())
    $mergeHost = [LandingPage.Views.GridNodeView]::new(); $mergeHost.DataContext = $rootModel; $mergeHost.Width = 900; $mergeHost.Height = 500
    [void]$view.Content.Children.Add($mergeHost); $window.UpdateLayout()
    $sourceModel = Find-MergeModel $rootModel $source
    if(!$sourceModel.MergeRightSpaceCommand.CanExecute($null)) { throw 'Top panel cannot merge into matching empty space across nested columns' }
    $sourceModel.MergeRightSpaceCommand.Execute($null)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    $remaining = @($rootNode.Enumerate())
    foreach($node in $keptNodes) { if($remaining -notcontains $node) { throw 'Merge removed a surrounding panel or widget' } }
    if($remaining -contains $empty -or $rootNode.ViewCount -ne $controls.Count -or $rootNode.Children[0] -ne $toolbar -or !$toolbar.Size.IsAuto) { throw 'Merge lost widgets or fixed toolbar sizing' }
    foreach($control in $controls) { if(!$control.Parent) { throw 'Retained widget control was discarded instead of reused' } }
    if($columns.Children.Count -ne 2 -or $columns.Children[0].Children[0] -ne $source -or $columns.Children[1] -ne $unaffected) { throw 'Merge did not isolate its two-column band' }
    $profile = [LandingPage.Models.DashboardProfiles]::Clone($rootNode)
    if($profile.ViewCount -ne $rootNode.ViewCount -or $profile.Children[1].Children[0].Children[0].ViewProperties.TextSettings.Text -ne 'Keep divider') { throw 'Merged layout did not persist' }
    [void]$view.Content.Children.Remove($mergeHost)
    'PASS: real directional merge across nested columns, unequal lower subdivisions, surrounding panel identities, fixed toolbar, cached controls and profile persistence.'

    foreach($orientation in @('Horizontal','Vertical')) {
        foreach($next in @($true,$false)) {
            $cross = if($orientation -eq 'Horizontal') { 'Vertical' } else { 'Horizontal' }
            $beforeA = Merge-Cell 'Before A'; $beforeB = Merge-Cell 'Before B'; $afterA = Merge-Cell 'After A'; $afterB = Merge-Cell 'After B'
            $current = Merge-Cell 'Selected'; $blank = Merge-Cell $null
            $a = Merge-Group $cross @($beforeA,$current,$afterA); $b = Merge-Group $cross @($beforeB,$blank,$afterB)
            $owner = if($next) { Merge-Group $orientation @($a,$b) } else { Merge-Group $orientation @($b,$a) }
            $plan = [LandingPage.Models.Layout.AdjacentPanelMerge]::Find($owner,$current,[Windows.Controls.Orientation]::$orientation,$next)
            if(!$plan) { throw 'Middle-band directional plan missing' }; $plan.Apply()
            $remaining = @($owner.Enumerate())
            foreach($cell in @($beforeA,$beforeB,$afterA,$afterB,$current)) { if($remaining -notcontains $cell) { throw 'Middle-band merge lost a preceding/following widget' } }
            if($remaining -contains $blank -or $owner.ViewCount -ne 5) { throw 'Middle-band merge discarded more than its empty target' }
        }
    }
    $current = Merge-Cell $null; $keep = Merge-Cell 'Neighbor'; $owner = Merge-Group 'Horizontal' @($current,$keep)
    $plan = [LandingPage.Models.Layout.AdjacentPanelMerge]::Find($owner,$current,[Windows.Controls.Orientation]::Horizontal,$true)
    if($plan.Kept -ne $keep) { throw 'Merging from an empty panel does not retain the neighbor widget' }; $plan.Apply()
    $current = Merge-Cell 'Current'; $other = Merge-Cell 'Occupied'; $owner = Merge-Group 'Horizontal' @($current,$other)
    $plan = [LandingPage.Models.Layout.AdjacentPanelMerge]::Find($owner,$current,[Windows.Controls.Orientation]::Horizontal,$true)
    if($plan.Removed -ne $other -or $plan.Kept -ne $current) { throw 'Occupied-target confirmation cannot identify the widget being removed' }
    $fixedA = Merge-Cell 'Fixed'; $fixedA.Size = [Windows.GridLength]::new(120)
    $fixedB = Merge-Cell $null; $fixedB.Size = [Windows.GridLength]::new(80)
    $fixedOwner = Merge-Group 'Horizontal' @($fixedA,$fixedB)
    $fixedPlan = [LandingPage.Models.Layout.AdjacentPanelMerge]::Find($fixedOwner,$fixedA,[Windows.Controls.Orientation]::Horizontal,$true); $fixedPlan.Apply()
    if(!$fixedA.Size.IsAbsolute -or $fixedA.Size.Value -ne 205) { throw 'Fixed-size merge lost the removed splitter width' }
    $mismatch = Merge-Group 'Horizontal' @((Merge-Group 'Vertical' @((Merge-Cell 'Top'),(Merge-Cell 'Bottom'))),(Merge-Cell $null))
    if([LandingPage.Models.Layout.AdjacentPanelMerge]::Find($mismatch,$mismatch.Children[0].Children[0],[Windows.Controls.Orientation]::Horizontal,$true)) { throw 'Nonrectangular merge was incorrectly offered' }
    'PASS: all four directions, middle bands, keeping populated neighbors, explicit occupied-target removal and rejection of incompatible rectangles.'
'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
