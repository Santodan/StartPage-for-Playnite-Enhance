# Run after a Release build: powershell.exe -STA -File tests/Test-ShelfPatterns.ps1
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildDir = Join-Path $projectRoot 'source\StartPage\bin\Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Gu.Wpf.NumericInput','Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $buildDir ($name + '.dll')))
}
$today = [datetime]'2026-11-10T12:00:00'
function Assert-Rule($game, $settings, [bool]$expected, [string]$label) {
    if ([LandingPage.Models.ShelfFilterRules]::Matches($game, $settings, $today) -ne $expected) { throw $label }
}
$game = [Playnite.SDK.Models.Game]::new()
$rules = [LandingPage.Models.ShelveProperties]::new()
foreach ($case in @(
    @('Game 4','glob:Game [0-9]',$true),
    @('Game X','glob:Game [!0-9]',$true),
    @('Game 4','glob:Game [!0-9]',$false),
    @('Game x','glob:Game [A-Z]',$true),
    @('Game?','glob:Game[?]',$true),
    @('Game*','glob:Game[*]',$true),
    @('Game [IV]','Game [IV]',$true),
    @('What?','literal:What?',$true),
    @('WhatX','literal:What?',$false),
    @('My Quest II','contains:Quest',$true),
    @('My Quest II','starts:Quest',$false),
    @('My Quest II','ends:II',$true),
    @('Quake II','regex:^(Doom|Quake)\b',$true),
    @('Earthquake','regex:^(Doom|Quake)\b',$false),
    @('Earthquake','regex:quake',$true),
    @('literal:Something','literal:literal:Something',$true)
)) {
    $game.Name = $case[0]; $rules.IncludedNamePatterns = $case[1]
    Assert-Rule $game $rules $case[2] ('Pattern mismatch: ' + $case[1])
}
$rules.ResetAdvancedFilters(); $game.Name = 'Game'; $game.InstallDirectory = $null
$rules.IncludedInstallPathPatterns = 'missing:'
Assert-Rule $game $rules $true 'Missing path include failed'
$game.InstallDirectory = 'D:\Games\Example'
Assert-Rule $game $rules $false 'Missing path must not match a populated path'
$rules.IncludedInstallPathPatterns = "missing:`nD:/Games/*"
Assert-Rule $game $rules $true 'Missing + path alternatives failed'
$rules.ExcludedInstallPathPatterns = 'missing:'; $game.InstallDirectory = $null
Assert-Rule $game $rules $false 'Missing path exclude failed'
$rules.ResetAdvancedFilters()
foreach ($bad in @('regex:(', 'glob:Game[', 'glob:[z-a]', ('x' * 2049))) {
    if (![LandingPage.Models.ShelfFilterRules]::ValidatePatterns($bad)) { throw "Missing validation error for $bad" }
    $rules.ExcludedNamePatterns = $bad
    Assert-Rule $game $rules $false 'Invalid exclusions must fail closed'
    if (!([ComponentModel.IDataErrorInfo]$rules)['ExcludedNamePatterns']) { throw 'IDataErrorInfo did not expose validation error' }
}
$rules.ResetAdvancedFilters(); $game.Name = ('a' * 10000) + '!'; $rules.ExcludedNamePatterns = 'regex:(a+)+$'
Assert-Rule $game $rules $false 'Regex timeout must fail closed'
$rules.ResetAdvancedFilters(); $game.Name = 'Game'; $game.SortingName = 'Sorted Game'; $game.Version = '1.2.3'
$action = [Playnite.SDK.Models.GameAction]::new()
$action.Name = 'Play with Vulkan'; $action.Path = 'D:\Emulators\retroarch.exe'; $action.WorkingDir = 'D:\Emulators'
$action.Arguments = '-L core.dll'; $action.AdditionalArguments = '-fullscreen'; $action.IsPlayAction = $false
$game.GameActions = [Collections.ObjectModel.ObservableCollection[Playnite.SDK.Models.GameAction]]::new(); $game.GameActions.Add($action)
$rom = [Playnite.SDK.Models.GameRom]::new(); $rom.Path = 'D:\ROMs\NES\Game.nes'
$game.Roms = [Collections.ObjectModel.ObservableCollection[Playnite.SDK.Models.GameRom]]::new(); $game.Roms.Add($rom)
foreach ($case in @(
    @('SortingName','Sorted*'), @('Version','1.2.*'), @('LaunchPath','d:/emulators/*retroarch.exe'),
    @('WorkingDirectory','D:/Emulators'), @('LaunchArguments','*-fullscreen*'), @('ActionName','*Vulkan*'), @('RomPath','*.nes')
)) {
    $rules.ResetAdvancedFilters(); $inc = 'Included' + $case[0] + 'Patterns'; $exc = 'Excluded' + $case[0] + 'Patterns'
    $rules.$inc = $case[1]; Assert-Rule $game $rules $true ("Field mapping include: $inc")
    $rules.$exc = $case[1]; Assert-Rule $game $rules $false ("Field mapping exclude: $exc")
}

