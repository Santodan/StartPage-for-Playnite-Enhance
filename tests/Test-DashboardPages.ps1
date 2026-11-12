# Runs entirely against temporary settings and an in-memory Playnite API.
# powershell.exe -STA -File tests/Test-DashboardPages.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'source\StartPage\bin\Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Gu.Wpf.NumericInput','Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll')))
}
$app = [Windows.Application]::new()
[Threading.SynchronizationContext]::SetSynchronizationContext([Windows.Threading.DispatcherSynchronizationContext]::new($app.Dispatcher))
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
function Set-Field($target, $name, $value) { $target.GetType().GetField($name, $flags).SetValue($target, $value) }
function Get-Setting($settings, $name) { return ,([LandingPage.LandingPageSettings].GetProperty($name).GetValue($settings)) }
function Set-Setting($settings, $name, $value) { [LandingPage.LandingPageSettings].GetProperty($name).SetValue($settings, $value) }
function Wait-Task($task) {
    $deadline = [datetime]::UtcNow.AddSeconds(15)
    while (!$task.IsCompleted) {
        if ([datetime]::UtcNow -gt $deadline) { throw 'Dashboard task timed out' }
        $app.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::Background)
        Start-Sleep -Milliseconds 5
    }
    [void]$task.GetAwaiter().GetResult()
}
$tempRoot = Join-Path $env:TEMP ('StartPage-pages-test-' + [guid]::NewGuid())
[void][IO.Directory]::CreateDirectory($tempRoot)
Add-Type -ReferencedAssemblies @((Join-Path $build 'Playnite.SDK.dll'), (Join-Path $build 'StartPage.SDK.dll'), (Join-Path $build 'StartPagePlugin.dll'), (Join-Path $build 'StartPage.SDK.Async.dll'), (Join-Path $build 'Newtonsoft.Json.dll'), 'System.Core.dll','PresentationFramework','PresentationCore','WindowsBase','System.Xaml') -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using StartPage.SDK;
public static class ToolbarTestVisuals
{
    public static System.Windows.FrameworkElement Named(System.Windows.DependencyObject root, string name)
    {
        var element = root as System.Windows.FrameworkElement;
        if (element != null && element.Name == name) return element;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); ++i)
        { var found = Named(System.Windows.Media.VisualTreeHelper.GetChild(root,i),name); if (found != null) return found; }
        return null;
    }

    public static IEnumerable<LandingPage.Controls.DashboardTemplatePreview> Previews(System.Windows.DependencyObject root)
    {
        var preview = root as LandingPage.Controls.DashboardTemplatePreview;
        if (preview != null) yield return preview;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); ++i)
            foreach (var child in Previews(System.Windows.Media.VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    public static IEnumerable<Button> Buttons(System.Windows.DependencyObject root)
    {
        var button = root as Button;
        if (button != null) yield return button;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); ++i)
            foreach (var child in Buttons(System.Windows.Media.VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    public static LandingPage.Views.ShelfGameInfo GameInfo(System.Windows.DependencyObject root)
    {
        var info = root as LandingPage.Views.ShelfGameInfo;
        if (info != null) return info;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); ++i)
        { var found = GameInfo(System.Windows.Media.VisualTreeHelper.GetChild(root,i)); if (found != null) return found; }
        return null;
    }
    public static IEnumerable<TextBlock> Text( System.Windows.DependencyObject root)
    {
        var text = root as TextBlock;
        if (text != null) yield return text;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); ++i)
            foreach (var child in Text(System.Windows.Media.VisualTreeHelper.GetChild(root,i))) yield return child;
    }
}
public class ToolbarTestSidebarWrapper
{
    public Playnite.SDK.Plugins.SidebarItem SideItem { get; set; }
}
public class ToolbarTestModel : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    private string searchText = "";
    public string SearchText { get { return searchText; } set { searchText = value; if (PropertyChanged != null) PropertyChanged(this,new System.ComponentModel.PropertyChangedEventArgs("SearchText")); } }
    private bool filters;
    public bool FilterPanelVisible { get { return filters; } set { filters = value; if (PropertyChanged != null) PropertyChanged(this,new System.ComponentModel.PropertyChangedEventArgs("FilterPanelVisible")); } }
    public bool NotificationPanelVisible { get; set; }
    public int Searches, PluginClicks;
    public List<ToolbarTestSidebarWrapper> SidebarItems = new List<ToolbarTestSidebarWrapper>();
    public object LastPage;
    public System.Windows.Input.ICommand SelectSidebarViewCommand { get; private set; }
    public System.Windows.Input.ICommand OpenGlobalSearchCommand { get; private set; }
    public System.Windows.Input.ICommand SwitchDetailsViewCommand { get; private set; }
    public ToolbarTestModel()
    {
        SelectSidebarViewCommand = new RelayCommand<object>(page => LastPage = page);
        OpenGlobalSearchCommand = new RelayCommand(() => ++Searches);
        SwitchDetailsViewCommand = new RelayCommand(() => {}, () => false);
    }
}
public class ToolbarTestWrapper
{
    public string Title { get; set; }
    public object IconObject { get; set; }
    public System.Windows.Input.ICommand Command { get; set; }
    public Playnite.SDK.Plugins.TopPanelItem PanelItem { get; set; }
    public ToolbarTestWrapper() { PanelItem = new Playnite.SDK.Plugins.TopPanelItem { Visible = true }; }
}
namespace Playnite.DesktopApp.Controls.Views
{
    public class TopPanel : ContentControl
    {
        public StackPanel PanelMainItems = new StackPanel();
        public StackPanel PanelMainPluginItems = new StackPanel();
        public ToolbarTestWrapper ButtonSearch, ButtonSortSettings;
        public Border ElemMainMenu = new Border();
        public TextBox TextMainSearch = new TextBox();
        public CheckBox ToggleFilter = new CheckBox(), ToggleNotifications = new CheckBox();
        public ProgressBar ProgressGlobal = new ProgressBar { Maximum = 100, Value = 25 };
        public TextBlock TextProgressText = new TextBlock { Text = "Test operation" };
        public Button ButtonProgressCancel = new Button();
        public ContextMenu SortMenu;
        public TopPanel(ToolbarTestModel model)
        {
            ButtonSearch = new ToolbarTestWrapper { Title = "Search", Command = model.OpenGlobalSearchCommand };
            PanelMainItems.Children.Add(new Button { DataContext = ButtonSearch, Command = ButtonSearch.Command });
            ButtonSortSettings = new ToolbarTestWrapper { Title = "Sort by" };
            SortMenu = new ContextMenu { Opacity = 0 };
            SortMenu.Items.Add(new MenuItem { Header = "Name" });
            PanelMainItems.Children.Add(new Button { DataContext = ButtonSortSettings, ContextMenu = SortMenu });
            ElemMainMenu.ContextMenu = new ContextMenu { Opacity = 0 };
            var wrapper = new ToolbarTestWrapper { Title = "Test extension", Command = new RelayCommand(() => ++model.PluginClicks), IconObject = new TextBlock { Text = "\u2605", FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"), FontSize = 20 } };
            wrapper.PanelItem.Activated = () => ++model.PluginClicks;
            PanelMainPluginItems.Children.Add(new Button { DataContext = wrapper, Command = wrapper.Command, Content = wrapper.IconObject });
            TextMainSearch.SetBinding(TextBox.TextProperty,new System.Windows.Data.Binding("SearchText") { Source = model, Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
            ToggleFilter.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,new System.Windows.Data.Binding("FilterPanelVisible") { Source = model, Mode = System.Windows.Data.BindingMode.TwoWay });
            ToggleNotifications.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,new System.Windows.Data.Binding("NotificationPanelVisible") { Source = model, Mode = System.Windows.Data.BindingMode.TwoWay });
            var panel = new StackPanel(); panel.Children.Add(PanelMainItems); panel.Children.Add(PanelMainPluginItems); Content = panel;
        }
    }
}
public class DashboardTestNullConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { return value != null; }
    public object ConvertBack(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { throw new NotSupportedException(); }
}
public class DashboardTestWidget : Border, IStartPageControl, StartPage.SDK.Async.IAsyncStartPageControl
{
    public int OpenCount, CloseCount, ShownAsyncCount, HiddenAsyncCount;
    public Action BeforeHidden;
    public System.Threading.Tasks.Task InitializeAsync() { return System.Threading.Tasks.Task.FromResult(0); }
    public System.Threading.Tasks.Task OnViewShownAsync() { ++ShownAsyncCount; return System.Threading.Tasks.Task.FromResult(0); }
    public System.Threading.Tasks.Task OnViewHiddenAsync() { if (BeforeHidden != null) BeforeHidden(); ++HiddenAsyncCount; return System.Threading.Tasks.Task.FromResult(0); }
    public System.Threading.Tasks.Task OnDayChangedAsync(DateTime time) { return System.Threading.Tasks.Task.FromResult(0); }
    public void OnStartPageOpened() { ++OpenCount; }
    public void OnStartPageClosed() { ++CloseCount; }
    public void OnDayChanged(DateTime time) { }
}
public class ToolbarTestProvider : Plugin
{
    public override Guid Id { get { return Guid.Parse("ddfd6f9a-f1d4-40ea-98af-76427616b27a"); } }
    public List<Playnite.SDK.Plugins.TopPanelItem> Items = new List<Playnite.SDK.Plugins.TopPanelItem>();
    public int Activations;
    public List<SidebarItem> Pages = new List<SidebarItem>();
    public override IEnumerable<SidebarItem> GetSidebarItems() { return Pages; }
    public ToolbarTestProvider() : base(null)
    {
        Items.Add(new Playnite.SDK.Plugins.TopPanelItem { Title = "Direct SDK provider", Visible = true,
            Icon = new TextBlock { Text = "\u2606", FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"), FontSize = 20 },
            Activated = () => ++Activations });
    }
    public override IEnumerable<Playnite.SDK.Plugins.TopPanelItem> GetTopPanelItems() { return Items; }
    public Control OpenPage() { return new UserControl(); }
}
public class DashboardTestProxy : RealProxy
{
    public static List<Plugin> ExtraPlugins = new List<Plugin>();
    private readonly Func<IMethodCallMessage, object> handler;
    public DashboardTestProxy(Type type, Func<IMethodCallMessage, object> handler) : base(type) { this.handler = handler; }
    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        try
        {
            object value;
            if (call.MethodName == "ToString") value = "Dashboard test proxy";
            else if (call.MethodName == "GetHashCode") value = 1;
            else if (call.MethodName == "Equals") value = false;
            else if (call.MethodName == "GetType") value = GetProxiedType();
            else value = handler(call);
            return new ReturnMessage(value, null, 0, call.LogicalCallContext, call);
        }
        catch (Exception ex) { return new ReturnMessage(ex, call); }
    }
    public static object CreateSerializer()
    {
        return new DashboardTestProxy(typeof(Playnite.SDK.Data.IDataSerializer), call => {
            if (call.MethodName == "ToJson") return Newtonsoft.Json.JsonConvert.SerializeObject(call.Args[0]);
            throw new NotSupportedException(call.MethodName);
        }).GetTransparentProxy();
    }
    public static int LibraryEnumerations;
    public static readonly List<Game> FeaturedFixtureGames = new List<Game>();
    public static IPlayniteAPI Create(string directory, Plugin plugin)
    {
        var paths = new DashboardTestProxy(typeof(IPlaynitePathsAPI), call => directory).GetTransparentProxy();
        var notificationsType = typeof(IPlayniteAPI).GetProperty("Notifications").PropertyType;
        var notifications = new DashboardTestProxy(notificationsType, call => new ObservableCollection<NotificationMessage>()).GetTransparentProxy();
        var addonsType = typeof(IPlayniteAPI).GetProperty("Addons").PropertyType;
        var addons = new DashboardTestProxy(addonsType, call => { var loaded = new List<Plugin> { plugin }; loaded.AddRange(ExtraPlugins); return loaded; }).GetTransparentProxy();
        var games = new DashboardTestProxy(typeof(IItemCollection<Game>), call =>
        {
            if (call.MethodName == "GetEnumerator") { System.Threading.Interlocked.Increment(ref LibraryEnumerations); return ((IEnumerable<Game>)FeaturedFixtureGames).GetEnumerator(); }
            if (call.MethodName == "get_Count") { System.Threading.Interlocked.Increment(ref LibraryEnumerations); return FeaturedFixtureGames.Count; }
            if (call.MethodName == "Get") return FeaturedFixtureGames.Find(game => game.Id == (Guid)call.Args[0]);
            if (call.MethodName == "CopyTo") { FeaturedFixtureGames.CopyTo((Game[])call.Args[0],(int)call.Args[1]); return null; }
            if (call.MethodName == "get_IsReadOnly") return false;
            return null;
        }).GetTransparentProxy();
        var database = new DashboardTestProxy(typeof(IGameDatabaseAPI), call =>
        {
            if (call.MethodName == "get_Games") return games;
            if (call.MethodName == "GetFullFilePath") return call.Args[0];
            return null;
        }).GetTransparentProxy();
        return (IPlayniteAPI)new DashboardTestProxy(typeof(IPlayniteAPI), call =>
        {
            switch (call.MethodName)
            {
                case "get_Paths": return paths;
                case "get_Notifications": return notifications;
                case "get_Addons": return addons;
                case "get_Database": return database;
                default: return null;
            }
        }).GetTransparentProxy();
    }
}
'@
$originalPlugin = [LandingPage.LandingPageExtension]::Instance
$serializerField = [Playnite.SDK.Data.Serialization].GetField('serializer', [Reflection.BindingFlags]'Static,NonPublic')
$originalSerializer = $serializerField.GetValue($null)
$serializerField.SetValue($null, [DashboardTestProxy]::CreateSerializer())
$vm = $null
try {
    # Legacy migration retains the original layout/widget instances and appearance.
    $settings = [LandingPage.LandingPageSettings]::new()
    $legacyLayout = [LandingPage.Models.Layout.GridNode]::new()
    $widget = [LandingPage.Models.Layout.ViewProperties]::new(); $widget.ViewId = 'TextWidget'
    $widget.TextSettings.Text = 'Keep my dashboard'; $widget.Appearance.Enabled = $true
    $legacyLayout.ViewProperties = $widget
    Set-Setting $settings 'GridLayout' $legacyLayout; Set-Setting $settings 'BlurAmount' ([double]37); Set-Setting $settings 'OverlayOpacity' ([double]0.42)
    $first = [LandingPage.Models.DashboardPages]::Ensure($settings)
    if ((Get-Setting $settings 'DashboardPages').Count -ne 1 -or $first.Name -ne 'Main' -or ![object]::ReferenceEquals($first.Layout, $legacyLayout) -or $first.Layout.ViewProperties.InstanceId -ne $widget.InstanceId) { throw 'Legacy layout migration failed' }
    $second = [LandingPage.Models.DashboardPages]::Add($settings, 'Library')
    $third = [LandingPage.Models.DashboardPages]::Add($settings, 'Library')
    if ($third.Name -ne 'Library (2)' -or [object]::ReferenceEquals($first.Background, $second.Background) -or $second.Layout.ViewProperties) { throw 'New pages must have independent backgrounds and empty layouts' }
    $second.Background.Blur = 4
    if ($first.Background.Blur -ne 37) { throw 'Page backgrounds share mutable settings' }
    (Get-Setting $settings 'DashboardPages').Move(2,0)
    $copy = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($settings), [LandingPage.LandingPageSettings])
    $restored = [LandingPage.Models.DashboardPages]::Ensure($copy)
    if ($restored.Id -ne $first.Id -or (Get-Setting $copy 'DashboardPages')[0].Name -ne 'Library (2)' -or $restored.Layout.ViewProperties.TextSettings.Text -ne 'Keep my dashboard') { throw 'Page persistence/order/active layout failed' }
    # Nested enumeration must reach grandchildren exactly once.
    $nested = [LandingPage.Models.Layout.GridNode]::new(); $child = [LandingPage.Models.Layout.GridNode]::new(); $grandchild = [LandingPage.Models.Layout.GridNode]::new()
    $child.Children.Add($grandchild); $nested.Children.Add($child)
    $nodes = @($nested.Enumerate())
    if ($nodes.Count -ne 3 -or ![object]::ReferenceEquals($nodes[2],$grandchild)) { throw 'Nested page traversal misses widgets' }

