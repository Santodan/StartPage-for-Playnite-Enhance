# Run after a Release build using Windows PowerShell: powershell.exe -STA -File tests/Test-AdvancedShelfRules.ps1
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildDir = Join-Path $projectRoot 'source\StartPage\bin\Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Gu.Wpf.NumericInput', 'Newtonsoft.Json', 'Playnite.SDK', 'StartPage.SDK', 'StartPage.SDK.Async', 'felixkmh.PlayniteCommon', 'StartPagePlugin')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $buildDir ($name + '.dll')))
}
$today = [datetime]'2026-11-10T12:00:00'
function Assert-Rule($game, $settings, [bool]$expected, [string]$label) {
    if ([LandingPage.Models.ShelfFilterRules]::Matches($game, $settings, $today) -ne $expected) { throw $label }
}
$game = [Playnite.SDK.Models.Game]::new()
$rules = [LandingPage.Models.ShelveProperties]::new()
Assert-Rule $game $rules $true 'Defaults should accept an ordinary game'
$rules.NeverPlayedOnly = $true
Assert-Rule $game $rules $true 'Never played should accept zero time with no activity'
$game.Playtime = 1
Assert-Rule $game $rules $false 'Recorded playtime is not never played'
$game.Playtime = 0; $game.LastActivity = $today.AddDays(-90)
Assert-Rule $game $rules $false 'Recorded activity is not never played'
$rules.ResetAdvancedFilters(); $rules.UseInactivityRule = $true; $rules.InactiveDays = 90
Assert-Rule $game $rules $true 'Inactivity cutoff day should be included'
$game.LastActivity = $today.AddDays(-89)
Assert-Rule $game $rules $false 'Recently played game should be excluded'
$game.LastActivity = $null
Assert-Rule $game $rules $false 'Never played is omitted from inactivity unless selected'
$rules.IncludeNeverPlayedInInactivity = $true
Assert-Rule $game $rules $true 'Never played option should work'
$game.Playtime = 1
Assert-Rule $game $rules $false 'Unknown last activity with playtime is not never played'
$rules.ResetAdvancedFilters(); $rules.UseMinimumAddedAge = $true; $rules.MinimumAddedDays = 7
$rules.UseMaximumAddedAge = $true; $rules.MaximumAddedDays = 30
foreach ($days in @(7, 15, 30)) {
    $game.Added = $today.AddDays(-$days)
    Assert-Rule $game $rules $true ('Added range should include day ' + $days)
}
foreach ($days in @(-1, 0, 6, 31)) {
    $game.Added = $today.AddDays(-$days)
    Assert-Rule $game $rules $false ('Added range should omit day ' + $days)
}
$game.Added = $null
Assert-Rule $game $rules $false 'Missing added date should not match a date range'
$rules.MinimumAddedDays = 31; $game.Added = $today.AddDays(-30)
Assert-Rule $game $rules $false 'Contradictory range should match nothing'
$rules.ResetAdvancedFilters()
$tagA = [guid]::NewGuid(); $tagB = [guid]::NewGuid(); $category = [guid]::NewGuid()
$game.TagIds = [Collections.Generic.List[guid]]::new(); $game.TagIds.Add($tagA)
$game.CategoryIds = [Collections.Generic.List[guid]]::new()
[void]$rules.Tags.Add($tagA); [void]$rules.Categories.Add($category)
Assert-Rule $game $rules $false 'All include groups must match by default'
$rules.FilterMatchMode = [LandingPage.Models.ShelfMatchMode]::Any
Assert-Rule $game $rules $true 'Any include group should allow a matching tag'
$rules.TagFilterAction = [LandingPage.Models.ShelfFilterAction]::Exclude
Assert-Rule $game $rules $false 'Exclusion must veto a game even in any mode'
$rules.TagFilterAction = [LandingPage.Models.ShelfFilterAction]::Include
$rules.Categories.Clear(); [void]$rules.Tags.Add($tagB)
Assert-Rule $game $rules $true 'Any selected value within a group should be the default'
$rules.MatchAllSelectedValues = $true
Assert-Rule $game $rules $false 'All selected tags should require both tags'
$game.TagIds.Add($tagB)
Assert-Rule $game $rules $true 'All selected tags should accept both tags'
$rules.Tags.Clear(); [void]$rules.Tags.Add([guid]::Empty)
Assert-Rule $game $rules $false 'None should not match populated tags'
$game.TagIds.Clear()
Assert-Rule $game $rules $true 'None should match empty tags'
$rules.TagFilterAction = [LandingPage.Models.ShelfFilterAction]::Exclude
Assert-Rule $game $rules $false 'Excluding None should exclude empty tags'
$rules.Tags.Clear(); $rules.FavoritesOnly = $true
Assert-Rule $game $rules $false 'Any matching mode must not bypass favorites-only'
$rules.FavoritesOnly = $false; $game.Hidden = $true
Assert-Rule $game $rules $false 'Hidden-game gate must remain mandatory'
$game.Hidden = $false; $rules.InstalledOnly = $true
Assert-Rule $game $rules $false 'Installation gate must remain mandatory'
$rules.InstalledOnly = $false
$source = [guid]::NewGuid(); $game.SourceId = $source; [void]$rules.Sources.Add($source)
Assert-Rule $game $rules $true 'Source inclusion should work'
$rules.SourceFilterAction = [LandingPage.Models.ShelfFilterAction]::Exclude
Assert-Rule $game $rules $false 'Source exclusion should work'
$rules.NeverPlayedOnly = $true; $rules.MinimumAddedDays = 99
$copy = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($rules), [LandingPage.Models.ShelveProperties])
if (!$copy.NeverPlayedOnly -or $copy.MinimumAddedDays -ne 99 -or !$copy.ExcludedSources.Contains($source) -or $copy.Sources.Count -ne 0) { throw 'Existing exclusion migration failed' }
$copy.ResetAdvancedFilters()
if ($copy.NeverPlayedOnly -or $copy.UseInactivityRule -or $copy.MatchAllSelectedValues -or $copy.FilterMatchMode -ne [LandingPage.Models.ShelfMatchMode]::All -or $copy.SourceFilterAction -ne [LandingPage.Models.ShelfFilterAction]::Include) { throw 'Reset advanced rules failed' }
$rules = [LandingPage.Models.ShelveProperties]::new()
$game.TagIds.Clear(); $game.TagIds.Add($tagA)
[void]$rules.Tags.Add($tagA); [void]$rules.ExcludedTags.Add($tagB)
Assert-Rule $game $rules $true 'A matching included tag should pass when an excluded tag is absent'
$game.TagIds.Add($tagB)
Assert-Rule $game $rules $false 'Include and exclude must work together in the same group'
$rules.FilterMatchMode = [LandingPage.Models.ShelfMatchMode]::Any
Assert-Rule $game $rules $false 'Any include mode must not bypass independent exclusions'
$game.TagIds.Remove($tagB) | Out-Null
[void]$rules.ExcludedTags.Add($tagA)
Assert-Rule $game $rules $false 'An identical include/exclude choice must exclude the game'
$copy = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($rules), [LandingPage.Models.ShelveProperties])
if (!$copy.Tags.Contains($tagA) -or !$copy.ExcludedTags.Contains($tagA) -or !$copy.ExcludedTags.Contains($tagB)) { throw 'Independent selections did not persist' }
$copy.ResetAdvancedFilters()
if ($copy.ExcludedTags.Count -ne 0) { throw 'Reset did not clear exclusions' }
$rules.NumberOfGames = 120
$many = [Playnite.SDK.Models.Game[]]::new(200)
if (@([LandingPage.Models.ShelfFilterRules]::SelectWindow($many, $rules, 0)).Count -ne 120) { throw 'Shelf count is still capped' }
$rules.UnlimitedGames = $true
if (@([LandingPage.Models.ShelfFilterRules]::SelectWindow($many, $rules, 7)).Count -ne 193) { throw 'Unlimited shelf/skip behavior failed' }
$legacy = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{}', [LandingPage.Models.ShelveProperties])
if ($legacy.NeverPlayedOnly -or $legacy.UseMinimumAddedAge -or $legacy.UseMaximumAddedAge -or $legacy.FilterMatchMode -ne [LandingPage.Models.ShelfMatchMode]::All) { throw 'Legacy defaults changed' }
$app = [Windows.Application]::new()
try {
    [void]$app.Resources.Add([Windows.Controls.TextBox], [Windows.Style]::new([Windows.Controls.TextBox]))
    [void]$app.Resources.Add([Windows.Controls.TextBlock], [Windows.Style]::new([Windows.Controls.TextBlock]))
    $view = [LandingPage.Views.AdvancedShelfRulesView]::new(); $view.DataContext = $legacy
    $view.Measure([Windows.Size]::new(360, 900)); $view.Arrange([Windows.Rect]::new(0, 0, 360, 900))
    $view.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $neverPlayedBox = $view.Content.Children | Where-Object {
        $_ -is [Windows.Controls.CheckBox] -and $_.GetBindingExpression([Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty).ParentBinding.Path.Path -eq 'NeverPlayedOnly'
    }
    $inactiveDaysBox = $view.Content.Children | Where-Object {
        $_ -is [Gu.Wpf.NumericInput.IntBox] -and $_.GetBindingExpression([Gu.Wpf.NumericInput.IntBox]::ValueProperty).ParentBinding.Path.Path -eq 'InactiveDays'
    }
    $neverPlayedBox.IsChecked = $true
    $inactiveDaysBox.Value = 90
    if (!$legacy.NeverPlayedOnly -or $legacy.InactiveDays -ne 90) { throw 'Advanced rules UI bindings failed' }
} finally { $app.Shutdown() }

$rules = [LandingPage.Models.ShelveProperties]::new()
$game = [Playnite.SDK.Models.Game]::new()
$game.Name = 'Space Quest [IV]'
$game.InstallDirectory = 'D:\Games\Steam\Space Quest'
$rules.IncludedInstallPathPatterns = 'd:/games/*'
Assert-Rule $game $rules $true 'Path wildcards should ignore case, normalize slashes, and include subfolders'
$rules.ExcludedInstallPathPatterns = '*\Steam\*'
Assert-Rule $game $rules $false 'Excluded subfolder must veto included directory'
$rules.ExcludedInstallPathPatterns = ''
$rules.IncludedNamePatterns = "Other*`r`nSpace ?uest [IV]"
Assert-Rule $game $rules $true 'Any line should match; question mark matches one character and brackets are literal'
$rules.ExcludedNamePatterns = '*Quest*'
$rules.FilterMatchMode = [LandingPage.Models.ShelfMatchMode]::Any
Assert-Rule $game $rules $false 'Name exclusions must veto matches even in Any mode'
$rules.ExcludedNamePatterns = ''
$rules.IncludedNamePatterns = 'Quest'
Assert-Rule $game $rules $false 'Patterns must match the full name'
$rules.IncludedNamePatterns = '*Quest*'
Assert-Rule $game $rules $true 'Surrounding stars should match a substring'
$rules.IncludedInstallPathPatterns = 'D:\GamesOther\*'
Assert-Rule $game $rules $false 'Directory boundaries must be respected'
$rules.IncludedInstallPathPatterns = 'D:\Games\*'
$game.InstallDirectory = $null
Assert-Rule $game $rules $false 'Missing path must not match an include path'
$rules.IncludedInstallPathPatterns = ''
Assert-Rule $game $rules $true 'Blank include path should permit missing paths'
$rules.IncludedInstallPathPatterns = 'D:\Games\*'
$rules.ExcludedInstallPathPatterns = '*\Excluded\*'
$rules.ExcludedNamePatterns = '*Demo*'
$copy = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($rules), [LandingPage.Models.ShelveProperties])
foreach ($property in @('IncludedNamePatterns','ExcludedNamePatterns','IncludedInstallPathPatterns','ExcludedInstallPathPatterns')) {
    if ($copy.$property -ne $rules.$property) { throw "Wildcard persistence: $property" }
}
$copy.ResetAdvancedFilters()
foreach ($property in @('IncludedNamePatterns','ExcludedNamePatterns','IncludedInstallPathPatterns','ExcludedInstallPathPatterns')) {
    if ($copy.$property -ne '') { throw "Wildcard reset: $property" }
}
'PASS: wildcard names/paths, exclusions, multiline patterns, literal punctuation, missing paths, slash normalization, persistence and reset.'
'PASS: activity/date boundaries, independent include/exclude selections, all/any matching, None handling, mandatory gates, exclusion migration, unlimited/count/skip behavior, persistence/reset/defaults, and UI bindings.'
