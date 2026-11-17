# powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File tests/Test-AchievementWidgets.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'source\StartPage\bin\Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Gu.Wpf.NumericInput','Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','SqlNado','StartPagePlugin')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll')))
}
$assembly = [LandingPage.Views.AchievementWidgetView].Assembly
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$static = [Reflection.BindingFlags]'Static,Public,NonPublic'
function Field($obj,$name,$value) { $obj.GetType().GetField($name,$flags).SetValue($obj,$value) }
function Render($view) { [void]$view.GetType().GetMethod('Render',([Reflection.BindingFlags]'Instance,NonPublic,DeclaredOnly')).Invoke($view,@()); return $view.Content.Content }
function Texts($node) {
    if ($node -is [Windows.Controls.TextBlock]) { $node.Text }
    if ($node -is [Windows.Controls.Panel]) { foreach ($child in $node.Children) { Texts $child } }
    elseif ($node -is [Windows.Controls.Border]) { Texts $node.Child }
}
$progressType = $assembly.GetType('LandingPage.Models.ShelfAchievementProgress')
$dictType = [Collections.Generic.Dictionary`2].MakeGenericType([guid],$progressType)
$data = [Activator]::CreateInstance($dictType)
$games = [Collections.Generic.List[Playnite.SDK.Models.Game]]::new()
foreach ($entry in @(@('Alpha',8,10),@('Beta',10,10),@('Gamma',0,20),@('Hidden',9,10))) {
    $game = [Playnite.SDK.Models.Game]::new(); $game.Id = [guid]::NewGuid(); $game.Name = $entry[0]; $game.Hidden = $game.Name -eq 'Hidden'
    $games.Add($game)
    $progress = [Activator]::CreateInstance($progressType)
    $progress.Total = $entry[2]; $progress.Unlocked = $entry[1]; $data.Add($game.Id,$progress)
}
$views = @()
try {
    foreach ($id in @('AchievementProgress','AchievementFinishNext','AchievementGoals')) {
        $view = [LandingPage.Views.AchievementWidgetView]::new($id); $views += $view
        $view.Settings.ShowArtwork = $false
        Field $view 'visible' $true; Field $view 'games' $games; Field $view 'progress' $data
        $panel = Render $view
        if ($id -eq 'AchievementProgress') {
            if ($panel.Children.Count -ne 3 -or $panel -isnot [Windows.Controls.WrapPanel]) { throw 'Progress cards should exclude hidden games and include completed games' }
            $view.Settings.MinimumPercent = 75; $view.Settings.MaximumRemaining = 2
            if ((Render $view).Children.Count -ne 2) { throw 'Progress/remaining filters did not apply' }
            $view.Settings.TitleSize = 29; $view.Settings.DetailSize = 17; $view.Settings.CustomTextColors = $true; $view.Settings.TitleColor = '#FF8800'
            $view.Settings.CardWidth = 333; $view.Settings.BorderWidth = 4; $view.Settings.ProgressStyle = [LandingPage.Settings.AchievementProgressStyle]::Both
            $panel = Render $view; $card = $panel.Children[0]
            if ($card.Width -ne 333 -or $card.BorderThickness.Left -ne 4) { throw 'Independent card appearance did not apply' }
            $countsStyle = $view.Settings.TextStyles['Counts']; $countsStyle.UseDefaults = $false; $countsStyle.Size = 22; $countsStyle.Bold = $true
            $percentStyle = $view.Settings.TextStyles['Percentage']; $percentStyle.UseDefaults = $false; $percentStyle.Size = 19; $percentStyle.Italic = $true
            $card = (Render $view).Children[0]
            $inline = @($card.Child.Children[0].Children[0].Children[1].Inlines)
            if ($inline[0].FontSize -ne 22 -or $inline[0].FontWeight -ne [Windows.FontWeights]::Bold -or $inline[2].FontSize -ne 19 -or $inline[2].FontStyle -ne [Windows.FontStyles]::Italic) { throw 'Independent count/percentage text styles were not applied to the actual card text' }
            $title = $card.Child.Children[0].Children[0].Children[0]
            if ($title.FontSize -ne 29 -or $title.Foreground.Color.ToString() -ne '#FFFF8800') { throw 'Text customization must work without general appearance enabled' }
        }
        elseif ($id -eq 'AchievementFinishNext') {
            if ($panel.Children.Count -ne 2 -or ((Texts $panel) -join '|') -match 'Beta|Hidden') { throw 'Finish next includes completed/hidden games' }
            if (((Texts $panel.Children[0]) -join '|') -notmatch 'Alpha') { throw 'Finish next does not prioritize fewest remaining' }
            $view.Settings.DetailBold = $true; $view.Settings.DetailItalic = $true; $view.Settings.DetailSize = 20
            $panel = Render $view; $runs = @($panel.Children[0].Child.Children[0].Children[0].Children[1].Inlines)
            if ($runs[0].FontSize -ne 20 -or $runs[0].FontWeight -ne [Windows.FontWeights]::Bold -or $runs[0].FontStyle -ne [Windows.FontStyles]::Italic) { throw 'Finish-next metadata does not respect the independent shared detail style' }

        }
        else {
            if (((Texts $panel) -join '|') -notmatch 'Add games') { throw 'Empty goal state is missing' }
            $goal = [LandingPage.Settings.AchievementGoal]::new(); $goal.GameId = $games[0].Id; $goal.GameName = 'Alpha'; $goal.TargetPercent = 75
            $view.Settings.Goals.Add($goal)
            $panel = Render $view
            if ($panel.Children.Count -ne 1 -or ((Texts $panel) -join '|') -notmatch 'reached') { throw 'Per-game percentage target was not applied' }
            $goalStyle = $view.Settings.TextStyles['Goal']; $goalStyle.UseDefaults = $false; $goalStyle.Size = 27; $goalStyle.Bold = $true
            $targetText = (Render $view).Children[0].Child.Children[0].Children[0].Children[2]
            if ($targetText.FontSize -ne 27 -or $targetText.FontWeight -ne [Windows.FontWeights]::Bold) { throw 'Goal target formatting was not applied' }
            $goal.TargetPercent = 90
            if (((Texts (Render $view)) -join '|') -match 'reached') { throw 'Unreached target was incorrectly completed' }
        }
    }
    $sourceSettings = $views[2].Settings
    $selectedId = $games[0].Id; $hoveredId = $games[1].Id
    $sourceSettings.GameSource = [LandingPage.Settings.AchievementGoalSource]::SelectedGame
    if ($sourceSettings.ResolveGoalGame($selectedId,$hoveredId) -ne $selectedId) { throw 'Selected goal source did not select the library game' }
    $sourceSettings.GameSource = [LandingPage.Settings.AchievementGoalSource]::HoveredGame
    if ($sourceSettings.ResolveGoalGame($selectedId,$hoveredId) -ne $hoveredId -or $sourceSettings.ResolveGoalGame($selectedId,$null)) { throw 'Hovered goal source used selection instead' }
    $sourceSettings.GameSource = [LandingPage.Settings.AchievementGoalSource]::HoveredOrSelectedGame
    if ($sourceSettings.ResolveGoalGame($selectedId,$hoveredId) -ne $hoveredId -or $sourceSettings.ResolveGoalGame($selectedId,$null) -ne $selectedId) { throw 'Combined goal source precedence is wrong' }
    $sourceSettings.TargetPercent = 65
    $view = $views[0]
    $settingsEditor = [LandingPage.Views.Settings.AchievementWidgetSettingsView]::new(); $settingsEditor.DataContext = $view.Settings
    $settingsEditor.Measure([Windows.Size]::new(400,3000)); $settingsEditor.Arrange([Windows.Rect]::new(0,0,400,3000))
    $node = [LandingPage.Models.Layout.ViewProperties]::new(); $node.ViewId = 'AchievementGoals'; $node.AchievementSettings = $views[2].Settings
    $clone = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]); $copy = $clone.Invoke($null,@($node))
    if ($copy.AchievementSettings.Goals.Count -ne 1 -or $copy.AchievementSettings.Goals[0].TargetPercent -ne 90) { throw 'Goals/settings did not survive profile cloning' }
    if ($copy.AchievementSettings.GameSource -ne [LandingPage.Settings.AchievementGoalSource]::HoveredOrSelectedGame -or $copy.AchievementSettings.TargetPercent -ne 65) { throw 'Goal source/default target did not persist' }
    $copy.AchievementSettings.Goals[0].TargetPercent = 20
    if ($node.AchievementSettings.Goals[0].TargetPercent -ne 90) { throw 'Independent snapshots share goals' }
    $node.ViewId = 'DigitalClock'
    if ($node.ShouldSerializeAchievementSettings()) { throw 'Unrelated widgets should not export achievement settings' }
    # Bulk SuccessStory snapshot is cached and never writes the provider data.
    $tempRoot = Join-Path $env:TEMP ('StartPage-achievement-tests-' + [guid]::NewGuid())
    $folder = Join-Path $tempRoot 'cebe6d32-8c46-4459-b993-5a5189d60788\SuccessStory'
    [void][IO.Directory]::CreateDirectory($folder)
    $path = Join-Path $folder ($games[0].Id.ToString() + '.json')
    [IO.File]::WriteAllText($path,'{"Total":10,"Unlocked":8}')
    try {
        $reader = $assembly.GetType('LandingPage.Models.ShelfAchievements').GetMethod('GetSnapshotAsync',$static)
        $first = $reader.Invoke($null,@([LandingPage.Settings.AchievementsProvider]::SuccessStory,[string]$tempRoot))
        $second = $reader.Invoke($null,@([LandingPage.Settings.AchievementsProvider]::SuccessStory,[string]$tempRoot))
        $result = $first.GetAwaiter().GetResult()
        if (![object]::ReferenceEquals($first,$second) -or $result[$games[0].Id].Unlocked -ne 8) { throw 'Provider snapshot was not shared or counts were incorrect' }
        if ([IO.File]::ReadAllText($path) -ne '{"Total":10,"Unlocked":8}') { throw 'Provider cache was modified' }
        $listReader = $assembly.GetType('LandingPage.Models.GameAchievementList').GetMethod('Read',$static)
        [IO.File]::WriteAllText($path,'{"Items":[{"Name":"Earned","Description":"Description","DateUnlocked":"2026-01-01T12:00:00Z","UrlUnlocked":"https://example.com/icon.png"},{"Name":"Locked","DateUnlocked":null}]}')
        $list = $listReader.Invoke($null,@([string]$tempRoot,[LandingPage.Settings.AchievementsProvider]::SuccessStory,$games[0].Id))
        if ($list.Count -ne 2 -or $list[0].Unlocked -ne 1 -or $list[1].Unlocked -ne 0) { throw 'SuccessStory list lost locked/unlocked definitions' }
        $options = [LandingPage.Settings.AchievementWidgetSettings]::Create('GameAchievementList')
        if ($options.GameSource -ne [LandingPage.Settings.AchievementGoalSource]::HoveredOrSelectedGame -or $options.Limit -ne 0) { throw 'List widget should follow hover/selection and show all by default' }
        $listView = [LandingPage.Views.AchievementWidgetView]::new('GameAchievementList'); $views += $listView
        $listView.Settings.ShowArtwork = $false; $listView.Settings.TitleSize = 25; $listView.Settings.ShowDescription = $true
        $rowMethod = $listView.GetType().GetMethod('AchievementRow',$flags)
        foreach ($roleName in @('Description','GameName','UnlockState','UnlockDate')) {
            $style = $listView.Settings.TextStyles[$roleName]; $style.UseDefaults = $false; $style.Size = 21; $style.Bold = $true; $style.Italic = $true
        }
        $row = $rowMethod.Invoke($listView,@('Fixture game',$list[0]))
        $rowText = (Texts $row) -join '|'
        $fields = $row.Child.Children[0].Children
        foreach ($index in @(1,2,3,4)) { if ($fields[$index].FontSize -ne 21 -or $fields[$index].FontWeight -ne [Windows.FontWeights]::Bold -or $fields[$index].FontStyle -ne [Windows.FontStyles]::Italic) { throw 'Game achievement text role did not receive its own formatting' } }

        if ($rowText -notmatch 'Earned' -or $rowText -notmatch 'Description' -or $rowText -notmatch 'Unlocked') { throw 'Achievement list row lost configured fields' }
        $listView.Settings.ShowDescription = $false; $listView.Settings.ShowUnlockDate = $false; $listView.Settings.ShowUnlockState = $false
        $row = $rowMethod.Invoke($listView,@('Fixture game',$list[0]))
        if (((Texts $row) -join '|') -match 'Description|Unlocked') { throw 'Achievement list field toggles did not apply' }
        # Row mode must realize only the visible subset, not load every achievement/icon.
        $rowType = $assembly.GetType('LandingPage.Views.AchievementWidgetView+AchievementListRow')
        $rowsType = [Collections.Generic.List`1].MakeGenericType($rowType)
        $manyRows = [Activator]::CreateInstance($rowsType)
        for ($i=0; $i -lt 1000; $i++) {
            $item = [Activator]::CreateInstance($rowType,$true)
            $rowType.GetField('GameName',$flags).SetValue($item,'Fixture game')
            $rowType.GetField('Entry',$flags).SetValue($item,$list[0]); $manyRows.Add($item)
        }
        $virtual = $listView.GetType().GetMethod('CreateVirtualList',$flags).Invoke($listView,(,$manyRows))
        $virtualWindow = [Windows.Window]::new(); $virtualWindow.Width = 500; $virtualWindow.Height = 350; $virtualWindow.Content = $virtual
        try {
            $virtualWindow.Show(); $virtualWindow.UpdateLayout()
            if (!$virtual.ItemContainerGenerator.ContainerFromIndex(0) -or $virtual.ItemContainerGenerator.ContainerFromIndex(999)) { throw 'Achievement rows are not virtualized' }
            $virtual.ScrollIntoView($manyRows[999]); $virtualWindow.UpdateLayout()
            if (!$virtual.ItemContainerGenerator.ContainerFromIndex(999)) { throw 'Virtual list cannot reach its final achievement' }
        } finally { $virtualWindow.Content = $null; $virtualWindow.Close() }
        'PASS: 1,000 achievement rows realize only visible content and scroll to the final achievement.'
        $options.ListFilter = [LandingPage.Settings.AchievementListFilter]::Locked
        $filter = [LandingPage.Views.AchievementWidgetView].GetMethod('FilterEntries',$static)
        $filtered = @($filter.Invoke($null,@($list,$options)))
        if ($filtered.Count -ne 1 -or $filtered[0].Name -ne 'Locked') { throw 'Locked achievement filter is incorrect' }
        $databasePath = Join-Path $tempRoot 'achievement_cache.db'
        $db = [SqlNado.SQLiteDatabase]::new($databasePath)
        try {
            foreach ($sql in @(
                'CREATE TABLE Games (Id INTEGER PRIMARY KEY, PlayniteGameId TEXT)',
                'CREATE TABLE AchievementDefinitions (Id INTEGER PRIMARY KEY, GameId INTEGER, DisplayName TEXT, Description TEXT, UnlockedIconPath TEXT, LockedIconPath TEXT)',
                'CREATE TABLE UserGameProgress (Id INTEGER PRIMARY KEY, GameId INTEGER, CacheKey TEXT)',
                'CREATE TABLE UserAchievements (AchievementDefinitionId INTEGER, UserGameProgressId INTEGER, Unlocked INTEGER, UnlockTimeUtc TEXT)',
                'INSERT INTO AchievementDefinitions VALUES (1,1,''Earned'',''Description'',''earned.png'',''locked.png'')',
                'INSERT INTO AchievementDefinitions VALUES (2,1,''Locked'',''Still locked'',''earned2.png'',''locked2.png'')',
                'INSERT INTO UserGameProgress VALUES (1,1,NULL)',
                'INSERT INTO UserAchievements VALUES (1,1,1,''2026-01-01T12:00:00Z'')'
            )) { [void]$db.ExecuteNonQuery($sql,[object[]]@()) }
            [void]$db.ExecuteNonQuery('INSERT INTO Games VALUES (1,?)',[object[]]@($games[0].Id.ToString()))
        } finally { $db.Dispose() }
        try {
            $provider = @([Enum]::GetValues([LandingPage.Settings.AchievementsProvider]) | Where-Object { $_ -ne [LandingPage.Settings.AchievementsProvider]::SuccessStory })[0]
            $sqlList = $listReader.Invoke($null,@([string]$tempRoot,[LandingPage.Settings.AchievementsProvider]$provider,$games[0].Id))
            if ($sqlList.Count -ne 2 -or @($sqlList | Where-Object Unlocked -eq 1).Count -ne 1 -or @($sqlList | Where-Object Name -eq 'Locked')[0].Unlocked -ne 0) { throw 'SQLite list must include definitions with no UserAchievements row' }
            if (!$sqlList[0].UnlockedIconPath.StartsWith($tempRoot)) { throw 'Relative cached icons were not resolved' }
            $secondGame = [guid]::NewGuid()
            $db = [SqlNado.SQLiteDatabase]::new($databasePath)
            try {
                [void]$db.ExecuteNonQuery('INSERT INTO Games VALUES (2,?)',[object[]]@($secondGame.ToString()))
                foreach ($sql in @(
                    'INSERT INTO AchievementDefinitions VALUES (3,2,''Other game'',''Other description'',NULL,NULL)',
                    'INSERT INTO UserGameProgress VALUES (2,2,NULL)',
                    'INSERT INTO UserAchievements VALUES (2,1,1,''2026-01-03T12:00:00Z'')',
                    'INSERT INTO UserAchievements VALUES (3,2,1,''2026-01-02T12:00:00Z'')'
                )) { [void]$db.ExecuteNonQuery($sql,[object[]]@()) }
            } finally { $db.Dispose() }
            $recentReader = $assembly.GetType('LandingPage.Models.GameAchievementList').GetMethod('ReadRecent',$static)
            $eligible = [string[]]@($games[0].Id.ToString(),$secondGame.ToString())
            $feed = $recentReader.Invoke($null,@([string]$tempRoot,[LandingPage.Settings.AchievementsProvider]$provider,$eligible,2,1,0))
            if ($feed.Count -ne 2 -or $feed[0].Name -ne 'Locked' -or $feed[1].Name -ne 'Other game') { throw 'Recent feed did not apply per-game limits before filling total' }
            $allFeed = $recentReader.Invoke($null,@([string]$tempRoot,[LandingPage.Settings.AchievementsProvider]$provider,$eligible,0,0,0))
            if ($allFeed.Count -ne 3) { throw 'Zero limits must allow all recent achievements' }
            $dayFeed = $recentReader.Invoke($null,@([string]$tempRoot,[LandingPage.Settings.AchievementsProvider]$provider,$eligible,0,0,1))
            if ($dayFeed.Count -ne 0) { throw 'Recent day range did not filter old unlocks' }
            $ssFeed = $recentReader.Invoke($null,@([string]$tempRoot,[LandingPage.Settings.AchievementsProvider]::SuccessStory,[string[]]@($games[0].Id.ToString()),0,0,0))
            if ($ssFeed.Count -ne 1 -or $ssFeed[0].Name -ne 'Earned') { throw 'SuccessStory recent feed must show only unlocked achievements' }
            'PASS: recent feeds from both providers, per-game refill, unlimited counts and date range.'

        } finally { [IO.File]::Delete($databasePath) }
        'PASS: full SuccessStory and SQLite achievement lists, locked definitions without user rows, source defaults, filters and relative icons.'

    } finally { [IO.File]::Delete($path); [IO.Directory]::Delete($folder); [IO.Directory]::Delete((Split-Path $folder)); [IO.Directory]::Delete($tempRoot) }
    'PASS: achievement card rendering, finish-next filters/order, goal targets, independent appearance, settings editor, profile cloning and shared read-only provider snapshots.'
} finally { foreach ($view in $views) { $view.OnViewClosed() } }