    # A real StartPage view model saves only inside our unique temporary directory.
    $plugin = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.LandingPageExtension])
    [LandingPage.LandingPageExtension].GetProperty('Instance').SetValue($null,$plugin)
    # Startup logging occurs before SettingsViewModel has been assigned.
    [LandingPage.LandingPageExtension].GetField('logger',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null).Info('Fixture startup before settings initialization')

    $api = [DashboardTestProxy]::Create($tempRoot,$plugin)
    [Playnite.SDK.Plugins.Plugin].GetField('PlayniteApi',$flags).SetValue($plugin,$api)
    $settingsModel = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.LandingPageSettingsViewModel])
    $settingsModel.Settings = [LandingPage.LandingPageSettings]::new()
    [LandingPage.LandingPageExtension].GetProperty('SettingsViewModel',$flags).SetValue($plugin,$settingsModel)
    Set-Setting $settingsModel.Settings 'GridLayout' ([LandingPage.Models.Layout.GridNode]::new())
    Set-Setting $settingsModel.Settings 'BackgroundImageUri' ([uri]('file:///' + $tempRoot.Replace('\','/') + '/A.png'))
    Set-Setting $settingsModel.Settings 'BlurAmount' ([double]19)
    $background = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.ViewModels.BackgroundViewModel])
    $background.Settings = $settingsModel
    Set-Field $background 'playniteAPI' $api; Set-Field $background 'plugin' $plugin
    Set-Field $background 'backgroundImageQueue' ([Collections.ObjectModel.ObservableCollection[LandingPage.ViewModels.BackgroundQueueItem]]::new())
    $rootModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new((Get-Setting $settingsModel.Settings 'GridLayout'))
    $vm = [LandingPage.ViewModels.StartPageViewModel].GetConstructors()[0].Invoke([object[]]@($api,$plugin,$settingsModel,$rootModel,$background))
    Wait-Task ($vm.Inititalize())
    $pageA = $vm.SelectedPage
    $probe = [DashboardTestWidget]::new(); $probeProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $probeProperties.ViewId = 'Probe'
    [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($probeProperties,$probe)
    $rootModel.GridNode.ViewProperties = $probeProperties
    $rootModel.OpenView()
    $pageB = [LandingPage.Models.DashboardPages]::Add($settingsModel.Settings,'Second')
    $pageB.Background.Blur = 3; $pageB.Background.ImageUri = [uri]('file:///' + $tempRoot.Replace('\','/') + '/B.png')
    $script:backgroundReadyBeforeHide = $false
    $probe.BeforeHidden = [Action]{
        $script:backgroundReadyBeforeHide = $background.BackgroundImagePath -eq $pageB.Background.ImageUri -and $background.BackgroundImageQueue.Count -eq 1 -and $background.BackgroundImageQueue[0].Opacity -eq 1
    }
    $firstSwitch = $vm.SwitchPageAsync($pageB)
    if (!$script:backgroundReadyBeforeHide) { throw 'Switch yielded to the dispatcher before hiding the outgoing page' }
    Wait-Task $firstSwitch
    $probe.BeforeHidden = $null
    if (!$script:backgroundReadyBeforeHide) { throw 'Destination background must replace old artwork before outgoing widget callbacks' }
    if ($probe.CloseCount -ne 1 -or (Get-Setting $settingsModel.Settings 'BlurAmount') -ne 3 -or !$background.BackgroundImagePath.AbsoluteUri.EndsWith('B.png')) { throw 'Switch did not close outgoing widget/apply target background' }
    Set-Setting $settingsModel.Settings 'BlurAmount' ([double]7)
    Wait-Task ($vm.SwitchPageAsync($pageA))
    if ($probe.OpenCount -ne 2 -or ![object]::ReferenceEquals($vm.RootNodeViewModel,$rootModel) -or $pageB.Background.Blur -ne 7 -or (Get-Setting $settingsModel.Settings 'BlurAmount') -ne 19) { throw 'Cached page/widget/background isolation failed' }
    Wait-Task ($vm.SwitchPageAsync($pageB))
    if ((Get-Setting $settingsModel.Settings 'BlurAmount') -ne 7) { throw 'Edited inactive background was lost' }
    $vm.MovePageUpCommand.Execute($null)
    if ($vm.Pages[0].Id -ne $pageB.Id) { throw 'Move page command failed' }
    $vm.RequestDeletePageCommand.Execute($null); $vm.DeletePageCommand.Execute($null)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($vm.Pages.Count -ne 1 -or $vm.SelectedPage.Id -ne $pageA.Id -or $vm.RequestDeletePageCommand.CanExecute($null)) { throw 'Delete/last-page protection failed' }
    $savedFile = Get-ChildItem -LiteralPath $tempRoot -Recurse -Filter config.json | Select-Object -First 1
    if (!$savedFile) { throw 'Page operations did not persist settings' }
    $saved = [Newtonsoft.Json.JsonConvert]::DeserializeObject([IO.File]::ReadAllText($savedFile.FullName), [LandingPage.LandingPageSettings])
    if ((Get-Setting $saved 'ActiveDashboardPageId') -ne $pageA.Id -or (Get-Setting $saved 'DashboardPages').Count -ne 1) { throw 'Saved active page/deletion is incorrect' }
    $imagePath = Join-Path $tempRoot 'page-image.png'
    [IO.File]::Copy((Join-Path $root 'source/StartPage/icon.png'),$imagePath)
    $backgroundItem = [LandingPage.ViewModels.BackgroundQueueItem]::new([uri]$imagePath,1)
    $decodedA = $backgroundItem.Image
    $decodedB = ([LandingPage.ViewModels.BackgroundQueueItem]::new([uri]$imagePath,1)).Image
    if ($decodedA -isnot [Windows.Media.Imaging.BitmapSource] -or !$decodedA.IsFrozen -or ![object]::ReferenceEquals($decodedA,$decodedB)) { throw 'Revisited background did not reuse a frozen decoded image' }
    [IO.File]::SetLastWriteTimeUtc($imagePath,[datetime]::UtcNow.AddMinutes(1))
    $decodedChanged = ([LandingPage.ViewModels.BackgroundQueueItem]::new([uri]$imagePath,1)).Image
    if ([object]::ReferenceEquals($decodedA,$decodedChanged)) { throw 'Background cache failed to invalidate an edited image' }
    $vm.SetPageImage($imagePath)
    if ($background.BackgroundImagePath.LocalPath -ne $imagePath) { throw 'Choosing a local page image did not display it' }
    $vm.SetPageImage($tempRoot)
    if ($background.BackgroundImagePath.LocalPath -ne $imagePath) { throw 'Choosing a page image folder did not select its image' }
    $vm.SetPageImage($null)
    if ($background.BackgroundImagePath -ne $null -or $background.BackgroundImageQueue.Count -ne 0) { throw 'Empty page background retained another page image' }
    Wait-Task ($vm.Closed())
    if ($probe.CloseCount -ne 3) { throw 'Closing StartPage did not hide current widget' }
    Wait-Task ($vm.Opened())
    if ($probe.OpenCount -ne 4 -or $probe.ShownAsyncCount -ne 3 -or $probe.HiddenAsyncCount -ne 3) { throw 'Returning to StartPage did not reopen current widget or async callbacks were missed' }
    foreach ($type in @([Windows.Controls.TextBox],[Windows.Controls.ScrollViewer],[Windows.Controls.Button],[Windows.Controls.Border],[Windows.Controls.ComboBox],[Windows.Controls.Expander],[Windows.Controls.GridSplitter],[Windows.Controls.ListBox],[Windows.Controls.CheckBox],[Windows.Controls.ContextMenu],[Windows.Controls.Menu],[Windows.Controls.MenuItem],[Windows.Controls.Primitives.Popup])) {
        [void]$app.Resources.Add($type,[Windows.Style]::new($type))
    }
    [void]$app.Resources.Add('TextBrush',[Windows.Media.Brushes]::Lime)
    [void]$app.Resources.Add('BaseTextBlockStyle',[Windows.Style]::new([Windows.Controls.TextBlock]))
    [void]$app.Resources.Add('GlyphTextBoxStyle',[Windows.Style]::new([Windows.Controls.TextBlock]))
    [void]$app.Resources.Add('FontIcoFont',[Windows.Media.FontFamily]::new('Segoe UI'))
    [void]$app.Resources.Add('BooleanToVisibilityConverter',[Windows.Controls.BooleanToVisibilityConverter]::new())
    [void]$app.Resources.Add('InvertedBooleanToVisibilityConverter',[Windows.Controls.BooleanToVisibilityConverter]::new())
    foreach ($key in @('NullToBoolConverter','InvertedObjectEqualityToBoolConverter','StringNullOrEmptyToBoolConverter')) { [void]$app.Resources.Add($key,[DashboardTestNullConverter]::new()) }
    [void]$app.Resources.Add('NullToVisibilityConverter',[Windows.Controls.BooleanToVisibilityConverter]::new())
    try { $view = [LandingPage.Views.StartPageView]::new() }
    catch { $detail = $_.Exception; while ($detail.InnerException) { $detail = $detail.InnerException }; throw $detail.Message }
    $view.DataContext = $vm
    $view.Measure([Windows.Size]::new(1000,800)); $view.Arrange([Windows.Rect]::new(0,0,1000,800))
    $view.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $manageMenu = @($view.ContextMenu.Items | Where-Object { $_ -is [Windows.Controls.MenuItem] -and $_.Header -eq 'Manage dashboard pages...' })
    if ($manageMenu.Count -ne 1) { throw 'Page management must remain available without a toolbar widget' }
    $view.GetType().GetMethod('ManagePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    if ($view.FindName('PagesOverlay').Visibility -ne [Windows.Visibility]::Visible) { throw 'Page manager did not open inside StartPage' }
    $view.GetType().GetMethod('ClosePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    if ($view.FindName('PagesOverlay').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Page manager did not close' }
    $toolbarSettings = [LandingPage.Settings.ToolbarWidgetSettings]::new()
    $toolbarClone = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($toolbarSettings), [LandingPage.Settings.ToolbarWidgetSettings])
    if ($toolbarClone.Items.Count -ne 4 -or [object]::ReferenceEquals($toolbarClone.Items,$toolbarSettings.Items)) { throw 'Toolbar defaults duplicated/shared after serialization' }
    $nativeModel = [ToolbarTestModel]::new()
    $native = [Playnite.DesktopApp.Controls.Views.TopPanel]::new($nativeModel)
    $themeIconTemplate = [Windows.Markup.XamlReader]::Parse('<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><TextBlock Text="THEME"/></DataTemplate>')
    [void]$native.Resources.Add('TopPanelGeneralViewSettingsTemplate',$themeIconTemplate)
    $options = [LandingPage.Controls.NativeToolbarCatalog]::Discover($native,$nativeModel)
    $viewSettingsOption = $options | Where-Object Id -eq 'native.view-settings'
    $manageOption = $options | Where-Object Id -eq 'page.manage'
    if (![object]::ReferenceEquals($viewSettingsOption.IconTemplate,$themeIconTemplate) -or $viewSettingsOption.Glyph -eq $manageOption.Glyph) { throw 'Theme icon resource or distinct page-management icon failed' }
    if ($options.Count -lt 25 -or !($options | Where-Object Id -eq 'native.sort').NativeControl.ContextMenu) { throw 'Native toolbar catalog omitted buttons or dropdowns' }
    $toolbar = [LandingPage.Views.ToolbarWidgetView]::new()
    $toolbarProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $toolbarProperties.ViewId = 'ToolbarWidget'
    [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($toolbarProperties,$toolbar)
    $vm.RootNodeViewModel.GridNode.ViewProperties = $toolbarProperties
    $layout = [Windows.Controls.Grid]::new(); [void]$layout.Children.Add($native); [void]$layout.Children.Add($view)
    $window = [Windows.Window]::new(); $window.Content = $layout; $window.DataContext = $nativeModel
    $window.Width = 1000; $window.Height = 800; $window.Opacity = 0; $window.ShowInTaskbar = $false; $window.ShowActivated = $false
    $window.Show()
    $window.UpdateLayout(); $view.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    $controls = $toolbar.Content.Content.Children[0].Child.Children
    $pageSelector = $controls | Where-Object { $_ -is [Windows.Controls.ComboBox] }
    if (!$pageSelector -or !$pageSelector.IsEnabled -or $pageSelector.SelectedItem.Id -ne $vm.SelectedPage.Id) { throw 'Toolbar page selector is not clickable/bound' }
    if (![Windows.Shell.WindowChrome]::GetIsHitTestVisibleInChrome($pageSelector)) { throw 'Toolbar selector is swallowed by Playnite window chrome' }
    foreach ($chromeSpace in @($toolbar,$toolbar.Content,$toolbar.Content.Content,$toolbar.Content.Content.Children[0],$toolbar.Content.Content.Children[0].Child)) {
        if ([Windows.Shell.WindowChrome]::GetIsHitTestVisibleInChrome($chromeSpace)) { throw 'Toolbar empty space blocks native caption dragging/resizing' }
    }

    if ($controls[0].Content.Children[0].Content.Child.Text -ne [string][char]0x25C0 -or $controls[2].Content.Children[0].Content.Child.Text -ne [string][char]0x25B6) { throw 'Page arrow glyphs are missing' }
    $manage = $controls | Where-Object { $_ -is [Windows.Controls.Button] -and $_.ToolTip -eq 'Manage dashboard pages' }
    $manage.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent))
    if ($view.FindName('PagesOverlay').Visibility -ne [Windows.Visibility]::Visible) { throw 'Toolbar manage-pages action failed' }
    $view.GetType().GetMethod('ClosePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    $toolbar.Settings.Items.Clear()
    foreach ($id in @('native.search','native.sort','native.details','native.filters','native.search-box','native.progress','native.progress-text',($options | Where-Object { $_.Title.StartsWith('Extension:') } | Select-Object -First 1).Id,'missing.extension','native.notifications')) {
        $entry = [LandingPage.Settings.ToolbarEntry]::new(); $entry.Id = $id; $toolbar.Settings.Items.Add($entry)
    }
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    $controls = $toolbar.Content.Content.Children[0].Child.Children
    if (![Windows.Shell.WindowChrome]::GetIsHitTestVisibleInChrome($controls[0]) -or ![Windows.Shell.WindowChrome]::GetIsHitTestVisibleInChrome($controls[4]) -or [Windows.Shell.WindowChrome]::GetIsHitTestVisibleInChrome($controls[6])) { throw 'Chrome interaction must be limited to interactive toolbar controls' }
    $controls[0].Command.Execute($controls[0].CommandParameter)
    if ($nativeModel.Searches -ne 1) { throw 'Native search command was not forwarded' }
    if ($controls[2].IsEnabled) { throw 'Native CanExecute=false was ignored' }
    if ($controls[3] -is [Windows.Controls.CheckBox]) { throw 'Native panel action must not render as a checkbox' }
    $controls[3].IsChecked = $true
    $controls[3].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent))
    if (!$nativeModel.FilterPanelVisible) { throw 'Native filter toggle binding failed' }
    $controls[4].Text = 'Test query'; $controls[4].GetBindingExpression([Windows.Controls.TextBox]::TextProperty).UpdateSource()
    if ($nativeModel.SearchText -ne 'Test query') { throw 'Native search-box binding failed' }
    if ($controls[5].Value -ne 25 -or $controls[6].Text -ne 'Test operation') { throw 'Native progress/status bindings failed' }
    $controls[7].Command.Execute($controls[7].CommandParameter)
    if ($nativeModel.PluginClicks -ne 1) { throw 'Native extension command was not forwarded' }
    $extensionIcon = $controls[7].Content.Children[0]
    if ($extensionIcon.Content.Child.Text -ne [string][char]0x2605 -or $extensionIcon.Content.Child.FontFamily.Source -ne 'Segoe UI Symbol') { throw 'Actual extension font icon was replaced by a generic symbol' }
    if (![object]::ReferenceEquals($native.PanelMainPluginItems.Children[0].Content,$native.PanelMainPluginItems.Children[0].DataContext.IconObject)) { throw 'Original native extension icon was moved out of the toolbar' }
    if ($controls[8].IsEnabled) { throw 'Missing extension should be unavailable without losing its saved ID' }
    if ($controls[9] -is [Windows.Controls.CheckBox]) { throw 'Notifications must render as a toolbar button' }
    $badge = $controls[9].Content.Children[1]
    if ($badge.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Bell badge must be hidden at zero notifications' }
    $badgeNotification = [Playnite.SDK.NotificationMessage]::new('badge-fixture','Badge test',[Playnite.SDK.NotificationType]::Info)
    $vm.Notifications.Add($badgeNotification)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($badge.Visibility -ne [Windows.Visibility]::Visible -or $badge.Text -ne [string][char]0x25CF) { throw 'Dot badge did not appear for pending notifications' }
    $toolbar.Settings.NotificationBadgeStyle = [LandingPage.Settings.NotificationBadgeStyle]::Count
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $controls = $toolbar.Content.Content.Children[0].Child.Children
    $badge = $controls[9].Content.Children[1]
    if ($badge.Text -ne '1' -or $badge.Visibility -ne [Windows.Visibility]::Visible) { throw 'Count badge setting did not update the bell' }
    $vm.Notifications.Clear()
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($badge.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Count badge stayed visible at zero' }
    $toolbar.Settings.NotificationBadgeStyle = [LandingPage.Settings.NotificationBadgeStyle]::Dot
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $controls = $toolbar.Content.Content.Children[0].Child.Children
    $vm.NotificationsOpen = $false
    $controls[9].Command.Execute($null)
    $window.UpdateLayout()
    if (!$vm.NotificationsOpen -or $view.FindName('NotificationGrid').Visibility -ne [Windows.Visibility]::Visible) { throw 'Notifications toolbar button did not open StartPage notifications' }
    if (@([ToolbarTestVisuals]::Text($view.FindName('NotificationGrid')) | Where-Object { $_.Text -eq 'No notifications' -and $_.Visibility -eq [Windows.Visibility]::Visible }).Count -ne 1) { throw 'Empty notifications panel must show an empty state' }
    $vm.ToggleNotificationsCommand.Execute($null)
    $window.UpdateLayout()
    if ($view.FindName('NotificationGrid').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Minimizing must fully hide notifications without leaving a floating popup' }
    $oldTarget = $native.SortMenu.ReadLocalValue([Windows.Controls.ContextMenu]::PlacementTargetProperty)
    $oldContext = $native.SortMenu.ReadLocalValue([Windows.FrameworkElement]::DataContextProperty)
    $controls[1].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent))
    if (!$native.SortMenu.IsOpen -or ![object]::ReferenceEquals($native.SortMenu.PlacementTarget,$controls[1])) { throw 'Native sort menu did not open at toolbar button' }
    $native.SortMenu.IsOpen = $false
    $restoreDeadline = [datetime]::UtcNow.AddSeconds(2)
    while ($native.SortMenu.ReadLocalValue([Windows.Controls.ContextMenu]::PlacementTargetProperty) -ne $oldTarget -and [datetime]::UtcNow -lt $restoreDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
    if ($native.SortMenu.ReadLocalValue([Windows.Controls.ContextMenu]::PlacementTargetProperty) -ne $oldTarget -or $native.SortMenu.ReadLocalValue([Windows.FrameworkElement]::DataContextProperty) -ne $oldContext) { throw ('Native menu state was not restored after closing. Target: ' + $native.SortMenu.ReadLocalValue([Windows.Controls.ContextMenu]::PlacementTargetProperty) + ' expected: ' + $oldTarget + ' context: ' + $native.SortMenu.ReadLocalValue([Windows.FrameworkElement]::DataContextProperty) + ' expected: ' + $oldContext) }
    $editor = [LandingPage.Views.Settings.ToolbarSettingsView]::new(); $editor.DataContext = $toolbar.Settings
    $editor.Measure([Windows.Size]::new(400,2000)); $editor.Arrange([Windows.Rect]::new(0,0,400,2000))
    if ($editor.Content.Children[0].Foreground.ToString() -ne [Windows.Media.Brushes]::Lime.ToString()) { throw 'Toolbar editor text ignores the theme TextBrush' }
    $idsBefore = @($toolbar.Settings.Items | ForEach-Object Id) -join '|'
    $editor.RefreshOptions()
    if (!$editor.FindName('OptionPicker').ItemTemplate) { throw 'Native item picker must preview toolbar icons' }
    if ((@($toolbar.Settings.Items | ForEach-Object Id) -join '|') -ne $idsBefore) { throw 'Refreshing native items erased saved selections' }
    $editor.Width = 400; [void]$layout.Children.Add($editor)
    $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $rowLabels = @([ToolbarTestVisuals]::Text($editor) | Where-Object { $_.Text -eq 'Group' -or $_.Text -eq 'Custom label (blank = default)' })
    if ($rowLabels.Count -lt 2) { throw 'Regression test did not realize the repeated toolbar item labels' }
    foreach ($label in $rowLabels) {
        if ($label.Foreground.ToString() -ne [Windows.Media.Brushes]::Lime.ToString()) { throw ('Repeated item label ignores the theme color: ' + $label.Text) }
    }
    $layout.Children.Remove($editor)
    $lateSource = $native.PanelMainPluginItems.Children[0]
    $native.PanelMainPluginItems.Children.Clear()
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    $missingButton = $toolbar.Content.Content.Children[0].Child.Children[7]
    if ($missingButton.IsEnabled) { throw 'Missing late-loading extension should begin unavailable' }
    [void]$native.PanelMainPluginItems.Children.Add($lateSource)
    $editor.RefreshOptions()
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $resolvedButton = $toolbar.Content.Content.Children[0].Child.Children[7]
    if (!$resolvedButton.IsEnabled -or $resolvedButton.Content.Children[0].Content.Child.Text -ne [string][char]0x2605) { throw 'Refresh native items did not refresh the actual toolbar extension icon' }
    $native.PanelMainPluginItems.Children.Clear()
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    [void]$native.PanelMainPluginItems.Children.Add($lateSource)
    $toolbar.GetType().GetMethod('RefreshPendingNative',$flags).Invoke($toolbar,@())
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $resolvedButton = $toolbar.Content.Content.Children[0].Child.Children[7]
    if (!$resolvedButton.IsEnabled -or $resolvedButton.Content.Children[0].Content.Child.Text -ne [string][char]0x2605) { throw 'Late native toolbar initialization did not automatically resolve extension icons' }

    $pageC = [LandingPage.Models.DashboardPages]::Add($settingsModel.Settings,'Arrow target')
    $vm.NextPageCommand.Execute($null)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($vm.SelectedPage.Id -ne $pageC.Id -or $vm.NextPageCommand.CanExecute($null)) { throw 'Next-page navigation or final-page boundary failed' }
    $vm.PreviousPageCommand.Execute($null)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($vm.SelectedPage.Id -ne $pageA.Id -or $vm.PreviousPageCommand.CanExecute($null)) { throw 'Previous-page navigation or first-page boundary failed' }
    $toolbar.Settings.Groups[0].Alignment = [Windows.HorizontalAlignment]::Center
    $rightGroup = [LandingPage.Settings.ToolbarGroup]::new(); $rightGroup.Name = 'Extensions'; $rightGroup.Alignment = [Windows.HorizontalAlignment]::Right
    $toolbar.Settings.Groups.Add($rightGroup); $toolbar.Settings.Items[7].GroupId = $rightGroup.Id
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    $groupsGrid = $toolbar.Content.Content
    if ($groupsGrid.ColumnDefinitions.Count -ne 2 -or $groupsGrid.Children[0].Child.HorizontalAlignment -ne [Windows.HorizontalAlignment]::Center -or $groupsGrid.Children[1].Child.HorizontalAlignment -ne [Windows.HorizontalAlignment]::Right) { throw 'Independent toolbar group alignment failed' }
    $styled = $toolbar.Settings.Groups[0]
    $styled.WidthSizing = [LandingPage.Settings.ToolbarGroupSizing]::Fixed; $styled.Width = 220
    $styled.HeightSizing = [LandingPage.Settings.ToolbarGroupSizing]::Fixed; $styled.Height = 70
    $styled.ShowBackground = $true; $styled.Background = '#336699'; $styled.BackgroundOpacity = 0.35
    $styled.BorderColor = '#112233'; $styled.BorderWidth = 2; $styled.CornerRadius = 12; $styled.Padding = 8; $styled.Margin = 3
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    $sizingGrid = $toolbar.Content.Content; $groupBorder = $sizingGrid.Children[0]
    if ($sizingGrid.ColumnDefinitions[0].Width.Value -ne 226 -or $sizingGrid.ColumnDefinitions[0].Width.GridUnitType -ne [Windows.GridUnitType]::Pixel -or $groupBorder.Width -ne 220 -or $groupBorder.Height -ne 70) { throw 'Fixed group sizing/margins failed' }
    if ($groupBorder.Background.Color.ToString() -ne '#FF336699' -or $groupBorder.Background.Opacity -ne 0.35 -or $groupBorder.BorderThickness.Left -ne 2 -or $groupBorder.Padding.Left -ne 8 -or $groupBorder.CornerRadius.TopLeft -ne 12) { throw 'Per-group background/border/padding rendering failed' }
    $styled.WidthSizing = [LandingPage.Settings.ToolbarGroupSizing]::Content
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    if (!$toolbar.Content.Content.ColumnDefinitions[0].Width.IsAuto) { throw 'Fit-content group sizing failed' }
    $styled.WidthSizing = [LandingPage.Settings.ToolbarGroupSizing]::Fill; $styled.Weight = 2.5
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    if (!$toolbar.Content.Content.ColumnDefinitions[0].Width.IsStar -or $toolbar.Content.Content.ColumnDefinitions[0].Width.Value -ne 2.5) { throw 'Proportional group sizing failed' }
    $groupCopy = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($toolbar.Settings), [LandingPage.Settings.ToolbarWidgetSettings])
    if ($groupCopy.Groups.Count -ne 2 -or $groupCopy.Items[7].GroupId -ne $rightGroup.Id -or $groupCopy.Groups[0].Alignment -ne [Windows.HorizontalAlignment]::Center) { throw 'Toolbar groups did not persist independently' }
    if ($groupCopy.Groups[0].BackgroundOpacity -ne 0.35 -or $groupCopy.Groups[0].BorderWidth -ne 2 -or $groupCopy.Groups[0].Weight -ne 2.5) { throw 'Group appearance/sizing did not persist' }
    $styled.ResetAppearance()
    if ($styled.ShowBackground -or $styled.BorderWidth -ne 0 -or $styled.Padding -ne 0 -or $styled.Weight -ne 2.5) { throw 'Group appearance reset must preserve sizing' }
    $extensionOption = $editor.AvailableOptions | Where-Object { $_.Id -eq $toolbar.Settings.Items[7].Id }
    $extensionTitle = $extensionOption.Title
    $pluginSource = $native.PanelMainPluginItems.Children[0]
    $native.PanelMainPluginItems.Children.Clear(); $editor.RefreshOptions()
    if ($toolbar.Settings.Items[7].Id -ne $extensionOption.Id -or $extensionOption.Title -ne $extensionTitle -or !$extensionOption.NativeControl) { throw 'Refresh lost a selected temporarily unavailable extension icon' }
    [void]$native.PanelMainPluginItems.Children.Add($pluginSource)
    $pluginSource.DataContext.Title = 'Renamed extension'; $editor.RefreshOptions()
    if ($toolbar.Settings.Items[7].Id -ne $extensionOption.Id -or $extensionOption.Title -ne 'Extension: Renamed extension') { throw 'Provider-based extension ID changed after renaming the title' }
    $directProvider = [ToolbarTestProvider]::new()
    [DashboardTestProxy]::ExtraPlugins.Add($directProvider)
    $directOptions = [LandingPage.Controls.NativeToolbarCatalog]::Discover($null,$nativeModel)
    $directOption = $directOptions | Where-Object { $_.Title -eq 'Extension: Direct SDK provider' }
    if (!$directOption -or !$directOption.Command.CanExecute($null)) { throw 'Extension SDK toolbar API must work without a native toolbar panel' }
    $directIcon = [LandingPage.Controls.ToolbarIcon]::new(); $directIcon.Option = $directOption
    if ($directIcon.Content.Child.Text -ne [string][char]0x2606) { throw 'Direct extension provider icon was not rendered' }
    $directOption.Command.Execute($null)
    if ($directProvider.Activations -ne 1) { throw 'Direct extension provider action was not invoked' }
    $native.PanelMainPluginItems.Children.Clear()
    $toolbar.Settings.Items[7].Id = $directOption.Id
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    $directButton = $toolbar.Content.Content.Children[1].Child.Children[0]
    if (!$directButton.IsEnabled -or $directButton.Content.Children[0].Content.Child.Text -ne [string][char]0x2606) { throw 'Actual toolbar did not fall back to the SDK extension provider when native buttons were absent' }
    $directProvider.Items[0].Visible = $false
    $toolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($toolbar,@())
    $directButton = $toolbar.Content.Content.Children[1].Child.Children[0]
    if (!$directButton.IsEnabled) { throw 'Native toolbar visibility preference must not disable a configured extension action' }
    $sideWrapper = [ToolbarTestSidebarWrapper]::new(); $sideWrapper.SideItem = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Playnite.SDK.Plugins.SidebarItem])
    $sideWrapper.SideItem.Opened = [Func[Windows.Controls.Control]][Delegate]::CreateDelegate([Func[Windows.Controls.Control]],$directProvider,$directProvider.GetType().GetMethod('OpenPage'))
    $nativeModel.SidebarItems.Add($sideWrapper); $directOption.NativeModel = $nativeModel
    $activationCount = $directProvider.Activations
    $pageCommand = [LandingPage.Controls.NativeToolbarCatalog]::ExtensionCommand($directOption,[LandingPage.Settings.ExtensionOpenMode]::Automatic)
    $pageCommand.Execute($null)
    if (![object]::ReferenceEquals($nativeModel.LastPage,$sideWrapper) -or $directProvider.Activations -ne $activationCount) { throw 'An extension without overlay support must open its real page' }
    $nativeModel.LastPage = $null
    ([LandingPage.Controls.NativeToolbarCatalog]::ExtensionCommand($directOption,[LandingPage.Settings.ExtensionOpenMode]::ExtensionPage)).Execute($null)
    if (![object]::ReferenceEquals($nativeModel.LastPage,$sideWrapper)) { throw 'Explicit extension-page mode did not use native page navigation' }
    $nativeModel.SidebarItems.Clear()
    ([LandingPage.Controls.NativeToolbarCatalog]::ExtensionCommand($directOption,[LandingPage.Settings.ExtensionOpenMode]::ExtensionPage)).Execute($null)
    if ($directProvider.Activations -ne $activationCount + 1) { throw 'Provider with no page must retain its native action' }
    $toolbar.Settings.Items[7].OpenMode = [LandingPage.Settings.ExtensionOpenMode]::ExtensionPage
    $modeCopy = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($toolbar.Settings),[LandingPage.Settings.ToolbarWidgetSettings])
    if ($modeCopy.Items[7].OpenMode -ne [LandingPage.Settings.ExtensionOpenMode]::ExtensionPage) { throw 'Per-item extension launch mode did not persist' }
    # A separate assembly with the real integration's identity verifies the supported
    # overlay route without loading or invoking the user's installed extension.
    $overlayFixturePath = Join-Path $tempRoot 'PlayniteAchievements.dll'
    Add-Type -OutputAssembly $overlayFixturePath -ReferencedAssemblies @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml') -TypeDefinition 'public static class StartPageOverlayTestCallbacks { public static int Calls; public static void OpenOverlay() { ++Calls; } public static System.Windows.Controls.Control OpenPage() { return new System.Windows.Controls.UserControl(); } }'
    $overlayAssembly = [Reflection.Assembly]::LoadFrom($overlayFixturePath)
    $overlayType = $overlayAssembly.GetType('StartPageOverlayTestCallbacks')
    $overlayItem = [Playnite.SDK.Plugins.TopPanelItem]::new(); $overlayItem.Visible = $true
    $overlayItem.Activated = [Action][Delegate]::CreateDelegate([Action],$overlayType.GetMethod('OpenOverlay'))
    $overlaySource = [LandingPage.Controls.ToolbarExtensionSource]::new($overlayItem)
    $overlayOption = [LandingPage.Controls.ToolbarOption]::new(); $overlayOption.Id = 'extension.PlayniteAchievements.0'
    $overlayOption.Command = $overlaySource.Command; $overlayOption.NativeControl = [Windows.Controls.Button]::new(); $overlayOption.NativeControl.DataContext = $overlaySource
    $overlayOption.NativeModel = $nativeModel
    $overlayPage = [ToolbarTestSidebarWrapper]::new(); $overlayPage.SideItem = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Playnite.SDK.Plugins.SidebarItem])
    $overlayPage.SideItem.Opened = [Func[Windows.Controls.Control]][Delegate]::CreateDelegate([Func[Windows.Controls.Control]],$overlayType.GetMethod('OpenPage'))
    $nativeModel.SidebarItems.Add($overlayPage); $nativeModel.LastPage = $null
    ([LandingPage.Controls.NativeToolbarCatalog]::ExtensionCommand($overlayOption,[LandingPage.Settings.ExtensionOpenMode]::Automatic)).Execute($null)
    if ($overlayType.GetField('Calls').GetValue($null) -ne 1 -or $nativeModel.LastPage) { throw 'Supported PlayniteAchievements overlay must retain its original action in automatic mode' }
    ([LandingPage.Controls.NativeToolbarCatalog]::ExtensionCommand($overlayOption,[LandingPage.Settings.ExtensionOpenMode]::ExtensionPage)).Execute($null)
    if ($overlayType.GetField('Calls').GetValue($null) -ne 1 -or ![object]::ReferenceEquals($nativeModel.LastPage,$overlayPage)) { throw 'PlayniteAchievements explicit page mode must bypass its overlay action' }
    $nativeModel.SidebarItems.Clear()
    $anikiPage = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Playnite.SDK.Plugins.SidebarItem])
    $anikiPage.Title = 'Aniki Helper'; $anikiPage.Icon = [Windows.Controls.TextBlock]::new(); $anikiPage.Icon.Text = [string][char]0x2605
    $anikiPage.Opened = [Func[Windows.Controls.Control]][Delegate]::CreateDelegate([Func[Windows.Controls.Control]],$directProvider,$directProvider.GetType().GetMethod('OpenPage'))
    $directProvider.Pages.Add($anikiPage)
    [LandingPage.Controls.NativeToolbarCatalog]::RefreshProviders()
    $earlyWrapper = [ToolbarTestSidebarWrapper]::new(); $earlyWrapper.SideItem = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Playnite.SDK.Plugins.SidebarItem]); $earlyWrapper.SideItem.Title = 'Early native entry'
    $nativeModel.SidebarItems.Add($earlyWrapper)
    $sidebarOptions = [LandingPage.Controls.NativeToolbarCatalog]::Discover($null,$nativeModel)
    $anikiOption = $sidebarOptions | Where-Object Title -eq 'Sidebar: Aniki Helper'
    if (!$anikiOption -or !$anikiOption.SidebarItem) { throw 'Sidebar-only extensions must appear even without a top-toolbar action' }
    $anikiIcon = [LandingPage.Controls.ToolbarIcon]::new(); $anikiIcon.Option = $anikiOption
    if ($anikiIcon.Content.Child.Text -ne [string][char]0x2605) { throw 'Sidebar-only extension icon was not rendered' }
    $nativeModel.SidebarItems.Clear()
    $nativeAniki = [ToolbarTestSidebarWrapper]::new(); $nativeAniki.SideItem = $anikiPage; $nativeModel.SidebarItems.Add($nativeAniki)
    $anikiOption.Command.Execute($null)
    if (![object]::ReferenceEquals($nativeModel.LastPage,$nativeAniki)) { throw 'Sidebar toolbar entry did not select its real extension page' }
    $nativeModel.LastPage = $null
    $nativeAniki.SideItem.Title = 'Renamed Aniki Helper'
    $updatedSidebarOptions = [LandingPage.Controls.NativeToolbarCatalog]::Discover($null,$nativeModel)
    $updatedAniki = $updatedSidebarOptions | Where-Object Id -eq $anikiOption.Id
    if (!$updatedAniki -or $updatedAniki.Title -ne 'Sidebar: Renamed Aniki Helper') { throw 'Sidebar entry ID changed when its title changed' }
    $sidebarEntry = [LandingPage.Settings.ToolbarEntry]::new(); $sidebarEntry.Id = $anikiOption.Id
    if (!$sidebarEntry.IsExtension -or $sidebarEntry.CanChooseOpenMode) { throw 'Sidebar entries should always use their native page/action route' }
    $sidebarCopy = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($sidebarEntry),[LandingPage.Settings.ToolbarEntry])
    if ($sidebarCopy.Id -ne $sidebarEntry.Id) { throw 'Sidebar selection was not persisted' }
    $nativeModel.SidebarItems.Clear()
    [DashboardTestProxy]::ExtraPlugins.Clear()
    # Runtime profile operations use this fixture's temporary settings directory.
    $registry = [Collections.Generic.Dictionary[string,Collections.Generic.List[LandingPage.Models.Layout.StartPageViewArgs]]]::new()
    $available = [Collections.Generic.List[LandingPage.Models.Layout.StartPageViewArgs]]::new()
    foreach ($widgetId in @('ToolbarWidget','TextWidget','ImageWidget','DigitalClock')) {
        $args = [LandingPage.Models.Layout.StartPageViewArgs]::new(); $args.ViewId = $widgetId; $args.Name = $widgetId; $args.PluginId = $plugin.Id; $args.AllowMultipleInstances = $true
        $available.Add($args)
    }
    $registry.Add('StartPage',$available); $plugin.AllAvailableViews = $registry
    Set-Field $plugin 'shelvesViewModels' ([Collections.Generic.Dictionary[guid,LandingPage.ViewModels.ShelvesViewModel]]::new())
    $profileMainId = $vm.SelectedPage.Id
    $toolbar.Settings.Groups[0].Name = 'Snapshot group'
    $missingPage = [LandingPage.Models.DashboardPages]::Add($settingsModel.Settings,'Missing provider page')
    $missingPage.Layout.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new()
    $missingPage.Layout.ViewProperties.PluginId = [guid]::NewGuid(); $missingPage.Layout.ViewProperties.ViewId = 'UninstalledWidget'
    $savedProfile = $vm.SaveDashboardProfile('Full profile fixture')
    $toolbar.Settings.Groups[0].Name = 'Changed live group'
    $rootBeforeImport = $vm.RootNodeViewModel
    $importedProfile = $vm.ImportDashboardProfile([LandingPage.Models.DashboardProfiles]::Export($savedProfile))
    if (![object]::ReferenceEquals($rootBeforeImport,$vm.RootNodeViewModel) -or $vm.Profiles.Count -ne 2) { throw 'Import must save a profile without applying it' }
    $importedProfile.Snapshot.ActivePageId = $missingPage.Id
    Wait-Task ($vm.ApplyDashboardProfileAsync($importedProfile))
    if ($vm.Profiles.Count -ne 3 -or $vm.SelectedPage.Name -ne 'Missing provider page' -or !$vm.RootNodeViewModel.GridNode.ViewProperties) { throw 'Applying a complete profile failed or did not save a backup' }
    $missingControl = $vm.RootNodeViewModel.GridNode.ViewProperties.GetType().GetField('view',$flags).GetValue($vm.RootNodeViewModel.GridNode.ViewProperties)
    if ($missingControl.Tag -ne 'StartPage.MissingWidget') { throw 'Missing provider must retain a placeholder and saved widget data' }
    $profileBackup = $vm.Profiles | Where-Object { $_.Name.StartsWith('Before applying') } | Select-Object -First 1
    $backupToolbar = $profileBackup.Snapshot.Pages | ForEach-Object { $_.Layout.Enumerate() } | Where-Object { $_.ViewProperties.ViewId -eq 'ToolbarWidget' } | Select-Object -First 1
    if ($backupToolbar.ViewProperties.ToolbarSettings.Groups[0].Name -ne 'Changed live group') { throw 'Automatic profile backup did not capture current edits' }
    Wait-Task ($vm.ApplyDashboardProfileAsync($savedProfile))
    if ($vm.RootNodeViewModel.GridNode.ViewProperties.ToolbarSettings.Groups[0].Name -ne 'Snapshot group') { throw 'Applying saved profile did not restore independent toolbar settings' }
    $savedText = [LandingPage.Models.DashboardProfiles]::Export($savedProfile)
    $vm.RootNodeViewModel.GridNode.ViewProperties.ToolbarSettings.Groups[0].Name = 'Edit after applying'
    if ([LandingPage.Models.DashboardProfiles]::Export($savedProfile) -ne $savedText) { throw 'Runtime apply aliases saved profile snapshot objects' }
    $vm.GetType().GetProperty('SelectedProfile').SetValue($vm,$savedProfile)
    $view.ShowProfiles()
    if ($view.FindName('ProfilesOverlay').Visibility -ne [Windows.Visibility]::Visible) { throw 'Profiles manager did not open inside StartPage' }
    $view.GetType().GetMethod('CloseProfiles_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    if ($view.FindName('ProfilesOverlay').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Profiles manager did not close' }
    $externalLivePage = [LandingPage.Models.DashboardPages]::Add($settingsModel.Settings,'Live external page')
    $externalLive = [LandingPage.Models.Layout.ViewProperties]::new(); $externalLive.PluginId = [guid]::NewGuid(); $externalLive.ViewId = 'ExistingExternal'
    $externalProbe = [DashboardTestWidget]::new(); [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($externalLive,$externalProbe)
    $externalLivePage.Layout.ViewProperties = $externalLive
    Wait-Task ($vm.SwitchPageAsync($externalLivePage))
    $externalProfile = $vm.SaveDashboardProfile('External reuse fixture')
    Wait-Task ($vm.ApplyDashboardProfileAsync($externalProfile))
    $restoredExternal = [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).GetValue($vm.RootNodeViewModel.GridNode.ViewProperties)
    if (![object]::ReferenceEquals($externalProbe,$restoredExternal) -or $externalProbe.OpenCount -lt 2 -or $externalProbe.CloseCount -lt 1) { throw 'Applying profile did not preserve/reopen an existing external widget instance' }
    $profileCountBeforeInvalid = $vm.Profiles.Count; $invalidProfile = [LandingPage.Models.DashboardProfiles]::Clone($savedProfile); $invalidProfile.Version = 99
    Wait-Task ($vm.ApplyDashboardProfileAsync($invalidProfile))
    if ($vm.Profiles.Count -ne $profileCountBeforeInvalid -or ![object]::ReferenceEquals($externalProbe,[LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).GetValue($vm.RootNodeViewModel.GridNode.ViewProperties))) { throw 'Invalid profile changed the running dashboard' }
    'PASS: runtime profile save/import/apply, automatic backups, independent snapshots, missing-provider placeholders, external instance reuse/lifecycle, invalid-profile isolation, and in-app manager.'
    $view.ShowProfiles(); $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $starterPreviews = @([ToolbarTestVisuals]::Previews($view.FindName('ProfilesOverlay')))
    if ($starterPreviews.Count -ne 4 -or @($starterPreviews | Where-Object { !$_.Layout -or !$_.Shelves }).Count -ne 0) { throw 'Starter gallery preview layout/shelf bindings failed' }
    $starterButtons = @([ToolbarTestVisuals]::Buttons($view.FindName('ProfilesOverlay')) | Where-Object { $_.Content -eq 'Add profile' })
    if ($starterButtons.Count -ne 3 -or @($starterButtons | Where-Object { !$_.Command -or !$_.CommandParameter }).Count -ne 0) { throw 'Starter gallery cards are missing their commands or template parameters' }
    $minimalCard = $starterButtons | Where-Object { $_.CommandParameter.Key -eq 'minimal' }
    $recipe = $minimalCard.CommandParameter
    $rootBeforeTemplate = $vm.RootNodeViewModel; $profileCount = $vm.Profiles.Count
    $minimalCard.Command.Execute($recipe)
    if ($vm.Profiles.Count -ne $profileCount + 1 -or ![object]::ReferenceEquals($rootBeforeTemplate,$vm.RootNodeViewModel)) { throw 'Adding starter profile must not replace the current dashboard' }
    $vm.SelectedProfile.Snapshot.Pages[0].Name = 'Edited saved template'
    if ($recipe.Profile.Snapshot.Pages[0].Name -ne 'Home') { throw 'Saved starter copy mutated the gallery recipe' }
    # Reproduce a removed Most Played widget still watching settings during a starter apply.
    $activityModel = [LandingPage.ViewModels.GameActivity.GameActivityViewModel].GetConstructors()[0].Invoke([object[]]@($null,$api,$settingsModel))
    $mostPlayed = [LandingPage.ViewModels.GameActivity.MostPlayedViewModel].GetConstructors()[0].Invoke([object[]]@($api,$settingsModel,$activityModel))
    $mostPlayedPage = [LandingPage.Models.DashboardPages]::Add($settingsModel.Settings,'Second page with Most Played')
    $mostProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $mostProperties.PluginId = $plugin.Id; $mostProperties.ViewId = 'MostPlayed'
    $mostView = [Windows.Controls.Border]::new(); $mostView.DataContext = $mostPlayed
    [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($mostProperties,$mostView)
    $mostPlayedPage.Layout.ViewProperties = $mostProperties
    Set-Field $plugin 'mostPlayedViewModel' $mostPlayed
    Wait-Task ($vm.SwitchPageAsync($mostPlayedPage))
    $pendingMostPlayedUpdate = $mostPlayed.UpdateMostPlayedAsync()
    # Profiles already exported by older builds may explicitly contain null options.
    $recipe.Profile.Snapshot.SharedSettings['MostPlayedOptions'] = [Newtonsoft.Json.Linq.JValue]::CreateNull()
    $applyCard = [ToolbarTestVisuals]::Buttons($view.FindName('ProfilesOverlay')) | Where-Object { $_.Content -eq 'Apply' -and $_.CommandParameter.Key -eq 'minimal' } | Select-Object -First 1
    $profileCount = $vm.Profiles.Count
    $vm.AppendProfilePages = $false # Explicit replacement remains available.
    $applyCard.Command.Execute($applyCard.CommandParameter)
    $applyDeadline = [datetime]::UtcNow.AddSeconds(15)
    while ($vm.IsSwitchingPage -or $vm.IsLoading) { if ([datetime]::UtcNow -gt $applyDeadline) { throw 'Starter profile apply timed out' }; $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 5 }
    Wait-Task $pendingMostPlayedUpdate
    if (!$mostPlayed.GetType().GetField('closed',$flags).GetValue($mostPlayed) -or $mostPlayed.GetType().GetField('observedSettings',$flags).GetValue($mostPlayed) -or $mostPlayed.GetType().GetField('observedOptions',$flags).GetValue($mostPlayed)) { throw 'Removed Most Played widget retained settings subscriptions' }
    $normalizedMostPlayed = Get-Setting $settingsModel.Settings 'MostPlayedOptions'
    if (!$normalizedMostPlayed -or $normalizedMostPlayed.Count -ne 3) { throw 'Older null Most Played profile settings were not normalized safely' }
    Wait-Task ($mostPlayed.UpdateMostPlayedAsync())
    $activityModel.GetType().GetField('timer',$flags).GetValue($activityModel).Stop()
    if ($vm.SelectedPage.Name -ne 'Home' -or $vm.Profiles.Count -ne $profileCount + 2) { throw ('Applying starter failed or missed its automatic backup: ' + $vm.ProfileStatus) }
    if ($recipe.Profile.Snapshot.Pages[0].Name -ne 'Home') { throw 'Applying starter mutated its source recipe' }
    $view.GetType().GetMethod('CloseProfiles_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    'PASS: starter gallery previews/commands, add-without-applying, independent template copies, and apply with automatic backup.'
    $localSettings = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.LandingPageSettingsViewModel]); $localSettings.Settings = [LandingPage.LandingPageSettings]::new()
    $localActivity = [LandingPage.ViewModels.GameActivity.GameActivityViewModel].GetConstructors()[0].Invoke([object[]]@($null,$api,$localSettings))
    $localMost = [LandingPage.ViewModels.GameActivity.MostPlayedViewModel].GetConstructors()[0].Invoke([object[]]@($api,$localSettings,$localActivity))
    $oldMostOptions = Get-Setting $localSettings.Settings 'MostPlayedOptions'
    Set-Setting $localSettings.Settings 'MostPlayedOptions' $null
    Wait-Task ($localMost.UpdateMostPlayedAsync())
    $revision = $localMost.GetType().GetField('settingsRevision',$flags).GetValue($localMost)
    $oldMostOptions[0].SkippedGames = 9
    if ($localMost.GetType().GetField('settingsRevision',$flags).GetValue($localMost) -ne $revision) { throw 'Most Played still observes options from a replaced collection' }
    $oldSettingsObject = $localSettings.Settings
    $localSettings.Settings = [LandingPage.LandingPageSettings]::new()
    Wait-Task ($localMost.UpdateMostPlayedAsync())
    $revision = $localMost.GetType().GetField('settingsRevision',$flags).GetValue($localMost)
    Set-Setting $oldSettingsObject 'SkipGamesInPreviousMostPlayed' $true
    if ($localMost.GetType().GetField('settingsRevision',$flags).GetValue($localMost) -ne $revision) { throw 'Most Played still observes a replaced settings object' }
    $localMost.OnViewClosed()
    Set-Setting $localSettings.Settings 'MostPlayedOptions' $null
    Wait-Task ($localMost.UpdateMostPlayedAsync())
    $localActivity.GetType().GetField('timer',$flags).GetValue($localActivity).Stop()
    'PASS: Most Played on a second page, starter apply with legacy null options, removed-widget subscriptions, in-flight refresh cancellation, and settings/collection replacement.'
    # Append mode must preserve page/control identities and shared settings.
    $vm.AppendProfilePages = $true
    $navigationTimer = $vm.GetType().GetField('navigationSaveTimer',$flags).GetValue($vm)
    if (!$navigationTimer) { throw 'Page navigation never initialized its deferred save' }
    $vm.SaveCurrentPage()
    if ($navigationTimer.IsEnabled) { throw 'Explicit save did not flush deferred navigation persistence' }
    $beforePages = @($vm.Pages)
    $beforeRoot = $vm.RootNodeViewModel
    $beforeActive = $vm.SelectedPage.Id
    $beforePadding = Get-Setting $settingsModel.Settings 'Padding'
    $appendRecipe = $vm.StarterProfiles | Where-Object { $_.Key -eq 'library' }
    $appendProfile = $vm.AddStarterProfile($appendRecipe)
    $vm.SelectedProfile = $appendProfile
    $vm.ApplyDashboardProfileCommand.Execute($null)
    $deadline = [datetime]::UtcNow.AddSeconds(15)
    while ($vm.IsSwitchingPage -or $vm.IsLoading) { if ([datetime]::UtcNow -gt $deadline) { throw 'Append timed out' }; $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle) }
    if ($vm.Pages.Count -ne $beforePages.Count + 2 -or $vm.SelectedPage.Id -ne $beforeActive -or ![object]::ReferenceEquals($vm.RootNodeViewModel,$beforeRoot)) { throw 'Appending replaced existing dashboard state' }
    foreach ($page in $beforePages) { if (!$vm.Pages.Contains($page)) { throw 'Appending removed an existing page' } }
    if ((Get-Setting $settingsModel.Settings 'Padding') -ne $beforePadding) { throw 'Appending changed shared appearance settings' }
    $firstAppendIds = @($vm.Pages | ForEach-Object { $_.Id })
    Wait-Task ($vm.ApplyDashboardProfileAsync($appendProfile,$true))
    if ($vm.Pages.Count -ne $beforePages.Count + 4 -or @($vm.Pages | Group-Object Id | Where-Object Count -gt 1).Count -or @($vm.Pages | Group-Object Name | Where-Object Count -gt 1).Count) { throw 'Repeated append did not create independent, uniquely named pages' }
    $appendedPage = $vm.Pages[$beforePages.Count]
    Wait-Task ($vm.SwitchPageAsync($appendedPage))
    if ($vm.SelectedPage.Id -ne $appendedPage.Id) { throw 'Appended page cannot be opened' }
    $view.ShowProfiles(); $window.UpdateLayout()
    $list = $view.FindName('SavedProfilesList')
    if ($list.SelectionMode -ne [Windows.Controls.SelectionMode]::Extended) { throw 'Profiles list does not support multi-selection' }
    $list.SelectedItems.Clear()
    $deleteTargets = @($vm.Profiles | Select-Object -First 2)
    foreach ($profile in $deleteTargets) { [void]$list.SelectedItems.Add($profile) }
    if ($vm.SelectedProfiles.Count -ne 2) { throw 'Profile multi-selection did not reach the view model' }
    $rootBeforeDelete = $vm.RootNodeViewModel; $pagesBeforeDelete = $vm.Pages.Count; $countBeforeDelete = $vm.Profiles.Count
    $vm.RequestDeleteProfileCommand.Execute($null)
    $vm.DeleteDashboardProfileCommand.Execute($null)
    if ($vm.Profiles.Count -ne $countBeforeDelete - 2 -or $vm.Pages.Count -ne $pagesBeforeDelete -or ![object]::ReferenceEquals($rootBeforeDelete,$vm.RootNodeViewModel)) { throw 'Bulk deletion failed or changed the active dashboard' }
    foreach ($profile in $deleteTargets) { if ($vm.Profiles.Contains($profile)) { throw 'A selected profile was not deleted' } }
    'PASS: append default/command, existing controls/pages/settings preserved, repeated independent append, page navigation, and multi-select deletion through the real list.'
    $refreshShelf = [LandingPage.ViewModels.ShelveViewModel].GetConstructors()[0].Invoke([object[]]@([LandingPage.Models.ShelveProperties]::new(),[LandingPage.Settings.ShelvesSettings]::new(),$api,[Collections.ObjectModel.ObservableCollection[LandingPage.ViewModels.ShelveViewModel]]::new(),$null))
    $gameA = [Playnite.SDK.Models.Game]::new(); $gameA.Name = 'Stable cover A'
    $gameB = [Playnite.SDK.Models.Game]::new(); $gameB.Name = 'Stable cover B'
    $selection = [Collections.Generic.List[Playnite.SDK.Models.Game]]::new(); $selection.Add($gameA); $selection.Add($gameB)
    $applyGames = $refreshShelf.GetType().GetMethod('ApplyGameSelection',$flags)
    [void]$applyGames.Invoke($refreshShelf,[object[]](,$selection))
    $stableA = $refreshShelf.Games[0]; $stableB = $refreshShelf.Games[1]
    $script:shelfChanges = 0; $script:shelfResets = 0
    $listener = [Collections.Specialized.NotifyCollectionChangedEventHandler]{ param($sender,$eventArgs) $script:shelfChanges++; if ($eventArgs.Action -eq 'Reset') { $script:shelfResets++ } }
    $refreshShelf.Games.add_CollectionChanged($listener)
    for ($i = 0; $i -lt 5; ++$i) { [void]$applyGames.Invoke($refreshShelf,[object[]](,$selection)) }
    if ($script:shelfChanges -ne 0 -or ![object]::ReferenceEquals($stableA,$refreshShelf.Games[0])) { throw 'Unchanged shelf refresh invalidates covers/containers' }
    $selection.Reverse(); [void]$applyGames.Invoke($refreshShelf,[object[]](,$selection))
    if ($script:shelfResets -ne 0 -or ![object]::ReferenceEquals($stableB,$refreshShelf.Games[0])) { throw 'Shelf reorder recreated models or reset the view' }
    [void]$selection.Remove($gameA); [void]$applyGames.Invoke($refreshShelf,[object[]](,$selection))
    if ($refreshShelf.Games.Count -ne 1 -or $script:shelfResets -ne 0) { throw 'Incremental shelf removal failed' }
    $refreshShelf.Games.Add([LandingPage.Models.GameModel]::new($gameB))
    [void]$applyGames.Invoke($refreshShelf,[object[]](,$selection))
    if ($refreshShelf.Games.Count -ne 1) { throw 'Duplicate placeholder models survived shelf reconciliation' }
    $currentGroup = [Collections.ObjectModel.ObservableCollection[LandingPage.Models.GameModel]]::new(); $currentGroup.Add($stableB)
    $incomingGroup = [Collections.ObjectModel.ObservableCollection[LandingPage.Models.GameModel]]::new(); $incomingGroup.Add([LandingPage.Models.GameModel]::new($gameB))
    $script:mostGroupChanges = 0
    $groupListener = [Collections.Specialized.NotifyCollectionChangedEventHandler]{ $script:mostGroupChanges++ }
    $currentGroup.add_CollectionChanged($groupListener)
    $updateGroup = [LandingPage.ViewModels.GameActivity.MostPlayedViewModel].GetMethod('UpdateGroupGames',[Reflection.BindingFlags]'Static,NonPublic')
    [void]$updateGroup.Invoke($null,[object[]]@($currentGroup,$incomingGroup))
    if ($script:mostGroupChanges -ne 0 -or ![object]::ReferenceEquals($stableB,$currentGroup[0])) { throw 'Most Played refresh recreated an unchanged cover' }
    $refreshShelf.OnViewClosed()
    $previousProvider = Get-Setting $settingsModel.Settings 'SelectedAchievementsProvider'
    Set-Setting $settingsModel.Settings 'SelectedAchievementsProvider' ([LandingPage.Settings.AchievementsProvider]::SuccessStory)
    $progressDirectory = Join-Path $tempRoot 'cebe6d32-8c46-4459-b993-5a5189d60788/SuccessStory'
    [void][IO.Directory]::CreateDirectory($progressDirectory)
    $progressFile = Join-Path $progressDirectory ($gameB.Id.ToString() + '.json')
    [IO.File]::WriteAllText($progressFile,'{"Total":10,"Unlocked":4}')
    $info = [LandingPage.Views.ShelfGameInfo]::new()
    $info.Settings = [LandingPage.Models.ShelveProperties]::new(); $info.Settings.ShowAchievementProgress = $true
    $info.Model = $stableB
    [void]$layout.Children.Add($info); $window.UpdateLayout()
    $panel = $info.Content
    $deadline = [datetime]::UtcNow.AddSeconds(10)
    while (@($panel.Children | Where-Object { $_ -is [Windows.Controls.ProgressBar] }).Count -eq 0) {
        if ([datetime]::UtcNow -gt $deadline) { throw 'Achievement progress failed to load without hover' }
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 5
    }
    $bar = $panel.Children | Where-Object { $_ -is [Windows.Controls.ProgressBar] } | Select-Object -First 1
    $refreshInfo = $info.GetType().GetMethod('Refresh',$flags)
    [void]$refreshInfo.Invoke($info,@($true))
    if (!$panel.Children.Contains($bar)) { throw 'Achievement poll clears existing progress while awaiting data' }
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    Start-Sleep -Milliseconds 30
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if (!$panel.Children.Contains($bar) -or $bar.Value -ne 40) { throw 'Unchanged achievement poll replaces the existing bar' }
    [IO.File]::WriteAllText($progressFile,'{"Total":10,"Unlocked":7}')
    [void]$refreshInfo.Invoke($info,@($true))
    $deadline = [datetime]::UtcNow.AddSeconds(10)
    do {
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
        $newBar = $panel.Children | Where-Object { $_ -is [Windows.Controls.ProgressBar] } | Select-Object -First 1
        if ([datetime]::UtcNow -gt $deadline) { throw 'Achievement progress did not update without hover' }
    } while ($newBar.Value -ne 70)
    [void]$layout.Children.Remove($info)
    Set-Setting $settingsModel.Settings 'SelectedAchievementsProvider' $previousProvider
    'PASS: unchanged shelf refresh emits no collection changes, incremental reorder/removal preserves models, and achievement polls keep progress visible and update without hover.'
    $previousButton = [Windows.Controls.Button]::new(); $previousButton.Command = $vm.PreviousPageCommand
    $nextButton = [Windows.Controls.Button]::new(); $nextButton.Command = $vm.NextPageCommand
    [void]$layout.Children.Add($previousButton); [void]$layout.Children.Add($nextButton)
    Wait-Task ($vm.SwitchPageAsync($vm.Pages[0]))
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($previousButton.IsEnabled -or !$nextButton.IsEnabled) { throw 'Navigation buttons did not refresh after switch to first page without mouse input' }
    Wait-Task ($vm.SwitchPageAsync($vm.Pages[$vm.Pages.Count - 1]))
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if (!$previousButton.IsEnabled -or $nextButton.IsEnabled) { throw 'Navigation buttons did not refresh after switch to last page without mouse input' }
    $applyButton = [Windows.Controls.Button]::new(); $applyButton.Command = $vm.ApplyDashboardProfileCommand
    $confirmPageButton = [Windows.Controls.Button]::new(); $confirmPageButton.Command = $vm.DeletePageCommand
    $confirmProfileButton = [Windows.Controls.Button]::new(); $confirmProfileButton.Command = $vm.DeleteDashboardProfileCommand
    foreach ($button in @($applyButton,$confirmPageButton,$confirmProfileButton)) { [void]$layout.Children.Add($button) }
    $vm.SelectedProfile = $null
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($applyButton.IsEnabled) { throw 'Apply button did not disable after profile selection cleared' }
    $vm.SelectedProfile = $vm.Profiles[0]
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if (!$applyButton.IsEnabled) { throw 'Apply button did not enable after programmatic profile selection' }
    $vm.ConfirmPageDeletion = $true
    $vm.SetSelectedProfiles([LandingPage.Models.DashboardProfile[]]@($vm.Profiles[0]))
    $vm.ConfirmDeleteProfile = $true
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if (!$confirmPageButton.IsEnabled -or !$confirmProfileButton.IsEnabled) { throw 'Delete confirmation commands did not refresh without input' }
    $vm.ConfirmPageDeletion = $false; $vm.ConfirmDeleteProfile = $false
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($confirmPageButton.IsEnabled -or $confirmProfileButton.IsEnabled) { throw 'Cancelled delete confirmation left enabled commands' }
    $active = $vm.SelectedPage; $vm.Pages.Move($vm.Pages.IndexOf($active),0)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($previousButton.IsEnabled -or !$nextButton.IsEnabled) { throw 'Reordering pages left stale navigation state' }
    $nextBackgroundButton = [Windows.Controls.Button]::new(); $nextBackgroundButton.Command = $vm.NextRandomBackgroundCommand
    [void]$layout.Children.Add($nextBackgroundButton)
    Set-Setting $settingsModel.Settings 'BackgroundImagePath' $null
    Set-Setting $settingsModel.Settings 'BackgroundImageSource' ([LandingPage.BackgroundImageSource]::Random)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if (!$nextBackgroundButton.IsEnabled) { throw 'Random background command did not refresh after changing source' }
    Set-Setting $settingsModel.Settings 'BackgroundImageSource' ([LandingPage.BackgroundImageSource]::LastPlayed)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($nextBackgroundButton.IsEnabled) { throw 'Random background command stayed enabled for a non-random source' }
    function Count-RefreshListeners($source,$target) {
        $count = 0; $type = [object].GetMethod('GetType').Invoke($source,@())
        while ($type) {
            foreach ($field in $type.GetFields([Reflection.BindingFlags]'Instance,NonPublic,Public,DeclaredOnly')) {
                if ($field.Name -like '*PropertyChanged*') {
                    $handlers = $field.GetValue($source)
                    if ($handlers -is [Delegate]) { foreach ($handler in $handlers.GetInvocationList()) { if ([object]::ReferenceEquals($handler.Target,$target)) { $count++ } } }
                }
            }
            $type = $type.BaseType
        }
        return $count
    }
    $auditSettingsModel = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.LandingPageSettingsViewModel])
    $auditSettingsModel.Settings = [LandingPage.LandingPageSettings]::new()
    Set-Setting $auditSettingsModel.Settings 'BackgroundImageUri' ([uri]$imagePath)
    $auditPlugin = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.LandingPageExtension])
    [LandingPage.LandingPageExtension].GetProperty('SettingsViewModel',$flags).SetValue($auditPlugin,$auditSettingsModel)
    $auditBackground = [LandingPage.ViewModels.BackgroundViewModel].GetConstructors()[0].Invoke([object[]]@($api,$auditPlugin))
    $oldBackgroundSettings = $auditSettingsModel.Settings
    $replacementSettings = [LandingPage.LandingPageSettings]::new()
    Set-Setting $replacementSettings 'BackgroundImageUri' ([uri]$imagePath)
    $auditSettingsModel.Settings = $replacementSettings
    if ((Count-RefreshListeners $oldBackgroundSettings $auditBackground) -ne 0 -or (Count-RefreshListeners $replacementSettings $auditBackground) -ne 1) { throw 'Background settings replacement leaked or duplicated refresh subscriptions' }
    $beforeUnrelatedHover = [DashboardTestProxy]::LibraryEnumerations
    $auditBackground.LastHoveredGame = $gameA
    if ([DashboardTestProxy]::LibraryEnumerations -ne $beforeUnrelatedHover) { throw 'Hovering rebuilt a background that does not follow hovered games' }
    $auditBackground.Unsubscribe()
    Set-Setting $auditSettingsModel.Settings 'ShelveInstanceSettings' ([Collections.Generic.Dictionary[guid,LandingPage.Settings.ShelvesSettings]]::new())
    Set-Setting $auditSettingsModel.Settings 'ShelveProperties' ([Collections.ObjectModel.ObservableCollection[LandingPage.Models.ShelveProperties]]::new())
    $auditShelves = [LandingPage.ViewModels.ShelvesViewModel].GetConstructors()[0].Invoke([object[]]@($api,$auditPlugin,$auditSettingsModel,[guid]::NewGuid()))
    $oldShelfSettings = $auditShelves.Shelves
    $auditShelves.Shelves = [LandingPage.Settings.ShelvesSettings]::new()
    if ((Count-RefreshListeners $oldShelfSettings $auditShelves) -ne 0 -or (Count-RefreshListeners $auditShelves.Shelves $auditShelves) -ne 1) { throw 'Shelf settings replacement leaked or lost refresh subscriptions' }
    for ($shelfIndex = 0; $shelfIndex -lt 3; ++$shelfIndex) {
        $cachedShelf = [LandingPage.ViewModels.ShelveViewModel].GetConstructors()[0].Invoke([object[]]@([LandingPage.Models.ShelveProperties]::new(),$auditShelves.Shelves,$api,$auditShelves.ShelveViewModels,$null))
        $auditShelves.ShelveViewModels.Add($cachedShelf)
    }
    $queryCount = [DashboardTestProxy]::LibraryEnumerations
    Wait-Task ($auditShelves.OnViewShownAsync())
    $afterInitialQuery = [DashboardTestProxy]::LibraryEnumerations
    if ($afterInitialQuery -ne $queryCount + 1) { throw 'Shelf initial load must enumerate the library once' }
    Wait-Task ($auditShelves.OnViewHiddenAsync())
    Wait-Task ($auditShelves.OnViewShownAsync())
    if ([DashboardTestProxy]::LibraryEnumerations -ne $afterInitialQuery) { throw 'Returning to unchanged shelves unnecessarily queried the library' }
    Wait-Task ($auditShelves.OnViewHiddenAsync())
    Set-Setting $auditSettingsModel.Settings 'IgnoreTagId' ([guid]::NewGuid())
    if ([DashboardTestProxy]::LibraryEnumerations -ne $afterInitialQuery) { throw 'Hidden shelf refresh should be deferred' }
    Wait-Task ($auditShelves.OnViewShownAsync())
    if ([DashboardTestProxy]::LibraryEnumerations -ne $afterInitialQuery + 1) { throw 'Changed shelf settings did not invalidate the cached results' }
    $beforeBurst = [DashboardTestProxy]::LibraryEnumerations
    $refreshBurst = [Collections.Generic.List[Threading.Tasks.Task]]::new()
    for ($request = 0; $request -lt 20; ++$request) { $refreshBurst.Add($auditShelves.UpdateShelvesAsync()) }
    Wait-Task ([Threading.Tasks.Task]::WhenAll($refreshBurst))
    if ([DashboardTestProxy]::LibraryEnumerations -gt $beforeBurst + 2) { throw 'A burst of refresh requests was not coalesced' }
    'PASS: three shelves share one library snapshot, unchanged revisits perform no queries, and hidden filter changes invalidate the cache.'
    $auditShelves.OnViewClosed()
    if ((Count-RefreshListeners $auditShelves.Shelves $auditShelves) -ne 0) { throw 'Closed shelf widget retained refresh subscriptions' }
    'PASS: replaced background/shelf settings detach old listeners and attach exactly once; closing shelves detaches refresh listeners.'
    'PASS: profile selection, delete confirmation/cancellation, page reorder and background-source commands update real buttons without input.'
    'PASS: previous/next button availability refreshes after switching without mouse or keyboard input.'
    # Override only the fixture's animation preference, never the user's Windows settings.
    Set-Field $view 'DashboardAnimationsEnabled' ([Func[bool]]{ return $true })
    $snapshotImage = $view.FindName('DashboardTransitionImage')
    $scene = $view.FindName('DashboardScene')
    Set-Setting $settingsModel.Settings 'DashboardTransitionMilliseconds' 60
    foreach ($transition in @([LandingPage.DashboardTransition]::Fade,[LandingPage.DashboardTransition]::Slide)) {
        Set-Setting $settingsModel.Settings 'DashboardTransition' $transition
        $target = if ($vm.SelectedPage -eq $vm.Pages[0]) { $vm.Pages[1] } else { $vm.Pages[0] }
        $switch = $vm.SwitchPageAsync($target)
        if (!$snapshotImage.Source -or $scene.IsHitTestVisible) { throw 'Transition must retain outgoing dashboard and block old controls during preparation' }
        $snapshot = [Windows.Media.Imaging.BitmapSource]$snapshotImage.Source
        $stride = $snapshot.PixelWidth * 4
        $pixels = [byte[]]::new($stride * $snapshot.PixelHeight)
        $snapshot.CopyPixels($pixels,$stride,0)
        for ($pixel = 3; $pixel -lt $pixels.Length; $pixel += 4) {
            if ($pixels[$pixel] -ne 255) { throw 'Transparent transition snapshot allows the preparing page to bleed through' }
        }

        Wait-Task $switch
        if ($snapshotImage.Source -or !$scene.IsHitTestVisible -or $vm.IsSwitchingPage -or $scene.RenderTransform -ne [Windows.Media.Transform]::Identity) { throw 'Transition completion did not release snapshot/transform/navigation' }
    }
    foreach ($mode in @('SlideFade','ZoomIn','ZoomOut','Wipe','Custom')) {
        Set-Setting $settingsModel.Settings 'DashboardTransition' ([Enum]::Parse([LandingPage.DashboardTransition],$mode))
        $customMotion = Get-Setting $settingsModel.Settings 'DashboardCustomTransition'; $customMotion.DurationMs = 60
        $target = if ($vm.SelectedPage -eq $vm.Pages[0]) { $vm.Pages[1] } else { $vm.Pages[0] }
        Wait-Task ($vm.SwitchPageAsync($target))
        if ($snapshotImage.Source -or !$scene.IsHitTestVisible -or $scene.Opacity -ne 1 -or $snapshotImage.Clip) { throw ('Transition did not clean up: ' + $mode) }
    }
    $preset = Get-Setting $settingsModel.Settings 'DashboardCustomTransition'
    $preset.Name = 'Fixture custom motion'
    $view.GetType().GetMethod('SaveTransitionPreset_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    $savedMotions = Get-Setting $settingsModel.Settings 'DashboardTransitionPresets'
    if ($savedMotions.Count -ne 1) { throw 'Saving custom transition failed' }
    $preset.Incoming.StartX = 25
    if ($savedMotions[0].Incoming.StartX -eq 25) { throw 'Saved custom preset aliases editor settings' }
    $view.GetType().GetMethod('LoadTransitionPreset_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    if ((Get-Setting $settingsModel.Settings 'DashboardCustomTransition').Incoming.StartX -eq 25) { throw 'Loading custom preset failed' }
    $beforePreview = $vm.SelectedPage.Id
    $view.GetType().GetMethod('PreviewTransition_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    $previewDeadline = [datetime]::UtcNow.AddSeconds(10)
    while ($snapshotImage.Source) {
        if ([datetime]::UtcNow -gt $previewDeadline) { throw 'Transition preview did not complete' }
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 5
    }
    if ($vm.SelectedPage.Id -ne $beforePreview -or !$scene.IsHitTestVisible) { throw 'Preview changed the page or left controls blocked' }
    $view.GetType().GetMethod('DeleteTransitionPreset_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    if ($savedMotions.Count -ne 0) { throw 'Deleting custom transition preset failed' }
    Set-Setting $settingsModel.Settings 'DashboardTransition' ([LandingPage.DashboardTransition]::Slide)
    foreach ($direction in @('Left','Right','Up','Down','Automatic')) {
        Set-Setting $settingsModel.Settings 'DashboardSlideDirection' ([Enum]::Parse([LandingPage.DashboardSlideDirection],$direction))
        $target = if ($vm.SelectedPage -eq $vm.Pages[0]) { $vm.Pages[1] } else { $vm.Pages[0] }
        Wait-Task ($vm.SwitchPageAsync($target))
        if ($snapshotImage.Source -or !$scene.IsHitTestVisible) { throw ('Slide direction did not clean up: ' + $direction) }
    }
    Set-Setting $settingsModel.Settings 'DashboardTransition' ([LandingPage.DashboardTransition]::None)
    $target = if ($vm.SelectedPage -eq $vm.Pages[0]) { $vm.Pages[1] } else { $vm.Pages[0] }
    $switch = $vm.SwitchPageAsync($target)
    if ($snapshotImage.Source) { throw 'None transition must not create a snapshot' }
    Wait-Task $switch
    Set-Setting $settingsModel.Settings 'DashboardTransitionMilliseconds' 0
    if ((Get-Setting $settingsModel.Settings 'DashboardTransitionMilliseconds') -ne 50) { throw 'Transition duration minimum was not clamped' }
    Set-Setting $settingsModel.Settings 'DashboardTransitionMilliseconds' 4000
    if ((Get-Setting $settingsModel.Settings 'DashboardTransitionMilliseconds') -ne 1000) { throw 'Transition duration maximum was not clamped' }
    Set-Field $view 'DashboardAnimationsEnabled' ([Func[bool]]{ return $false })
    Set-Setting $settingsModel.Settings 'DashboardTransition' ([LandingPage.DashboardTransition]::Fade)
    Set-Setting $settingsModel.Settings 'DashboardRespectWindowsAnimations' $true
    $target = if ($vm.SelectedPage -eq $vm.Pages[0]) { $vm.Pages[1] } else { $vm.Pages[0] }
    $switch = $vm.SwitchPageAsync($target)
    if ($snapshotImage.Source) { throw 'Disabled Windows animations must suppress transitions when respected' }
    Wait-Task $switch
    Set-Setting $settingsModel.Settings 'DashboardRespectWindowsAnimations' $false
    Set-Setting $settingsModel.Settings 'DashboardTransitionMilliseconds' 60
    $target = if ($vm.SelectedPage -eq $vm.Pages[0]) { $vm.Pages[1] } else { $vm.Pages[0] }
    $switch = $vm.SwitchPageAsync($target)
    if (!$snapshotImage.Source) { throw 'Explicit animation override did not permit transition' }
    # Closing/unloading during preparation must release snapshots and navigation.
    $view.GetType().GetMethod('ResetDashboardTransition',$flags).Invoke($view,@())
    Wait-Task $switch
    if ($snapshotImage.Source -or !$scene.IsHitTestVisible -or $vm.IsSwitchingPage) { throw 'Interrupted transition left snapshot/navigation stuck' }
    'PASS: real fade/slide lifecycle, all slide directions, None mode, outgoing snapshot retention, cleanup/navigation release, and duration bounds (animations follow Windows preference).'
    $notificationAnchor = [Windows.Controls.Button]::new()
    $notificationAnchor.Width = 32; $notificationAnchor.Height = 28
    $notificationAnchor.HorizontalAlignment = [Windows.HorizontalAlignment]::Left
    $notificationAnchor.VerticalAlignment = [Windows.VerticalAlignment]::Top
    $notificationAnchor.Margin = [Windows.Thickness]::new(75,95,0,0)
    [void]$view.Content.Children.Add($notificationAnchor); $window.UpdateLayout()
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $vm.NotificationsOpen = $false
    $view.GetType().GetMethod('ToggleNotificationsAt',$flags).Invoke($view,@($notificationAnchor))
    $window.UpdateLayout()
    $point = $notificationAnchor.TranslatePoint([Windows.Point]::new(0,$notificationAnchor.ActualHeight),$view)
    $notificationGrid = $view.FindName('NotificationGrid')
    if ([Math]::Abs($notificationGrid.Margin.Left - [Math]::Max(8,[Math]::Min($point.X + $notificationAnchor.ActualWidth / 2 - $notificationGrid.Width / 2,$view.ActualWidth - $notificationGrid.Width - 8))) -gt 1 -or [Math]::Abs($notificationGrid.Margin.Top - $point.Y - 6) -gt 1) { throw ('Notifications are not anchored below the opening button: margin=' + $notificationGrid.Margin + ' anchor=' + $point + ' visible=' + $notificationGrid.Visibility + ' loaded=' + $notificationAnchor.IsLoaded) }
    $notificationAnchor.Margin = [Windows.Thickness]::new(180,150,0,0); $window.UpdateLayout()
    $point = $notificationAnchor.TranslatePoint([Windows.Point]::new(0,$notificationAnchor.ActualHeight),$view)
    if ([Math]::Abs($notificationGrid.Margin.Top - $point.Y - 6) -gt 1) { throw 'Notification panel did not follow a moved toolbar anchor' }
    $notificationAnchor.Margin = [Windows.Thickness]::new($view.ActualWidth / 2 - 16,150,0,0); $window.UpdateLayout()
    $point = $notificationAnchor.TranslatePoint([Windows.Point]::new(0,$notificationAnchor.ActualHeight),$view)
    if ([Math]::Abs(($notificationGrid.Margin.Left + $notificationGrid.Width / 2) - ($point.X + $notificationAnchor.ActualWidth / 2)) -gt 1) { throw 'Notification panel is not centered beneath a bell away from the edges' }
    $notificationAnchor.Margin = [Windows.Thickness]::new($view.ActualWidth - 40,150,0,0); $window.UpdateLayout()
    if ($notificationGrid.Margin.Left -lt 8 -or $notificationGrid.Margin.Left + $notificationGrid.Width -gt $view.ActualWidth - 7) { throw 'Notification panel extends beyond the window edge' }
    $vm.NotificationsOpen = $false
    [void]$view.Content.Children.Remove($notificationAnchor)
    $message = [Playnite.SDK.NotificationMessage]::new('test-notification','Test notification',[Playnite.SDK.NotificationType]::Info)
    $vm.Notifications.Add($message)
    if ($vm.NotificationCount -ne 1 -or $vm.NotificationsOpen) { throw 'Minimized notifications should update count without reopening the popup' }
    $vm.ToggleNotificationsCommand.Execute($null)
    $window.UpdateLayout()
    if (!$vm.NotificationsOpen -or $view.FindName('NotificationItemsControl').Items.Count -ne 1) { throw 'Bell did not show pending notifications' }
    $vm.Notifications.Clear(); $window.UpdateLayout()
    if ($vm.NotificationCount -ne 0 -or !$vm.NotificationsOpen) { throw 'Clearing notifications should retain an open empty-state panel' }
    $vm.ToggleNotificationsCommand.Execute($null)
    $diagnosticFile = $settingsModel.DebugLogPath
    if ([IO.File]::Exists($diagnosticFile)) { throw 'Disabled performance diagnostics wrote a log' }
    Set-Setting $settingsModel.Settings 'EnableDashboardDiagnostics' $true
    $extensionLogger = [LandingPage.LandingPageExtension].GetField('logger',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
    $extensionLogger.Info('Fixture extension-wide information')
    $extensionLogger.Debug('Fixture extension-wide debug')
    $extensionLogger.Trace('Fixture extension-wide trace')
    $extensionLogger.Warn([InvalidOperationException]::new('Fixture exception details'),'Fixture extension-wide warning')
    $extensionLogger.Error('Fixture extension-wide error')

    $target = if ($vm.SelectedPage -eq $vm.Pages[0]) { $vm.Pages[1] } else { $vm.Pages[0] }
    Wait-Task ($vm.SwitchPageAsync($target))
    if (![IO.File]::Exists($diagnosticFile) -or ![IO.File]::ReadAllText($diagnosticFile).Contains('Switch complete')) { throw 'Enabled performance diagnostics did not record a completed switch' }
    $debugText = [IO.File]::ReadAllText($diagnosticFile)
    foreach ($entry in @('Fixture extension-wide information','Fixture extension-wide debug','Fixture extension-wide trace','Fixture extension-wide warning','Fixture extension-wide error','Fixture exception details')) {
        if (!$debugText.Contains($entry)) { throw ('Extension debug log missed: ' + $entry) }
    }
    $diagnosticLength = ([IO.FileInfo]$diagnosticFile).Length
    Set-Setting $settingsModel.Settings 'EnableDashboardDiagnostics' $false
    $extensionLogger.Info('This message must not enter the disabled dedicated log')
    $target = if ($vm.SelectedPage -eq $vm.Pages[0]) { $vm.Pages[1] } else { $vm.Pages[0] }
    Wait-Task ($vm.SwitchPageAsync($target))
    if (([IO.FileInfo]$diagnosticFile).Length -ne $diagnosticLength) { throw 'Disabled diagnostics kept writing logs' }
    'PASS: bell opens empty and populated notifications, minimizing fully hides panel, count updates, and opt-in diagnostics writes only when enabled.'
    $selectorToolbar = [LandingPage.Views.ToolbarWidgetView]::new()
    $selectorToolbar.Settings = [LandingPage.Settings.ToolbarWidgetSettings]::new()
    $selectorToolbar.Settings.Items.Clear()
    $selectorEntry = [LandingPage.Settings.ToolbarEntry]::new(); $selectorEntry.Id = 'page.select'; $selectorToolbar.Settings.Items.Add($selectorEntry)
    [void]$view.Content.Children.Add($selectorToolbar); $window.UpdateLayout()
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $selectorToolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($selectorToolbar,@())
    $replacementSelector = $selectorToolbar.Content.Content.Children[0].Child.Children[0]
    $selectedBeforeRestore = $vm.SelectedPage.Id
    $rootBeforeRestore = $vm.RootNodeViewModel
    $settingsCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.LandingPageSettings]).Invoke($null,@($settingsModel.Settings))
    $settingsModel.Settings = $settingsCopy
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if (!$replacementSelector.SelectedItem -or $replacementSelector.SelectedItem.Id -ne $selectedBeforeRestore -or !$vm.Pages.Contains($replacementSelector.SelectedItem)) { throw 'Settings restoration left the dashboard selector blank or bound to an obsolete page object' }
    if (![object]::ReferenceEquals($rootBeforeRestore,$vm.RootNodeViewModel)) { throw 'Settings restoration rebuilt the cached page layout' }
    [void]$view.Content.Children.Remove($selectorToolbar)
    'PASS: restoring cloned settings preserves the current dashboard selector by stable page ID and keeps the cached layout.'
    $featuredGame = [Playnite.SDK.Models.Game]::new(); $featuredGame.Name = 'Featured fixture'; $featuredGame.Description = '<p>Featured description</p>'; $featuredGame.IsInstalled = $true
    [DashboardTestProxy]::FeaturedFixtureGames.Add($featuredGame)
    $featuredInstance = [guid]::NewGuid()
    $featured = $plugin.GetStartPageView('FeaturedGame',$featuredInstance)
    if ($featured -isnot [LandingPage.Views.FeaturedGameView]) { throw 'Featured game widget is not registered with the provider' }
    $featured.Settings.Source = [LandingPage.Settings.FeaturedGameSource]::Pinned
    $featured.Settings.PinnedGameId = $featuredGame.Id
    $featured.Settings.ShowArtwork = $false; $featured.Settings.ShowSource = $false
    [void]$layout.Children.Add($featured); $window.UpdateLayout()
    Wait-Task ($featured.OnViewShownAsync())
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($featured.FeaturedGame.Id -ne $featuredGame.Id -or @([ToolbarTestVisuals]::Text($featured) | Where-Object Text -eq 'Featured fixture').Count -ne 1) { throw 'Pinned featured game did not render' }
    $artworkControl = $featured.GetType().GetField('artwork',$flags).GetValue($featured)
    $featured.Settings.ArtworkStretch = [Windows.Media.Stretch]::Uniform
    if ($artworkControl.Stretch -ne [Windows.Media.Stretch]::Uniform) { throw 'Fit entire artwork mode did not update live' }
    $featured.Settings.ArtworkStretch = [Windows.Media.Stretch]::UniformToFill
    if ($artworkControl.Stretch -ne [Windows.Media.Stretch]::UniformToFill) { throw 'Fill artwork mode did not update live' }
    foreach ($sizing in @(
        @([LandingPage.Settings.FeaturedArtworkSizing]::Fill,[Windows.Media.Stretch]::UniformToFill,[Windows.Controls.StretchDirection]::Both),
        @([LandingPage.Settings.FeaturedArtworkSizing]::Fit,[Windows.Media.Stretch]::Uniform,[Windows.Controls.StretchDirection]::Both),
        @([LandingPage.Settings.FeaturedArtworkSizing]::FitWithoutEnlarging,[Windows.Media.Stretch]::Uniform,[Windows.Controls.StretchDirection]::DownOnly),
        @([LandingPage.Settings.FeaturedArtworkSizing]::Stretch,[Windows.Media.Stretch]::Fill,[Windows.Controls.StretchDirection]::Both),
        @([LandingPage.Settings.FeaturedArtworkSizing]::OriginalSize,[Windows.Media.Stretch]::None,[Windows.Controls.StretchDirection]::Both)
    )) {
        $featured.Settings.ArtworkSizing = $sizing[0]
        if ($artworkControl.Stretch -ne $sizing[1] -or $artworkControl.StretchDirection -ne $sizing[2]) { throw ('Artwork sizing did not update live: ' + $sizing[0]) }
    }
    $featured.Settings.ArtworkSizing = [LandingPage.Settings.FeaturedArtworkSizing]::Fill
    $featured.Settings.UseCustomTextColor = $true; $featured.Settings.TitleColor = '#12AB34'; $featured.Settings.TitleSize = 40
    $title = [ToolbarTestVisuals]::Text($featured) | Where-Object Text -eq 'Featured fixture' | Select-Object -First 1
    if ($title.FontSize -ne 40 -or $title.Foreground.Color.ToString() -ne '#FF12AB34') { throw 'Featured text customization did not update live' }
    $featured.Settings.UseCustomTextColor = $false; $featured.Settings.TextSize = 23
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $metadataText = [ToolbarTestVisuals]::Text($featured) | Where-Object { $_.Text.StartsWith('Playtime:') } | Select-Object -First 1
    if (!$metadataText -or $metadataText.FontSize -ne 23 -or $metadataText.Foreground.Color -ne [Windows.Media.Colors]::Lime) { throw 'Metadata size must match text independently of custom colors while respecting the theme' }
    $metadataView = [ToolbarTestVisuals]::GameInfo($featured)
    if ($metadataView.Settings.MetadataIconSize -ne 23) { throw 'Metadata icon size did not follow metadata text size' }
    $featured.Settings.MatchMetadataTextSize = $false; $featured.Settings.MetadataTextSize = 18
    $featured.Settings.MatchMetadataTextColor = $false; $featured.Settings.UseCustomMetadataColor = $true; $featured.Settings.MetadataTextColor = '#34CD56'
    $featured.Settings.MatchMetadataIconSize = $false; $featured.Settings.MetadataIconSize = 27
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $metadataText = [ToolbarTestVisuals]::Text($featured) | Where-Object { $_.Text.StartsWith('Playtime:') } | Select-Object -First 1
    $metadataView = [ToolbarTestVisuals]::GameInfo($featured)
    if ($metadataText.FontSize -ne 18 -or $metadataText.Foreground.Color.ToString() -ne '#FF34CD56' -or $metadataView.Settings.MetadataIconSize -ne 27) { throw 'Independent metadata size/color/icon settings failed' }
    $featured.Settings.MatchMetadataTextSize = $true; $featured.Settings.MatchMetadataTextColor = $true; $featured.Settings.MatchMetadataIconSize = $true
    $featured.Settings.UseCustomTextColor = $true; $featured.Settings.TextSize = 26; $featured.Settings.TextColor = '#3456CD'
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $metadataText = [ToolbarTestVisuals]::Text($featured) | Where-Object { $_.Text.StartsWith('Playtime:') } | Select-Object -First 1
    if ($metadataText.FontSize -ne 26 -or $metadataText.Foreground.Color.ToString() -ne '#FF3456CD') { throw 'Metadata did not follow the description text color/size' }
    $featuredGame.Version = '2.4.1'; $featuredGame.UserScore = 0; $featuredGame.CriticScore = 88
    $featuredGame.ReleaseDate = [Playnite.SDK.Models.ReleaseDate]::new(2024,5,4)
    $featuredGame.Added = [datetime]::Now.AddDays(-3)
    $featuredGame.Links = [Collections.ObjectModel.ObservableCollection[Playnite.SDK.Models.Link]]::new()
    $featuredGame.Links.Add([Playnite.SDK.Models.Link]::new('Official website','https://example.com/game'))
    $featuredGame.Links.Add([Playnite.SDK.Models.Link]::new('Invalid launch target','file:///C:/example.exe'))
    $featured.Settings.ShowTitle = $false
    $featured.Settings.ShowGameDetails = $true; $featured.Settings.ShowLibraryDetails = $true
    $featured.Settings.ShowScores = $true; $featured.Settings.ShowLinks = $true
    $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $sectionTexts = @([ToolbarTestVisuals]::Text($featured) | ForEach-Object Text)
    foreach ($expected in @('Game details','Library information','Scores','Links','Version: 2.4.1','User score: 0','Critic score: 88')) {
        if ($sectionTexts -notcontains $expected) { throw ('Featured section or field missing: ' + $expected) }
    }
    if ($sectionTexts -contains 'Featured fixture' -or @($sectionTexts | Where-Object { $_ -eq 'Genres: ' -or $_ -eq 'Community score: ' }).Count) { throw 'Disabled title or empty field remained visible' }
    $links = @([ToolbarTestVisuals]::Buttons($featured) | ForEach-Object Content)
    if ($links -notcontains 'Official website' -or $links -contains 'Invalid launch target') { throw 'Featured links were not filtered/rendered correctly' }
    $featured.Settings.ShowSectionHeadings = $false; $featured.Settings.ShowVersion = $false; $featured.Settings.ShowCriticScore = $false
    $sectionTexts = @([ToolbarTestVisuals]::Text($featured) | ForEach-Object Text)
    if ($sectionTexts -contains 'Scores' -or $sectionTexts -contains 'Version: 2.4.1' -or $sectionTexts -contains 'Critic score: 88' -or $sectionTexts -notcontains 'User score: 0') { throw 'Section heading or individual field toggles failed' }
    $scoreText = [ToolbarTestVisuals]::Text($featured) | Where-Object Text -eq 'User score: 0' | Select-Object -First 1
    if ($scoreText.FontSize -ne 26 -or $scoreText.Foreground.Color.ToString() -ne '#FF3456CD') { throw 'Detail sections did not follow metadata formatting' }
    $featured.Settings.ShowScores = $false; $featured.Settings.ShowLibraryDetails = $false; $featured.Settings.ShowGameDetails = $false; $featured.Settings.ShowLinks = $false
    $featured.Settings.ShowTitle = $true
    $featuredGame.Description = '<p>Before image</p><img src="' + ([uri]$imagePath).AbsoluteUri + '"><p>' + ('Long description ' * 500) + '</p>'
    $featured.Settings.DescriptionLength = 0; $featured.Settings.ShowDescriptionImages = $true
    $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $descriptionPanel = $featured.GetType().GetField('sectionPanels',$flags).GetValue($featured)[[LandingPage.Settings.FeaturedGameSection]::Description]
    if (@($descriptionPanel.Children | Where-Object { $_ -is [Windows.Controls.Image] -and $_.Source }).Count -ne 1) { throw 'Local description image did not render' }
    $descriptionViewport = $featured.GetType().GetField('detailsViewport',$flags).GetValue($featured)
    if ($descriptionViewport.ScrollableHeight -le 0) { throw 'Unlimited description cannot scroll inside the widget' }
    $descriptionImage = $descriptionPanel.Children | Where-Object { $_ -is [Windows.Controls.Image] } | Select-Object -First 1
    $naturalWidth = $descriptionImage.Source.Width
    if ($descriptionImage.MaxWidth -ne $naturalWidth -or $descriptionImage.ActualWidth -gt $naturalWidth+0.1) { throw 'Description image was enlarged beyond its natural size' }
    $oldFeaturedWidth = $featured.Width
    $featured.Width = 900; $window.UpdateLayout(); $wideImageWidth = $descriptionImage.ActualWidth
    $featured.Width = 800; $window.UpdateLayout()
    if ([Math]::Abs($descriptionImage.ActualWidth-$wideImageWidth) -gt 0.1) { throw 'Small description image resized unnecessarily with its widget' }
    $featured.Width = 180; $window.UpdateLayout()
    if ($descriptionImage.ActualWidth -gt 180) { throw 'Large description image did not shrink to fit its widget' }
    $featured.Width = $oldFeaturedWidth
    $featured.SetCurrentValue([LandingPage.Views.FeaturedGameView]::DashboardEditModeProperty,$true)
    $featured.Settings.AutoScrollContent = $true; $window.UpdateLayout()
    if ([LandingPage.Controls.ContentAutoScroll]::GetPaused($descriptionViewport)) { throw 'General dashboard Edit Mode prevented auto-scroll preview' }
    $featured.Settings.AutoScrollContent = $false
    $featured.Settings.ShowDescriptionImages = $false; $window.UpdateLayout()
    $descriptionPanel = $featured.GetType().GetField('sectionPanels',$flags).GetValue($featured)[[LandingPage.Settings.FeaturedGameSection]::Description]
    if (@($descriptionPanel.Children | Where-Object { $_ -is [Windows.Controls.Image] }).Count) { throw 'Description image toggle did not remove images' }
    $featuredGame.Description = '<p>Featured description</p>'; $featured.Settings.DescriptionLength = 400
    $featured.SetCurrentValue([LandingPage.Views.FeaturedGameView]::DashboardEditModeProperty,$true)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $sharedSectionStyle = $featured.Settings.SectionAppearance
    $sharedSectionStyle.Background = '#112233'; $sharedSectionStyle.BackgroundOpacity = 0.4; $sharedSectionStyle.BorderWidth = 2; $sharedSectionStyle.Padding = 11; $sharedSectionStyle.CornerRadius = 9; $sharedSectionStyle.Enabled = $true
    $window.UpdateLayout()
    $sectionPanels = $featured.GetType().GetField('sectionPanels',$flags).GetValue($featured)
    $styledTitle = $sectionPanels[[LandingPage.Settings.FeaturedGameSection]::Title].Parent
    $styledDescription = $sectionPanels[[LandingPage.Settings.FeaturedGameSection]::Description].Parent
    if ($styledTitle.Background.Color.ToString() -ne '#FF112233' -or $styledTitle.Background.Opacity -ne 0.4 -or $styledTitle.BorderThickness.Left -ne 2 -or $styledTitle.Padding.Left -ne 11 -or $styledTitle.CornerRadius.TopLeft -ne 9 -or $styledDescription.Background.Color.ToString() -ne '#FF112233') { throw ('Shared section style not applied: title=' + $styledTitle.GetType().Name + ' bg=' + $styledTitle.Background + ' opacity=' + $styledTitle.Background.Opacity + ' border=' + $styledTitle.BorderThickness + ' padding=' + $styledTitle.Padding + ' corners=' + $styledTitle.CornerRadius + ' desc=' + $styledDescription.Background) }
    $individualTitle = $featured.Settings.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Title)
    $individualTitle.Appearance.Background = '#445566'; $individualTitle.Appearance.Padding = 5; $individualTitle.Appearance.Enabled = $true; $individualTitle.OverrideAppearance = $true
    $sharedSectionStyle.Background = '#ABCDEF'; $window.UpdateLayout()
    if ($styledTitle.Background.Color.ToString() -ne '#FF445566' -or $styledTitle.Padding.Left -ne 5 -or $styledDescription.Background.Color.ToString() -ne '#FFABCDEF') { throw 'Individual appearance did not override shared settings independently' }
    $individualTitle.Appearance.TextColor = '#00AAFF'; $individualTitle.Appearance.CustomizeText = $true
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $appearanceTitleText = [ToolbarTestVisuals]::Text($featured) | Where-Object Text -eq 'Featured fixture' | Select-Object -First 1
    if ($appearanceTitleText.Foreground.Color.ToString() -ne '#FF00AAFF') { throw 'Individual section text color was not applied' }
    $individualTitle.OverrideAppearance = $false; $window.UpdateLayout()
    if ($styledTitle.Background.Color.ToString() -ne '#FFABCDEF' -or $styledTitle.Padding.Left -ne 11) { throw 'Disabling override did not restore shared section appearance' }
    $sharedSectionStyle.Enabled = $false; $window.UpdateLayout()
    if ($styledTitle.BorderThickness.Left -ne 0 -or $styledTitle.Padding.Left -ne 0) { throw 'Disabling shared section styling did not restore default geometry' }
    $featured.Settings.UseCustomSectionLayout = $true; $featured.Settings.EditSectionLayout = $true
    $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $sectionCanvas = $featured.GetType().GetField('sectionCanvas',$flags).GetValue($featured)
    $titleFrame = $sectionCanvas.Children | Where-Object { $_.GetType().GetField('Layout',$flags).GetValue($_).Section -eq [LandingPage.Settings.FeaturedGameSection]::Title } | Select-Object -First 1
    if (!$titleFrame) { throw 'Custom layout did not create the title section frame' }
    $titleLayout = $featured.Settings.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Title)
    $sharedSectionStyle.Enabled = $true; $window.UpdateLayout()
    if ($titleFrame.Background.Color.ToString() -ne '#FFABCDEF' -or $titleFrame.BorderThickness.Left -ne 2 -or $titleFrame.Padding.Left -ne 11) { throw 'Shared appearance did not apply to custom-layout section frames' }
    $sharedSectionStyle.Enabled = $false; $window.UpdateLayout()
    $oldX = $titleLayout.X; $oldY = $titleLayout.Y; $oldWidth = $titleLayout.Width; $oldHeight = $titleLayout.Height
    $moveHandle = $titleFrame.GetType().GetField('MoveHandle',$flags).GetValue($titleFrame)
    $resizeHandle = $titleFrame.GetType().GetField('ResizeHandle',$flags).GetValue($titleFrame)
    $titleInside = [ToolbarTestVisuals]::Text($featured) | Where-Object Text -eq 'Featured fixture' | Select-Object -First 1
    $textPoint = $titleInside.TranslatePoint([Windows.Point]::new(3,3),$titleFrame)
    $textHit = $titleFrame.InputHitTest($textPoint)
    while ($textHit -and $textHit -isnot [Windows.Controls.Primitives.Thumb]) { $textHit = [Windows.Media.VisualTreeHelper]::GetParent($textHit) }
    if (![object]::ReferenceEquals($textHit,$moveHandle)) { throw 'Text inside an editable section does not hit the move handle' }
    $resizeHit = $titleFrame.InputHitTest([Windows.Point]::new($titleFrame.ActualWidth-6,$titleFrame.ActualHeight-6))
    while ($resizeHit -and $resizeHit -isnot [Windows.Controls.Primitives.Thumb]) { $resizeHit = [Windows.Media.VisualTreeHelper]::GetParent($resizeHit) }
    if (![object]::ReferenceEquals($resizeHit,$resizeHandle)) { throw 'Body drag surface blocks the resize grip' }
    $drag = [Windows.Controls.Primitives.DragDeltaEventArgs]::new(40,30); $drag.RoutedEvent = [Windows.Controls.Primitives.Thumb]::DragDeltaEvent
    $moveHandle.RaiseEvent($drag)
    $resize = [Windows.Controls.Primitives.DragDeltaEventArgs]::new(50,20); $resize.RoutedEvent = [Windows.Controls.Primitives.Thumb]::DragDeltaEvent
    $resizeHandle.RaiseEvent($resize); $window.UpdateLayout()
    if ($titleLayout.X -ne $oldX+40 -or $titleLayout.Y -ne $oldY+30 -or $titleLayout.Width -ne $oldWidth+50 -or $titleLayout.Height -ne $oldHeight+20 -or [Windows.Controls.Canvas]::GetLeft($titleFrame) -ne $titleLayout.X -or $titleFrame.ActualWidth -ne $titleLayout.Width) { throw 'Dragging/resizing did not update section settings and visible geometry' }
    $beforeLockText = [ToolbarTestVisuals]::Text($featured) | Where-Object Text -eq 'Featured fixture' | Select-Object -First 1
    $beforeLockBounds = $beforeLockText.TransformToAncestor($featured).TransformBounds([Windows.Rect]::new(0,0,$beforeLockText.ActualWidth,$beforeLockText.ActualHeight))
    $savedSectionX = $titleLayout.X; $savedSectionY = $titleLayout.Y; $savedSectionWidth = $titleLayout.Width; $savedSectionHeight = $titleLayout.Height
    $featured.Settings.EditSectionLayout = $false; $window.UpdateLayout()
    $afterLockText = [ToolbarTestVisuals]::Text($featured) | Where-Object Text -eq 'Featured fixture' | Select-Object -First 1
    $afterLockBounds = $afterLockText.TransformToAncestor($featured).TransformBounds([Windows.Rect]::new(0,0,$afterLockText.ActualWidth,$afterLockText.ActualHeight))
    if ($beforeLockBounds -ne $afterLockBounds -or $titleLayout.X -ne $savedSectionX -or $titleLayout.Y -ne $savedSectionY -or $titleLayout.Width -ne $savedSectionWidth -or $titleLayout.Height -ne $savedSectionHeight) { throw 'Leaving section editing moved/resized its content or changed saved geometry' }
    $titleFrame = $sectionCanvas.Children | Where-Object { $_.GetType().GetField('Layout',$flags).GetValue($_).Section -eq [LandingPage.Settings.FeaturedGameSection]::Title } | Select-Object -First 1
    $beforeLocked = $titleLayout.X
    $titleFrame.GetType().GetField('MoveHandle',$flags).GetValue($titleFrame).RaiseEvent($drag)
    if ($titleLayout.X -ne $beforeLocked) { throw 'Read-only sections allowed dragging' }
    $titleLayout.X = 120; $titleLayout.Width = 300; $window.UpdateLayout()
    if ([Windows.Controls.Canvas]::GetLeft($titleFrame) -ne 120 -or $titleFrame.ActualWidth -ne 300) { throw 'Numeric layout edits did not update the section live' }
    $featured.Settings.PinnedGameId = [guid]::NewGuid()
    Wait-Task ($featured.OnViewShownAsync())
    $window.UpdateLayout()
    if (@([ToolbarTestVisuals]::Text($featured) | Where-Object { $_.Text.StartsWith('Choose a pinned game') }).Count -ne 1) { throw 'Missing game message disappeared in custom section layout' }
    $featured.Settings.PinnedGameId = $featuredGame.Id
    Wait-Task ($featured.OnViewShownAsync())
    $featured.Settings.ResetSectionLayout(); $window.UpdateLayout()
    if ($featured.Settings.UseCustomSectionLayout -or $featured.Settings.EditSectionLayout -or @([ToolbarTestVisuals]::Text($featured) | Where-Object Text -eq 'Featured fixture').Count -ne 1) { throw 'Reset did not restore automatic section layout' }
    $loadedFeaturedGame = $featured.FeaturedGame
    Wait-Task ($featured.OnViewHiddenAsync()); Wait-Task ($featured.OnViewShownAsync())
    if (![object]::ReferenceEquals($loadedFeaturedGame,$featured.FeaturedGame)) { throw 'Unchanged featured widget lost its cached selection' }
    $editor = [LandingPage.Views.Settings.FeaturedGameSettingsView]::new(); $editor.DataContext = $featured.Settings
    [void]$layout.Children.Add($editor); $window.UpdateLayout()
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if (@([ToolbarTestVisuals]::Text($editor) | Where-Object Text -eq 'Featured game source').Count -ne 1) { throw 'Featured game settings editor did not render' }
    [void]$layout.Children.Remove($editor)
    [void]$view.Content.Children.Add($editor)
    $sectionNode = [LandingPage.Models.Layout.GridNode]::new()
    $sectionNode.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new()
    $sectionNode.ViewProperties.ViewId = 'FeaturedGame'; $sectionNode.ViewProperties.FeaturedGameSettings = $featured.Settings
    $sectionModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($sectionNode)
    $sectionModel.View = [Windows.Controls.Grid]::new()
    $featured.Settings.UseCustomSectionLayout = $true
    $sectionModel.EditModeEnabled = $true; $featured.Settings.EditSectionLayout = $true
    $sectionModel.EditModeEnabled = $false
    if ($featured.Settings.EditSectionLayout) { throw 'Exiting dashboard Edit Mode left section editing enabled' }
    $view.GetType().GetMethod('ShowAppearance',$flags).Invoke($view,@($sectionModel))
    $editor.GetType().GetMethod('BeginSectionEditing_Click',$flags).Invoke($editor,@($null,[Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)))
    if (!$featured.Settings.EditSectionLayout -or $view.FindName('AppearanceOverlay').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Explicit section edit action did not start editing and close Appearance' }
    $sectionModel.EditModeEnabled = $true
    $sectionModel.IsLeaf = $true; $sectionModel.HasView = $true
    $sectionNodeView = [LandingPage.Views.GridNodeView]::new(); $sectionNodeView.DataContext = $sectionModel
    [void]$view.Content.Children.Add($sectionNodeView); $window.UpdateLayout()
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $doneSections = $sectionNodeView.FindName('FeaturedSectionsButton')
    if ($doneSections.Visibility -ne [Windows.Visibility]::Visible -or $doneSections.Content -ne 'Done sections' -or !$doneSections.IsHitTestVisible) { throw 'Single right section button is unavailable while editing sections' }
    $buttonPoint = $doneSections.TranslatePoint([Windows.Point]::new($doneSections.ActualWidth/2,$doneSections.ActualHeight/2),$sectionNodeView)
    $buttonHit = $sectionNodeView.InputHitTest($buttonPoint)
    while ($buttonHit -and $buttonHit -isnot [Windows.Controls.Primitives.ToggleButton]) { $buttonHit = [Windows.Media.VisualTreeHelper]::GetParent($buttonHit) }
    if (![object]::ReferenceEquals($buttonHit,$doneSections)) { throw ('Dashboard edit overlay blocks the right section button: button=' + $doneSections.ActualWidth + 'x' + $doneSections.ActualHeight + ' node=' + $sectionNodeView.ActualWidth + 'x' + $sectionNodeView.ActualHeight + ' point=' + $buttonPoint + ' hit=' + $buttonHit) }
    $doneSections.SetCurrentValue([Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty,$false)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($featured.Settings.EditSectionLayout -or $doneSections.Content -ne 'Edit sections' -or [Windows.Controls.Panel]::GetZIndex($doneSections) -lt 1) { throw 'Done sections did not lock the layout or stay above section content' }
    $buttonHit = $sectionNodeView.InputHitTest($buttonPoint)
    while ($buttonHit -and $buttonHit -isnot [Windows.Controls.Primitives.ToggleButton]) { $buttonHit = [Windows.Media.VisualTreeHelper]::GetParent($buttonHit) }
    if (![object]::ReferenceEquals($buttonHit,$doneSections)) { throw 'Dashboard editing overlay blocks the section entry button before editing starts' }
    $doneSections.SetCurrentValue([Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty,$true)
    $doneSections.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent))
    $window.UpdateLayout()
    if (!$featured.Settings.EditSectionLayout -or $doneSections.Content -ne 'Done sections') { throw 'Right button failed to enter section editing' }
    $vm.RootNodeViewModel.EditModeEnabled = $true
    $plugin.GetType().GetMethod('OnApplicattionDeactivated',$flags).Invoke($plugin,@($null,[EventArgs]::Empty))
    if (!$vm.RootNodeViewModel.EditModeEnabled -or !$featured.Settings.EditSectionLayout) { throw 'Alt+Tab ended dashboard or section editing' }
    $escapeSections = [Windows.Input.KeyEventArgs]::new([Windows.Input.Keyboard]::PrimaryDevice,[Windows.PresentationSource]::FromVisual($featured),0,[Windows.Input.Key]::Escape)
    $escapeSections.RoutedEvent = [Windows.Input.Keyboard]::PreviewKeyDownEvent
    $featured.RaiseEvent($escapeSections)
    if ($featured.Settings.EditSectionLayout -or !$escapeSections.Handled) { throw 'Escape did not lock custom section editing' }
    $featured.Settings.EditSectionLayout = $true
    $featured.SetCurrentValue([LandingPage.Views.FeaturedGameView]::DashboardEditModeProperty,$false)
    $sectionModel.EditModeEnabled = $false; $window.UpdateLayout()
    if ($featured.Settings.EditSectionLayout -or $doneSections.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Leaving dashboard Edit Mode kept the button visible or section editing active' }
    $vm.RootNodeViewModel.EditModeEnabled = $false
    $featured.Settings.ResetSectionLayout()
    [void]$view.Content.Children.Remove($sectionNodeView)
    [void]$view.Content.Children.Remove($editor)
    $hoverPreviewGame = [Playnite.SDK.Models.Game]::new(); $hoverPreviewGame.Name = 'Hovered preview'; $hoverPreviewGame.IsInstalled = $true
    [DashboardTestProxy]::FeaturedFixtureGames.Add($hoverPreviewGame)
    $selectionArgs = [Playnite.SDK.Events.OnGameSelectedEventArgs]::new([Collections.Generic.List[Playnite.SDK.Models.Game]]::new(),[Collections.Generic.List[Playnite.SDK.Models.Game]]::new()); $selectionArgs.NewValue.Add($featuredGame)
    $plugin.OnGameSelected($selectionArgs)
    $setHover = $plugin.GetType().GetMethod('SetFeaturedHoveredGame',$flags)
    $hoverOwnerA = [object]::new(); $hoverOwnerB = [object]::new(); $hoverGroup = [object]::new()
    $queriesBeforeHover = [DashboardTestProxy]::LibraryEnumerations
    $featured.Settings.HoverDelayMilliseconds = 0; $featured.Settings.Source = [LandingPage.Settings.FeaturedGameSource]::HoveredGame
    [void]$setHover.Invoke($plugin,@($hoverPreviewGame,$hoverOwnerB,$hoverGroup)); Wait-Task ($featured.OnViewShownAsync())
    if ($featured.FeaturedGame.Id -ne $hoverPreviewGame.Id) { throw 'Hovered game did not reach the Featured Game widget' }
    [void]$setHover.Invoke($plugin,@($null,$hoverOwnerA,$hoverGroup))
    if ($plugin.GetType().GetProperty('FeaturedHoveredGame',$flags).GetValue($plugin).Id -ne $hoverPreviewGame.Id) { throw 'A stale hover leave cleared a newer hover owner' }
    [void]$setHover.Invoke($plugin,@($null,$hoverOwnerB,$hoverGroup)); Wait-Task ($featured.OnViewShownAsync())
    if ($featured.FeaturedGame.Id -ne $hoverPreviewGame.Id) { throw 'Hover-only mode did not keep the last game' }
    $featured.Settings.KeepLastHoveredGame = $false; Wait-Task ($featured.OnViewShownAsync())
    if ($featured.FeaturedGame) { throw 'Hover-only empty state retained a game despite disabling keep-last' }
    $featured.Settings.Source = [LandingPage.Settings.FeaturedGameSource]::HoveredOrSelectedGame; Wait-Task ($featured.OnViewShownAsync())
    if ($featured.FeaturedGame.Id -ne $featuredGame.Id) { throw 'Combined source did not fall back to the selected game' }
    $featured.Settings.HoverDelayMilliseconds = 60
    [void]$setHover.Invoke($plugin,@($hoverPreviewGame,$hoverOwnerB,$hoverGroup))
    $previewTimer = $featured.GetType().GetField('hoverTimer',$flags).GetValue($featured)
    if (!$previewTimer.IsEnabled -or $featured.FeaturedGame.Id -ne $featuredGame.Id) { throw 'Hover delay did not defer a preview update' }
    $hoverDeadline = [datetime]::UtcNow.AddSeconds(3)
    while ($featured.FeaturedGame.Id -ne $hoverPreviewGame.Id -and [datetime]::UtcNow -lt $hoverDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 5 }
    if ($featured.FeaturedGame.Id -ne $hoverPreviewGame.Id) { throw 'Delayed hover preview never updated' }
    Wait-Task ($featured.OnViewHiddenAsync())
    [void]$setHover.Invoke($plugin,@($null,$hoverOwnerB,$hoverGroup))
    if ($previewTimer.IsEnabled) { throw 'Hidden widget kept its hover timer running' }
    Wait-Task ($featured.OnViewShownAsync())
    if ($featured.FeaturedGame.Id -ne $featuredGame.Id -or [DashboardTestProxy]::LibraryEnumerations -ne $queriesBeforeHover) { throw 'Hover/selection fallback queried the full library or lost its selection on returning' }
    $featured.Settings.Source = [LandingPage.Settings.FeaturedGameSource]::Pinned
    Wait-Task ($featured.OnViewShownAsync())
    $plugin.OnViewRemoved('FeaturedGame',$featuredInstance)
    if (!$featured.GetType().GetField('closed',$flags).GetValue($featured) -or $featured.GetType().GetField('subscribed',$flags).GetValue($featured)) { throw 'Removed featured widget retained subscriptions' }
    [void]$layout.Children.Remove($featured)
    [DashboardTestProxy]::FeaturedFixtureGames.Clear()
    'PASS: featured game registration, pinned rendering, live theme/color/size controls, cached revisits, settings editor and removal cleanup.'
    $hoverSettings = [LandingPage.Models.ShelveProperties]::new()
    $hoverSettings.HoverEffect = [LandingPage.Models.ShelfHoverEffect]::Dock; $hoverSettings.HoverScale = 1.4; $hoverSettings.HoverRadius = 2
    $hoverSettings.HoverGlow = $true; $hoverSettings.HoverDimOthers = $true
    $hoverList = [Windows.Controls.ListBox]::new(); $hoverList.Width = 500; $hoverList.Height = 230; $hoverList.Padding = [Windows.Thickness]::new(3)
    $hoverList.ItemsPanel = [Windows.Markup.XamlReader]::Parse('<ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><StackPanel Orientation="Horizontal"/></ItemsPanelTemplate>')
    $hoverList.ItemTemplate = [Windows.Markup.XamlReader]::Parse('<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><Border x:Name="HoverArtwork" Width="100" Height="120" Background="Gray"><TextBlock Text="{Binding Game.Name}"/></Border></DataTemplate>')
    $hoverStyle = [Windows.Style]::new([Windows.Controls.ListBoxItem])
    $hoverStyle.Setters.Add([Windows.Setter]::new([LandingPage.Controls.ShelfHover]::SettingsProperty,$hoverSettings))
    $hoverStyle.Setters.Add([Windows.Setter]::new([Windows.UIElement]::OpacityProperty,0.8))
    $hoverStyle.Setters.Add([Windows.Setter]::new([Windows.UIElement]::RenderTransformProperty,[Windows.Media.TranslateTransform]::new(2,3)))
    $hoverList.ItemContainerStyle = $hoverStyle
    $hoverThirdGame = [Playnite.SDK.Models.Game]::new(); $hoverThirdGame.Name = 'Third hover card'
    foreach ($hoverGame in @($gameA,$gameB,$hoverThirdGame)) { [void]$hoverList.Items.Add([LandingPage.Models.GameModel]::new($hoverGame)) }
    [void]$scene.Children.Add($hoverList); $window.UpdateLayout()
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $middleCard = $hoverList.ItemContainerGenerator.ContainerFromIndex(1)
    $leftCard = $hoverList.ItemContainerGenerator.ContainerFromIndex(0)
    $controllerProperty = [LandingPage.Controls.ShelfHover].GetField('ControllerProperty',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
    $hoverController = $middleCard.GetValue($controllerProperty)
    if (!$hoverController -or $middleCard.RenderTransform -isnot [Windows.Media.TransformGroup]) { throw 'Shelf hover did not attach to realized game containers' }
    Set-Field $hoverController 'AnimationsEnabled' ([Func[bool]]{ return $false })
    $centerPoint = $hoverController.GetType().GetMethod('Center',$flags).Invoke($hoverController,@($middleCard))
    $applyHover = $hoverController.GetType().GetMethod('ApplyAt',$flags)
    $beforeHoverQueries = [DashboardTestProxy]::LibraryEnumerations
    [void]$applyHover.Invoke($hoverController,@($centerPoint,$middleCard))
    $states = $hoverController.GetType().GetField('items',$flags).GetValue($hoverController)
    $middleState = $states[$middleCard]; $leftState = $states[$leftCard]
    if (!$middleState.Floating -or [Math]::Abs($middleState.Scale.ScaleX - 1.4) -gt 0.001 -or $leftState.Scale.ScaleX -le 1 -or $leftState.Scale.ScaleX -ge 1.4) { throw 'Dock overlay did not magnify focus and neighbors' }
    if ($middleCard.Opacity -ne 0 -or $middleState.Floating.Effect -isnot [Windows.Media.Effects.DropShadowEffect]) { throw 'Overlay did not replace the original drawing/glow' }
    $artworkVisual = [ToolbarTestVisuals]::Named($middleCard,'HoverArtwork')
    if ($artworkVisual.ActualWidth -ne 100 -or $artworkVisual.ActualHeight -ne 120 -or !$middleCard.LayoutTransform.Value.IsIdentity) { throw 'Hover changed normal card dimensions' }
    $window.UpdateLayout()
    $viewport = [Windows.Media.VisualTreeHelper]::GetParent($middleCard)
    while ($viewport -and $viewport -isnot [Windows.Controls.ScrollContentPresenter]) { $viewport = [Windows.Media.VisualTreeHelper]::GetParent($viewport) }
    $layer = $view.FindName('ShelfHoverLayer')
    $viewportBounds = $viewport.TransformToAncestor($scene).TransformBounds([Windows.Rect]::new(0,0,$viewport.ActualWidth,$viewport.ActualHeight))
    $floatingBounds = $middleState.Floating.TransformToAncestor($scene).TransformBounds([Windows.Rect]::new(0,0,$middleState.Floating.ActualWidth,$middleState.Floating.ActualHeight))
    if ($floatingBounds.Top -ge $viewportBounds.Top) { throw 'Magnified artwork did not extend above the shelf viewport' }
    if ([Windows.Media.VisualTreeHelper]::GetParent($middleState.Floating) -ne $layer -or $layer.IsHitTestVisible) { throw 'Artwork is not in the dashboard overlay or intercepts input' }
    $floating = $middleState.Floating
    $pixelWidth = [int]$scene.ActualWidth; $pixelHeight = [int]$scene.ActualHeight
    $pixels = [Windows.Media.Imaging.RenderTargetBitmap]::new($pixelWidth,$pixelHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    # A narrow presenter can still contain full-width fixed-size artwork.
    # The overlay must reproduce drawing bounds at 1:1 before magnification.
    $sourcePresenter = [Windows.Media.VisualTreeHelper]::GetParent($middleState.Source)
    $sourcePresenter.Width = 24
    $window.UpdateLayout()
    $hoverController.GetType().GetMethod('PositionFloating',$flags).Invoke($hoverController,@())
    $drawingBounds = [Windows.Media.VisualTreeHelper]::GetDescendantBounds($sourcePresenter)
    if ($middleState.Floating.Width -lt 100 -or $middleState.Floating.Height -lt 120 -or $middleState.Floating.Fill.Stretch -ne [Windows.Media.Stretch]::None -or $middleState.Floating.Fill.Viewbox.Width -ne $middleState.Floating.Width) { throw ('Constrained presenter squeezed the artwork overlay: ' + $middleState.Floating.Width + 'x' + $middleState.Floating.Height + ' bounds=' + $middleState.Floating.Fill.Viewbox) }
    $sourcePresenter.ClearValue([Windows.FrameworkElement]::WidthProperty)
    $window.UpdateLayout()
    $hoverController.GetType().GetMethod('PositionFloating',$flags).Invoke($hoverController,@())
    $stableWidth = $middleState.Floating.Width; $stableHeight = $middleState.Floating.Height
    $stableLeft = [Windows.Controls.Canvas]::GetLeft($middleState.Floating)
    $stableTop = [Windows.Controls.Canvas]::GetTop($middleState.Floating)
    $hoverText = [Windows.Media.VisualTreeHelper]::GetChild($middleState.Source,0)
    $hoverText.RenderTransform = [Windows.Media.TranslateTransform]::new(-200,-100)
    $window.UpdateLayout()
    for ($pointerPass = 0; $pointerPass -lt 12; ++$pointerPass) {
        $hoverController.GetType().GetMethod('PositionFloating',$flags).Invoke($hoverController,@())
        [void]$applyHover.Invoke($hoverController,@($centerPoint,$middleCard))
    }
    if ($middleState.Floating.Width -ne $stableWidth -or $middleState.Floating.Height -ne $stableHeight -or [Windows.Controls.Canvas]::GetLeft($middleState.Floating) -ne $stableLeft -or [Windows.Controls.Canvas]::GetTop($middleState.Floating) -ne $stableTop) { throw 'Changing hover decorations moved or resized the overlay viewport' }
    $hoverText.ClearValue([Windows.UIElement]::RenderTransformProperty)
    $pixels.Render($view.FindName('ShelfHoverLayer'))
    $buffer = [byte[]]::new($pixelWidth*$pixelHeight*4); $pixels.CopyPixels($buffer,$pixelWidth*4,0)
    if (($buffer | Measure-Object -Maximum).Maximum -eq 0) { throw 'Floating artwork is invisible when original item is hidden' }
    [void]$applyHover.Invoke($hoverController,@($centerPoint,$null))
    if ($middleState.Floating -or [Math]::Abs($middleCard.Opacity - 0.8) -gt 0.001) { throw 'Leaving the shelf did not remove the overlay and restore opacity' }
    $hoverSettings.HoverDirection = [LandingPage.Models.ShelfHoverDirection]::Down
    [void]$applyHover.Invoke($hoverController,@($centerPoint,$middleCard))
    if ($middleState.Floating.RenderTransformOrigin.Y -ne 0 -or $middleState.Move.Y -ne $hoverSettings.HoverLift) { throw 'Downward animation did not use the top anchor and positive lift' }
    $hoverSettings.HoverDirection = [LandingPage.Models.ShelfHoverDirection]::Up
    [void]$applyHover.Invoke($hoverController,@($centerPoint,$middleCard))
    if ($middleState.Floating.RenderTransformOrigin.Y -ne 1 -or $middleState.Move.Y -ne -$hoverSettings.HoverLift) { throw 'Upward animation did not restore the bottom anchor and negative lift' }
    $hoverSettings.HoverEffect = [LandingPage.Models.ShelfHoverEffect]::Tilt
    $rightPoint = [Windows.Point]::new($centerPoint.X + 40,$centerPoint.Y)
    [void]$applyHover.Invoke($hoverController,@($rightPoint,$middleCard))
    if ($middleState.Rotate.Angle -le 0) { throw 'Tilt did not follow pointer movement' }
    Set-Field $hoverController 'AnimationsEnabled' ([Func[bool]]{ return $true })
    $hoverSettings.HoverDuration = 50
    foreach ($animatedMode in @('Zoom','Lift','Bounce')) {
        $hoverSettings.HoverEffect = [Enum]::Parse([LandingPage.Models.ShelfHoverEffect],$animatedMode)
        [void]$applyHover.Invoke($hoverController,@($centerPoint,$null))
        [void]$applyHover.Invoke($hoverController,@($centerPoint,$middleCard))
        $animationDeadline = [datetime]::UtcNow.AddMilliseconds(160)
        while ([datetime]::UtcNow -lt $animationDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 5 }
        if ([Math]::Abs($middleState.Scale.ScaleX - 1.4) -gt 0.01) { throw ('Hover animation did not settle: ' + $animatedMode) }
    }
    Set-Setting $settingsModel.Settings 'EnableDashboardDiagnostics' $true
    [void]$applyHover.Invoke($hoverController,@($centerPoint,$middleCard))
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $hoverController.GetType().GetMethod('EndDiagnostics',$flags).Invoke($hoverController,@('fixture complete'))
    Wait-Task ($hoverController.GetType().GetField('diagnosticWrite',$flags).GetValue($hoverController))
    $logDeadline = [datetime]::UtcNow.AddSeconds(3)
    while (![IO.File]::ReadAllText($diagnosticFile).Contains('Shelf hover performance:') -and [datetime]::UtcNow -lt $logDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
    if (![IO.File]::ReadAllText($diagnosticFile).Contains('maxFrameGapMs=')) { throw 'Hover diagnostic summary or rendering cleanup failed' }
    Set-Setting $settingsModel.Settings 'EnableDashboardDiagnostics' $false
    if ([DashboardTestProxy]::LibraryEnumerations -ne $beforeHoverQueries) { throw 'Hovering games queried the library' }
    # Reproduce grouped shelves and repeat layout: hover must never resize normal cards.
    $hoverSettings.HoverEffect = [LandingPage.Models.ShelfHoverEffect]::Dock
    $groupStyle = [Windows.Controls.GroupStyle]::new()
    $groupStyle.Panel = [Windows.Markup.XamlReader]::Parse('<ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><StackPanel Orientation="Horizontal"/></ItemsPanelTemplate>')
    $groupContainerStyle = [Windows.Style]::new([Windows.Controls.GroupItem])
    $groupTemplate = [Windows.Markup.XamlReader]::Parse('<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="GroupItem"><GroupBox Header="{Binding Name}"><GroupBox.Template><ControlTemplate TargetType="GroupBox"><Grid><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions><Border x:Name="GroupHeader" Height="24"><ContentPresenter ContentSource="Header"/></Border><ContentPresenter Grid.Row="1"/></Grid></ControlTemplate></GroupBox.Template><ItemsPresenter/></GroupBox></ControlTemplate>')
    $groupContainerStyle.Setters.Add([Windows.Setter]::new([Windows.Controls.Control]::TemplateProperty,$groupTemplate))
    $groupStyle.ContainerStyle = $groupContainerStyle; $hoverList.GroupStyle.Add($groupStyle)
    $hoverList.Items.GroupDescriptions.Add([Windows.Data.PropertyGroupDescription]::new('Game.Name'))
    for ($layoutPass = 0; $layoutPass -lt 5; ++$layoutPass) { $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle) }
    $groupedArt = [ToolbarTestVisuals]::Named($hoverList,'HoverArtwork')
    $groupedCard = [Windows.Media.VisualTreeHelper]::GetParent($groupedArt)
    while ($groupedCard -and $groupedCard -isnot [Windows.Controls.ListBoxItem]) { $groupedCard = [Windows.Media.VisualTreeHelper]::GetParent($groupedCard) }
    if (!$groupedCard) { throw 'Grouped hover fixture did not produce a card' }
    $groupController = $groupedCard.GetValue($controllerProperty)
    $fitBefore = $groupedCard.LayoutTransform.Value.M11
    for ($layoutPass = 0; $layoutPass -lt 12; ++$layoutPass) {
        $groupController.GetType().GetMethod('PositionFloating',$flags).Invoke($groupController,@())
        $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    }
    if ($groupedArt.ActualHeight -lt 100 -or $groupedCard.LayoutTransform.Value.M11 -lt 0.5 -or [Math]::Abs($groupedCard.LayoutTransform.Value.M11 - $fitBefore) -gt 0.01) { throw 'Repeated grouped reservation shrank artwork toward zero' }
    [void]$scene.Children.Remove($hoverList); $window.UpdateLayout()
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($hoverList.Padding -ne [Windows.Thickness]::new(3) -or $middleCard.RenderTransform -isnot [Windows.Media.TranslateTransform] -or $middleCard.RenderTransform.X -ne 2) { throw 'Unloading hover effects lost original transforms/padding' }
    'PASS: visible overflow above shelf bounds, unchanged card layout, dock neighbors, glow/dimming, animated tilt/zoom/lift/bounce, grouped stability, cleanup and no hover queries.'
    $window.Close()
    'PASS: toolbar serialization, native catalog/commands/menus/toggles/search/progress/extensions, clickable page navigation, missing providers, menu-state restoration, and settings refresh.'
    'PASS: legacy migration, independent layout/backgrounds, save/reload/order, nested traversal, real page switching and widget callbacks, deletion, last-page protection, temporary-file persistence, context-menu navigation, and in-app manager open/close.'
} finally {
    if ($vm) { $vm.GetType().GetField('clock',$flags).GetValue($vm).Stop(); $navTimer = $vm.GetType().GetField('navigationSaveTimer',$flags).GetValue($vm); if ($navTimer) { $navTimer.Stop() } }
    [LandingPage.LandingPageExtension].GetProperty('Instance').SetValue($null,$originalPlugin)
    $serializerField.SetValue($null, $originalSerializer)
    $app.Shutdown()
    # All created test files remain in this uniquely named temp directory for inspection.
}