# SDK metadata getters resolve through an in-memory API proxy. Never opens a real database.
Add-Type -ReferencedAssemblies @((Join-Path $buildDir 'Playnite.SDK.dll'), 'System.Core.dll') -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using Playnite.SDK;
using Playnite.SDK.Models;
public class ShelfPatternTestProxy : RealProxy
{
    private readonly Func<IMethodCallMessage, object> invoke;
    private ShelfPatternTestProxy(Type type, Func<IMethodCallMessage, object> invoke) : base(type) { this.invoke = invoke; }
    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        try
        {
            object value;
            if (call.MethodName == "GetHashCode") value = 1;
            else if (call.MethodName == "ToString") value = "Shelf pattern test API";
            else if (call.MethodName == "Equals") value = false;
            else if (call.MethodName == "GetType") value = GetProxiedType();
            else value = invoke(call);
            return new ReturnMessage(value, null, 0, call.LogicalCallContext, call);
        }
        catch (Exception ex) { return new ReturnMessage(ex, call); }
    }
    public static IGameDatabase Create(Dictionary<string, DatabaseObject> entries)
    {
        var properties = typeof(IGameDatabase).GetInterfaces().Concat(new[] { typeof(IGameDatabase) })
            .SelectMany(type => type.GetProperties()).GroupBy(prop => prop.Name).ToDictionary(group => group.Key, group => group.First().PropertyType);
        var collections = entries.ToDictionary(pair => pair.Key, pair =>
            new ShelfPatternTestProxy(properties[pair.Key], call => {
                if ((call.MethodName == "Get" || call.MethodName == "get_Item") && call.ArgCount == 1 && call.Args[0] is Guid)
                    return (Guid)call.Args[0] == pair.Value.Id ? pair.Value : null;
                if (call.MethodName == "Get" && call.ArgCount == 1 && call.Args[0] is IEnumerable<Guid>)
                {
                    var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pair.Value.GetType()));
                    if (((IEnumerable<Guid>)call.Args[0]).Contains(pair.Value.Id)) list.Add(pair.Value);
                    return list;
                }
                throw new NotSupportedException(call.MethodName);
            }).GetTransparentProxy());
        var database = new ShelfPatternTestProxy(typeof(IGameDatabase), call => collections[call.MethodName.Substring(4)]).GetTransparentProxy();
        return (IGameDatabase)database;
    }
}
'@
$entries = [Collections.Generic.Dictionary[string,Playnite.SDK.Models.DatabaseObject]]::new()
$metadata = @(
    @('SourceName','Sources','GameSource','SourceId','Steam'),
    @('PlatformName','Platforms','Platform','PlatformIds','Nintendo NES'),
    @('TagName','Tags','Tag','TagIds','Backlog Soon'),
    @('CategoryName','Categories','Category','CategoryIds','Co-op'),
    @('GenreName','Genres','Genre','GenreIds','Strategy'),
    @('FeatureName','Features','GameFeature','FeatureIds','Controller Support'),
    @('DeveloperName','Companies','Company','DeveloperIds','Valve'),
    @('PublisherName','Companies','Company','PublisherIds','Valve'),
    @('SeriesName','Series','Series','SeriesIds','The Legend of Zelda'),
    @('CompletionName','CompletionStatuses','CompletionStatus','CompletionStatusId','Unplayed'),
    @('RegionName','Regions','Region','RegionIds','Europe'),
    @('AgeRatingName','AgeRatings','AgeRating','AgeRatingIds','PEGI 12')
)
foreach ($row in $metadata) {
    if (!$entries.ContainsKey($row[1])) {
        $item = New-Object ('Playnite.SDK.Models.' + $row[2]); $item.Name = $row[4]
        $entries.Add($row[1], $item)
    }
    $id = $entries[$row[1]].Id
    if ($row[3].EndsWith('Ids')) {
        $ids = [Collections.Generic.List[guid]]::new(); $ids.Add($id); $game.($row[3]) = $ids
    } else { $game.($row[3]) = $id }
}
$apiField = [Playnite.SDK.Models.Game].GetField('<DatabaseReference>k__BackingField', [Reflection.BindingFlags]'Static,NonPublic')
$originalApi = $apiField.GetValue($null)
try {
    $apiField.SetValue($null, [ShelfPatternTestProxy]::Create($entries))
    foreach ($row in $metadata) {
        $rules.ResetAdvancedFilters(); $inc = 'Included' + $row[0] + 'Patterns'; $exc = 'Excluded' + $row[0] + 'Patterns'
        $rules.$inc = '*' + $row[4] + '*'
        Assert-Rule $game $rules $true ("Metadata name include: $inc")
        $rules.$exc = $rules.$inc
        Assert-Rule $game $rules $false ("Metadata name exclude: $exc")
    }
} finally { $apiField.SetValue($null, $originalApi) }

$properties = @([LandingPage.Models.ShelveProperties].GetProperties() | Where-Object Name -Match '^(Included|Excluded).*Patterns$')
if ($properties.Count -ne 42) { throw 'Expected 21 include/exclude fields' }
$rules.ResetAdvancedFilters()
foreach ($property in $properties) { $rules.($property.Name) = '*Example*' }
$copy = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($rules), [LandingPage.Models.ShelveProperties])
foreach ($property in $properties) { if ($copy.($property.Name) -ne '*Example*') { throw ('Persistence: ' + $property.Name) } }
$copy.ResetAdvancedFilters()
foreach ($property in $properties) { if ($copy.($property.Name) -ne '') { throw ('Reset: ' + $property.Name) } }
$app = [Windows.Application]::new()
try {
    [void]$app.Resources.Add([Windows.Controls.TextBox], [Windows.Style]::new([Windows.Controls.TextBox]))
    [void]$app.Resources.Add([Windows.Controls.TextBlock], [Windows.Style]::new([Windows.Controls.TextBlock]))
    $view = [LandingPage.Views.AdvancedShelfRulesView]::new(); $view.DataContext = $copy
    $view.Content.Children[1].IsExpanded = $true
    ($view.Content.Children[1].Content.Children | Where-Object { $_ -is [Windows.Controls.Expander] -and $_.Header -eq 'Game names' }).IsExpanded = $true
    $view.Measure([Windows.Size]::new(360,900)); $view.Arrange([Windows.Rect]::new(0,0,360,900))
    $view.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle)
    foreach ($property in $properties) {
        $box = $view.FindName($property.Name + 'Box')
        $binding = $box.GetBindingExpression([Windows.Controls.TextBox]::TextProperty)
        if ($binding.ParentBinding.Path.Path -ne $property.Name) { throw ('UI binding: ' + $property.Name) }
    }
    $box = $view.FindName('IncludedNamePatternsBox')
    $box.Text = 'regex:('; $box.GetBindingExpression([Windows.Controls.TextBox]::TextProperty).UpdateSource()
    $view.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if (![Windows.Controls.Validation]::GetHasError($box)) { throw 'Inline validation did not show an invalid regex' }
    $errorText = $box.Parent.Children | Where-Object { $_ -is [Windows.Controls.TextBlock] -and $_.Tag -eq $box }
    if ($errorText.Visibility -ne [Windows.Visibility]::Visible -or !$errorText.Text) { throw 'Inline error text was not visible' }
    $box.Text = '*Game*'; $box.GetBindingExpression([Windows.Controls.TextBox]::TextProperty).UpdateSource()
    $view.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ([Windows.Controls.Validation]::GetHasError($box)) { throw 'Inline validation did not clear after correction' }
} finally { $app.Shutdown() }
$help = [LandingPage.Views.AdvancedShelfRulesView]::PatternHelpUrl
if ($help -ne 'https://github.com/Santodan/StartPage-for-Playnite/blob/master/docs/SHELF-PATTERNS.md' -or !(Test-Path (Join-Path $projectRoot 'docs/SHELF-PATTERNS.md'))) { throw 'Help URL/document mismatch' }
'PASS: 21 fields, metadata name resolution, custom actions/ROMs, glob/regex syntax, missing values, invalid/slow patterns, persistence/reset, 42 UI bindings, inline validation, and help-document target.'
