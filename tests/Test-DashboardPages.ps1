# Runs entirely against temporary settings and an in-memory Playnite API.
# powershell.exe -STA -File tests/Test-DashboardPages.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'source\StartPage\bin\Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('LiveCharts','LiveCharts.Wpf','Gu.Wpf.NumericInput','Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) {
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
    try { [void]$task.GetAwaiter().GetResult() } catch { Write-Host $_.Exception.ToString(); Write-Host $_.ScriptStackTrace; throw }
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
    public static System.Threading.Tasks.Task WorkerEvent(object view,string method,object args)
    {
        return System.Threading.Tasks.Task.Run(() => {
            try { view.GetType().GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(view,new object[] {null,args}); }
            catch(System.Reflection.TargetInvocationException error) { throw error.InnerException; }
        });
    }

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
public class WidgetSettingsTestProvider : Plugin, StartPage.SDK.IStartPageExtension
{
    public override Guid Id { get { return Guid.Parse("cfeee9e8-1814-4212-bda0-576c195aa341"); } }
    public readonly UserControl Editor = new UserControl { Content = new TextBox { Text = "Provider setting" } };
    public int Requests;
    public WidgetSettingsTestProvider() : base(null) { }
    public StartPage.SDK.StartPageExtensionArgs GetAvailableStartPageViews() { return new StartPage.SDK.StartPageExtensionArgs(); }
    public object GetStartPageView(string viewId, Guid instanceId) { return new UserControl(); }
    public Control GetStartPageViewSettings(string viewId, Guid instanceId) { Requests++; return Editor; }
    public void OnViewRemoved(string viewId, Guid instanceId) { }
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
        var completionStatuses = new DashboardTestProxy(typeof(IItemCollection<CompletionStatus>), call => {
            if(call.MethodName == "GetEnumerator") return ((IEnumerable<CompletionStatus>)new List<CompletionStatus>()).GetEnumerator();
            if(call.MethodName == "get_Count") return 0;
            return null;
        }).GetTransparentProxy();
        var database = new DashboardTestProxy(typeof(IGameDatabaseAPI), call =>
        {
            if (call.MethodName == "get_Games") return games;
            if (call.MethodName == "get_CompletionStatuses") return completionStatuses;
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
    [LandingPage.LandingPageExtension].GetField('<Id>k__BackingField',$flags).SetValue($plugin,[guid]'a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5')
    [LandingPage.LandingPageExtension].GetProperty('Instance').SetValue($null,$plugin)
    # Startup logging occurs before SettingsViewModel has been assigned.
    [LandingPage.LandingPageExtension].GetField('logger',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null).Info('Fixture startup before settings initialization')

    $api = [DashboardTestProxy]::Create($tempRoot,$plugin)
    [Playnite.SDK.Plugins.Plugin].GetField('PlayniteApi',$flags).SetValue($plugin,$api)
    $settingsModel = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.LandingPageSettingsViewModel])
    $settingsModel.Settings = [LandingPage.LandingPageSettings]::new()
    [LandingPage.LandingPageExtension].GetProperty('SettingsViewModel',$flags).SetValue($plugin,$settingsModel)
    $initialRegistry = [Collections.Generic.Dictionary[string,Collections.Generic.List[LandingPage.Models.Layout.StartPageViewArgs]]]::new()
    $initialViews = [Collections.Generic.List[LandingPage.Models.Layout.StartPageViewArgs]]::new()
    $initialToolbar = [LandingPage.Models.Layout.StartPageViewArgs]::new(); $initialToolbar.ViewId = 'ToolbarWidget'; $initialToolbar.Name = 'Toolbar'; $initialToolbar.PluginId = $plugin.Id; $initialToolbar.AllowMultipleInstances = $true
    $initialViews.Add($initialToolbar); $initialRegistry.Add('StartPage',$initialViews); $plugin.AllAvailableViews = $initialRegistry
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
    if($pageB.Layout.Orientation -ne [Windows.Controls.Orientation]::Vertical -or $pageB.Layout.Children.Count -ne 2 -or !$pageB.Layout.Children[0].Size.IsAuto -or $pageB.Layout.Children[0].ViewProperties.ViewId -ne 'ToolbarWidget' -or $pageB.Layout.Children[1].ViewProperties) { throw 'New dashboard does not have a fitted toolbar row and empty content panel' }
    # This early lifecycle probe predates the WPF resource setup below.
    $pageB.Layout.Children.Clear()
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
    if ($vm.Pages.Count -ne 1 -or $vm.SelectedPage.Id -ne $pageA.Id -or $vm.RequestDeletePageCommand.CanExecute($null) -or $vm.QuickRemovePageCommand.CanExecute($null)) { throw 'Delete/last-page protection failed' }
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
    foreach ($type in @([Windows.Controls.GroupItem],[Windows.Controls.ProgressBar],[Windows.Controls.TextBox],[Windows.Controls.ScrollViewer],[Windows.Controls.Button],[Windows.Controls.Border],[Windows.Controls.ComboBox],[Windows.Controls.Expander],[Windows.Controls.GridSplitter],[Windows.Controls.ListBox],[Windows.Controls.CheckBox],[Windows.Controls.ContextMenu],[Windows.Controls.Menu],[Windows.Controls.MenuItem],[Windows.Controls.Primitives.Popup])) {
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
    $positionMenu = $view.GetType().GetMethod('PositionDashboardContextMenu',$flags)
    [void]$positionMenu.Invoke($view,@([Windows.Point]::new(260,180)))
    if ($view.ContextMenu.Placement -ne [Windows.Controls.Primitives.PlacementMode]::AbsolutePoint -or ![object]::ReferenceEquals($view.ContextMenu.PlacementTarget,$view) -or $view.ContextMenu.HorizontalOffset -ne 260 -or $view.ContextMenu.VerticalOffset -ne 180) { throw 'Background menu is not anchored at the clicked position' }
    [void]$positionMenu.Invoke($view,@([Windows.Point]::new(420,240)))
    if ([Windows.Controls.ContextMenuService]::GetHorizontalOffset($view) -ne 420 -or [Windows.Controls.ContextMenuService]::GetVerticalOffset($view) -ne 240) { throw 'Repeated clicks retain stale menu coordinates' }
    $newSettings = [LandingPage.LandingPageSettings]::new()
    $freshPage = [LandingPage.Models.DashboardPages]::Ensure($newSettings)
    $directPage = [LandingPage.Models.DashboardPage]::new()
    $addedPage = [LandingPage.Models.DashboardPages]::Add($newSettings,'New page')
    foreach ($defaultPage in @($freshPage,$directPage,$addedPage)) {
        $defaultLayout = $defaultPage.Layout
        if ($defaultLayout.Orientation -ne [Windows.Controls.Orientation]::Vertical -or $defaultLayout.Children.Count -ne 2 -or $defaultLayout.Children[0].ViewProperties.ViewId -ne 'ToolbarWidget' -or !$defaultLayout.Children[0].Size.IsAuto -or $defaultLayout.Children[1].ViewProperties -or !$defaultLayout.Children[1].Size.IsStar) { throw 'New page does not contain a fitted toolbar above an empty content panel' }
    }
    if ([object]::ReferenceEquals($freshPage.Layout,$addedPage.Layout)) { throw 'New page layouts share mutable instances' }
    'PASS: click-relative background menu placement and independent toolbar/content defaults for first, direct and added pages.'

    $view.GetType().GetMethod('ManagePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    if ($view.FindName('PagesOverlay').Visibility -ne [Windows.Visibility]::Visible) { throw 'Page manager did not open inside StartPage' }
    $view.GetType().GetMethod('ClosePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    if ($view.FindName('PagesOverlay').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Page manager did not close' }
    $pageNameBefore = $vm.SelectedPage.Name; $pageRootBefore = $vm.RootNodeViewModel
    $view.ShowPages(); $vm.EditedPageName = 'Discarded rename'
    if($vm.SelectedPage.Name -ne $pageNameBefore) { throw 'Draft rename leaked into live page' }
    Wait-Task ($vm.EndPageEditingAsync($false))
    $view.GetType().GetMethod('ClosePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    if($vm.SelectedPage.Name -ne $pageNameBefore -or ![object]::ReferenceEquals($vm.RootNodeViewModel,$pageRootBefore)) { throw 'Dismissed rename was saved or rebuilt the page' }
    $view.ShowPages(); $vm.EditedPageName = 'Accepted rename'
    $view.GetType().GetMethod('SavePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent)))
    if($vm.SelectedPage.Name -ne 'Accepted rename') { throw 'Done did not commit the rename' }
    $vm.SelectedPage.Name = $pageNameBefore
    $oldOverlay = (Get-Setting $vm.Settings.Settings 'OverlayOpacity')
    $vm.BeginPageEditing(); Set-Setting $vm.Settings.Settings 'OverlayOpacity' ([double]0.17)
    Wait-Task ($vm.EndPageEditingAsync($false))
    if((Get-Setting $vm.Settings.Settings 'OverlayOpacity') -ne $oldOverlay -or ![object]::ReferenceEquals($vm.RootNodeViewModel,$pageRootBefore)) { throw 'Dismiss did not restore background settings while preserving the page root' }

    $toolbarSettings = [LandingPage.Settings.ToolbarWidgetSettings]::new()
    $toolbarClone = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($toolbarSettings), [LandingPage.Settings.ToolbarWidgetSettings])
    if ($toolbarClone.Items.Count -ne 7 -or [object]::ReferenceEquals($toolbarClone.Items,$toolbarSettings.Items)) { throw 'Toolbar defaults duplicated/shared after serialization' }
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
    # Flyout behaviour: clicks inside the panel or on the bell keep it open, a click anywhere else closes it.
    $pressOn = {
        param($element)
        $press = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left)
        $press.RoutedEvent = [Windows.UIElement]::PreviewMouseDownEvent; $element.RaiseEvent($press)
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    }
    & $pressOn $view.FindName('NotificationItemsControl')
    & $pressOn $controls[9]
    if (!$vm.NotificationsOpen) { throw 'A click inside the notifications panel or on the bell closed it' }
    & $pressOn $controls[0]
    if ($vm.NotificationsOpen) { throw 'A click outside the notifications panel did not close it' }
    $vm.NotificationsOpen = $true; $window.UpdateLayout()
    $vm.Notifications.Add($badgeNotification); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $vm.Notifications.Remove($badgeNotification) | Out-Null; $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($vm.NotificationsOpen) { throw 'Dismissing the last notification did not close the notifications panel' }
    $vm.NotificationsOpen = $true; $window.UpdateLayout()
    $vm.ClearNotificationsCommand.Execute($null)
    if ($vm.NotificationsOpen) { throw 'Dismiss all did not close the notifications panel' }
    $vm.NotificationsOpen = $true; $window.UpdateLayout()
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
    $groupSettings = [LandingPage.Settings.ToolbarWidgetSettings]::new(); $groupSettings.Items.Clear()
    $otherGroup = [LandingPage.Settings.ToolbarGroup]::new(); $otherGroup.Name = 'Right group'; $groupSettings.Groups.Add($otherGroup)
    $groupEntries = @()
    foreach ($spec in @(@('page.previous','main','A'),@('page.manage',$otherGroup.Id,'X'),@('page.next','main','B'),@('profile.manage',$otherGroup.Id,'Y'),@('page.add','main','C'))) {
        $entry = [LandingPage.Settings.ToolbarEntry]::new(); $entry.Id = $spec[0]; $entry.GroupId = $spec[1]; $entry.Label = $spec[2]
        $groupSettings.Items.Add($entry); $groupEntries += $entry
    }
    $groupEditor = [LandingPage.Views.Settings.ToolbarSettingsView]::new(); $groupEditor.DataContext = $groupSettings
    $groupEditor.Measure([Windows.Size]::new(400,2000)); $groupEditor.Arrange([Windows.Rect]::new(0,0,400,2000)); $groupEditor.UpdateLayout()
    if ((@($groupEditor.ItemsView | ForEach-Object Label) -join ',') -ne 'A,B,C' -or $groupEditor.FindName('GroupItemsList').Items.Count -ne 3) { throw 'Toolbar group selector did not filter the real item list' }
    $secondGroupEditor = [LandingPage.Views.Settings.ToolbarSettingsView]::new(); $secondGroupEditor.DataContext = $groupSettings; $secondGroupEditor.SelectedGroup = $otherGroup
    if (@($secondGroupEditor.ItemsView).Count -ne 2 -or @($groupEditor.ItemsView).Count -ne 3 -or [Windows.Data.CollectionViewSource]::GetDefaultView($groupSettings.Items).Count -ne 5) { throw 'Group filter leaked into another editor or the widget collection' }
    $moveSender = [Windows.Controls.Button]::new(); $moveSender.DataContext = $groupEntries[0]
    [void]$groupEditor.GetType().GetMethod('MoveLater_Click',$flags).Invoke($groupEditor,@($moveSender,[Windows.RoutedEventArgs]::new()))
    if ((@($groupEditor.ItemsView | ForEach-Object Label) -join ',') -ne 'B,A,C' -or (@($groupSettings.Items | Where-Object GroupId -eq $otherGroup.Id | ForEach-Object Label) -join ',') -ne 'X,Y') { throw 'Group reorder moved outside its own group or changed hidden item ordering' }
    $groupEditor.SelectedGroup = $otherGroup
    $groupEditor.FindName('OptionPicker').SelectedValue = 'page.remove'
    [void]$groupEditor.GetType().GetMethod('AddItem_Click',$flags).Invoke($groupEditor,@($null,[Windows.RoutedEventArgs]::new()))
    if ($groupSettings.Items[$groupSettings.Items.Count-1].GroupId -ne $otherGroup.Id -or @($groupEditor.ItemsView).Count -ne 3) { throw 'Adding toolbar item ignored the selected group' }
    $groupEntries[4].GroupId = $otherGroup.Id
    if (@($groupEditor.ItemsView).Count -ne 4) { throw 'Moving an item between groups did not immediately refresh the list' }
    $removeGroupSender = [Windows.Controls.Button]::new(); $removeGroupSender.DataContext = $otherGroup
    [void]$groupEditor.GetType().GetMethod('RemoveGroup_Click',$flags).Invoke($groupEditor,@($removeGroupSender,[Windows.RoutedEventArgs]::new()))
    if ($groupEditor.SelectedGroup.Id -ne 'main' -or @($groupEditor.ItemsView).Count -ne 6 -or @($groupSettings.Items | Where-Object GroupId -ne 'main').Count) { throw 'Deleting selected group lost items or left a stale group filter' }
    [void]$groupEditor.GetType().GetMethod('AddGroup_Click',$flags).Invoke($groupEditor,@($null,[Windows.RoutedEventArgs]::new()))
    if ($groupEditor.SelectedGroup.Id -eq 'main' -or @($groupEditor.ItemsView).Count -ne 0) { throw 'Newly added group was not selected with an empty item list' }
    $groupEditor.DataContext = [LandingPage.Settings.ToolbarWidgetSettings]::new()
    $groupEntries[0].GroupId = 'removed-provider-group'; $groupSettings.Items.Add([LandingPage.Settings.ToolbarEntry]::new())
    if (@($groupEditor.ItemsView).Count -ne 7) { throw 'Old settings continued changing the replacement editor' }
    'PASS: independent group-filtered item lists, selected-group adds, group-only reorder, live reassignment, deleted-group recovery and settings replacement.'
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
    if ($starterPreviews.Count -ne 8 -or @($starterPreviews | Where-Object { !$_.Layout -or !$_.Shelves }).Count -ne 0) { throw 'Starter gallery preview layout/shelf bindings failed' }
    $starterButtons = @([ToolbarTestVisuals]::Buttons($view.FindName('ProfilesOverlay')) | Where-Object { $_.Content -eq 'Add profile' })
    if ($starterButtons.Count -ne 7 -or @($starterButtons | Where-Object { !$_.Command -or !$_.CommandParameter }).Count -ne 0) { throw 'Starter gallery cards are missing their commands or template parameters' }
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
    $filterSet = [Collections.Generic.HashSet[guid]]::new()
    $firstFilterId = [guid]::NewGuid(); $secondFilterId = [guid]::NewGuid()
    $firstFilter = [LandingPage.ViewModels.ShelveViewModel+ToggleFilter]::new($firstFilterId,'Action',$filterSet,$refreshShelf,$false)
    $secondFilter = [LandingPage.ViewModels.ShelveViewModel+ToggleFilter]::new($secondFilterId,'Action RPG',$filterSet,$refreshShelf,$false)
    $typedFilter = [Windows.Controls.ComboBox]::new(); $typedFilter.IsEditable = $true; $typedFilter.IsTextSearchEnabled = $false
    [void]$typedFilter.Items.Add($firstFilter); [void]$typedFilter.Items.Add($secondFilter)
    $typedFilter.Text = ' action '
    if (![LandingPage.Views.ShelveView]::ApplyTypedFilter($typedFilter,$false) -or !$filterSet.Contains($firstFilterId) -or $typedFilter.IsDropDownOpen) { throw 'Typed exact filter cannot be added with dropdown closed' }
    if (![LandingPage.Views.ShelveView]::ApplyTypedFilter($typedFilter,$false) -or $filterSet.Contains($firstFilterId)) { throw 'Typed filter cannot be toggled off' }
    $typedFilter.Text = 'Act'
    if ([LandingPage.Views.ShelveView]::ApplyTypedFilter($typedFilter,$false) -or $filterSet.Count) { throw 'Ambiguous partial filter changed a selection' }
    $typedFilter.Text = 'Action R'
    if (![LandingPage.Views.ShelveView]::ApplyTypedFilter($typedFilter,$false) -or !$filterSet.Contains($secondFilterId)) { throw 'Unique partial name cannot be added' }
    if (![LandingPage.Views.ShelveView]::ApplyTypedFilter($typedFilter,$true) -or $filterSet.Count) { throw 'Remove action did not remove typed selection' }
    $typedFilter.Text = 'Unknown'
    if ([LandingPage.Views.ShelveView]::ApplyTypedFilter($typedFilter,$false) -or $filterSet.Count) { throw 'Unknown typed name changed filter selections' }
    'PASS: closed-dropdown typed filters add/remove, exact names ignore case/whitespace, unique prefixes work and ambiguous/unknown names preserve selections.'
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
    if ($vm.NotificationCount -ne 0 -or $vm.NotificationsOpen) { throw 'Clearing the last notifications should close the panel' }
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
    Set-Setting $settingsModel.Settings 'EnableDashboardDiagnostics' $true
    $hostDiagnosticsType = $plugin.GetType().Assembly.GetType('LandingPage.Utils.HostWindowDiagnostics')
    $hostDiagnostics = [Activator]::CreateInstance($hostDiagnosticsType,$flags,$null,@($plugin,$window),$null)
    try {
        $beforeBounds = [Windows.Rect]::new($window.Left,$window.Top,$window.ActualWidth,$window.ActualHeight)
        $messageArgs = @([IntPtr]::Zero,0x007E,[IntPtr]32,[IntPtr]((1080 -shl 16) -bor 1920),$false)
        $hostDiagnosticsType.GetMethod('Message',$flags).Invoke($hostDiagnostics,$messageArgs) | Out-Null
        if($messageArgs[4] -or [Windows.Rect]::new($window.Left,$window.Top,$window.ActualWidth,$window.ActualHeight) -ne $beforeBounds) { throw 'Window diagnostics consumed a display change or modified window bounds' }
        $deadline = [datetime]::UtcNow.AddSeconds(3)
        while(![IO.File]::ReadAllText($diagnosticFile).Contains('WM_DISPLAYCHANGE pixels=1920x1080') -and [datetime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 20 }
        $hostDebug = [IO.File]::ReadAllText($diagnosticFile)
        if(!$hostDebug.Contains('nativePixels=') -or !$hostDebug.Contains('workPixels=') -or !$hostDebug.Contains('sizeToContent=') -or !$hostDebug.Contains('WM_DISPLAYCHANGE pixels=1920x1080')) { throw 'Display diagnostics did not record window/monitor geometry and display message' }
    } finally { $hostDiagnostics.Dispose(); Set-Setting $settingsModel.Settings 'EnableDashboardDiagnostics' $false }
    if($hostDiagnosticsType.GetField('source',$flags).GetValue($hostDiagnostics) -or $hostDiagnosticsType.GetField('settled',$flags).GetValue($hostDiagnostics).IsEnabled) { throw 'Window diagnostics retained its native hook or timer after disposal' }
    'PASS: opt-in display/window geometry diagnostics observe display changes without consuming messages or resizing the host; hook/timer cleanup.'
    $restoreDecision = $hostDiagnosticsType.GetMethod('ShouldRestoreStartupBounds',[Reflection.BindingFlags]'Static,NonPublic')
    $savedBounds = [Windows.Rect]::new(100,100,800,600); $monitorBounds = [Windows.Rect]::new(0,0,1920,1080)
    if(!$restoreDecision.Invoke($null,@($savedBounds,$monitorBounds,$monitorBounds,[Windows.WindowState]::Normal,$false,$true))) { throw 'Startup safeguard did not identify automatic full-monitor growth' }
    foreach($case in @(@([Windows.WindowState]::Maximized,$false,$true),@([Windows.WindowState]::Normal,$true,$true),@([Windows.WindowState]::Normal,$false,$false))) {
        if($restoreDecision.Invoke($null,@($savedBounds,$monitorBounds,$monitorBounds,$case[0],$case[1],$case[2]))) { throw 'Startup safeguard would interfere with maximize, user input or later resizes' }
    }
    $guardWindow = [Windows.Window]::new(); $guardWindow.Width = 500; $guardWindow.Height = 350; $guardWindow.Left = 100; $guardWindow.Top = 100; $guardWindow.Show(); $guardWindow.UpdateLayout()
    $guard = [Activator]::CreateInstance($hostDiagnosticsType,$flags,$null,@($plugin,$guardWindow),$null)
    try {
        $guardWindow.Left = 0; $guardWindow.Top = 0; $guardWindow.Width = [Windows.SystemParameters]::PrimaryScreenWidth; $guardWindow.Height = [Windows.SystemParameters]::PrimaryScreenHeight
        $guardWindow.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
        if([Math]::Abs($guardWindow.Width-500) -gt 1 -or [Math]::Abs($guardWindow.Height-350) -gt 1) { throw 'Startup safeguard did not restore the previously restored normal-window size' }
    } finally { $guard.Dispose(); $guardWindow.Close() }
    'PASS: startup-only restoration for automatic monitor-sized normal windows, with manual/maximized/later changes excluded.'


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
    if (!$featured.FeaturedGame) { throw 'Hover-only mode did not provide a fallback game when keep-last was disabled' }
    if([DashboardTestProxy]::LibraryEnumerations -gt $queriesBeforeHover+1) { throw 'Random preview fallback repeatedly enumerated the library' }
    $queriesBeforeHover = [DashboardTestProxy]::LibraryEnumerations
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
    $detailWidgets = @(); $detailNodes = @(); $detailIds = @()
    $availableDetails = @($plugin.GetAvailableStartPageViews().Views)
    $detailQueries = [DashboardTestProxy]::LibraryEnumerations
    $featuredGame.CoverImage = $imagePath; $featuredGame.BackgroundImage = $imagePath
    foreach ($definition in [LandingPage.Models.GameDetailWidgets]::Definitions) {
        if (@($availableDetails | Where-Object ViewId -eq $definition.Id).Count -ne 1) { throw ('Detail widget missing from Add menu: ' + $definition.Id) }
        $detailId = [guid]::NewGuid(); $detailWidget = $plugin.GetStartPageView($definition.Id,$detailId)
        $detailProps = [LandingPage.Models.Layout.ViewProperties]::new(); $detailProps.ViewId = $definition.Id; $detailProps.InstanceId = $detailId
        $detailProps.GameDetailSettings.HoverDelayMilliseconds = 0
        $detailNode = [LandingPage.Models.Layout.GridNode]::new(); $detailNode.ViewProperties = $detailProps
        $detailModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($detailNode); $detailModel.IsLeaf = $true; $detailModel.HasView = $true
        [void]$detailModel.View.Children.Add($detailWidget)
        $detailNodeView = [LandingPage.Views.GridNodeView]::new(); $detailNodeView.DataContext = $detailModel
        [void]$layout.Children.Add($detailNodeView); $window.UpdateLayout()
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
        Wait-Task ($detailWidget.OnViewShownAsync())
        if (![object]::ReferenceEquals($detailWidget.Settings,$detailProps.GameDetailSettings) -or $detailWidget.FeaturedGame.Id -ne $featuredGame.Id) { throw ('Detail widget did not bind to its own settings/selected game: ' + $definition.Id) }
        $renderedSections = $detailWidget.GetType().GetField('sectionPanels',$flags).GetValue($detailWidget)
        if ($definition.IsArtwork) {
            if ($renderedSections.Count -ne 0 -or !$detailWidget.GetType().GetField('artwork',$flags).GetValue($detailWidget).Source) { throw 'Artwork detail widget rendered unrelated content or no image' }
        } elseif ($renderedSections.Count -ne 1 -or !$renderedSections.ContainsKey($definition.Section)) { throw ('Standalone widget rendered the wrong sections: ' + $definition.Id) }
        $detailEditor = [LandingPage.Views.Settings.GameDetailSettingsView]::new(); $detailEditor.WidgetId = $definition.Id; $detailEditor.DataContext = $detailProps.GameDetailSettings
        [void]$layout.Children.Add($detailEditor); $window.UpdateLayout()
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
        if ($detailEditor.FindName('ArtworkOptions').Visibility -ne $(if ($definition.IsArtwork) { [Windows.Visibility]::Visible } else { [Windows.Visibility]::Collapsed })) { throw 'Detail settings show fields from the wrong widget type' }
        [void]$layout.Children.Remove($detailEditor)
        $detailWidgets += $detailWidget; $detailNodes += $detailNodeView; $detailIds += $detailId
    }
    if ([DashboardTestProxy]::LibraryEnumerations -ne $detailQueries) { throw 'Selected detail widgets scanned the entire library' }
    $workerUpdateArgs = [Playnite.SDK.ItemUpdatedEventArgs[Playnite.SDK.Models.Game]]::new($featuredGame,$featuredGame)
    $workerChangedArgs = [Playnite.SDK.ItemCollectionChangedEventArgs[Playnite.SDK.Models.Game]]::new([Collections.Generic.List[Playnite.SDK.Models.Game]]::new(),[Collections.Generic.List[Playnite.SDK.Models.Game]]::new())
    foreach($detailWidget in $detailWidgets) {
        foreach($method in @('GamesUpdated','GamesChanged','SelectionChanged','HoverChanged','FallbackChanged','SettingChanged')) {
            $workerArgs = if($method -eq 'GamesUpdated') { $workerUpdateArgs } elseif($method -eq 'GamesChanged') { $workerChangedArgs } elseif($method -eq 'SettingChanged') { [ComponentModel.PropertyChangedEventArgs]::new('TextSize') } else { [EventArgs]::Empty }
            $workerTask = [ToolbarTestVisuals]::WorkerEvent($detailWidget,$method,$workerArgs)
            if(!$workerTask.Wait(5000)) { throw ('Background event blocked while waiting for the UI: '+$method) }
            [void]$workerTask.GetAwaiter().GetResult()
        }
    }
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    foreach($detailWidget in $detailWidgets) { if($detailWidget.FeaturedGame.Id -ne $featuredGame.Id) { throw 'Background database events lost the selected preview' } }
    'PASS: all nine game-detail widgets accept import/update/collection/selection/hover/settings events from workers without accessing WPF or blocking the importing thread.'
    $detailWidgets[1].Settings.TextSize = 31
    if ($detailWidgets[2].Settings.TextSize -eq 31) { throw 'Independent detail widgets share formatting settings' }
    [void]$setHover.Invoke($plugin,@($hoverPreviewGame,$hoverOwnerB,$hoverGroup))
    # The update must finish before lower-priority input/background work, without
    # requiring each widget to await a thread-pool task.
    $fastPreviewReady = [bool[]]@($false)
    $previewFrame = [Windows.Threading.DispatcherFrame]::new()
    [void]$app.Dispatcher.BeginInvoke([Action]{
        $fastPreviewReady[0] = @($detailWidgets | Where-Object { $_.FeaturedGame.Id -ne $hoverPreviewGame.Id }).Count -eq 0
        $previewFrame.Continue = $false
    },[Windows.Threading.DispatcherPriority]::Input)
    [Windows.Threading.Dispatcher]::PushFrame($previewFrame)
    if (!$fastPreviewReady[0]) { throw 'Hover previews waited behind input/background work or asynchronous lookup tasks' }
    foreach ($detailWidget in $detailWidgets) { Wait-Task ($detailWidget.OnViewShownAsync()); if ($detailWidget.FeaturedGame.Id -ne $hoverPreviewGame.Id) { throw 'Detail widgets did not synchronize to the hovered game' } }
    [void]$setHover.Invoke($plugin,@($null,$hoverOwnerB,$hoverGroup))
    foreach ($detailWidget in $detailWidgets) { Wait-Task ($detailWidget.OnViewShownAsync()); if ($detailWidget.FeaturedGame.Id -ne $featuredGame.Id) { throw 'Detail widgets did not synchronize selected fallback' } }
    $titleSectionsBeforeSameGame = $detailWidgets[1].GetType().GetField('sectionPanels',$flags).GetValue($detailWidgets[1])[[LandingPage.Settings.FeaturedGameSection]::Title]
    [void]$setHover.Invoke($plugin,@($featuredGame,$hoverOwnerB,$hoverGroup))
    foreach ($detailWidget in $detailWidgets) { $fastLookup = $detailWidget.OnViewShownAsync(); if (!$fastLookup.IsCompleted) { throw 'Indexed context lookup waited for a worker thread' }; Wait-Task $fastLookup }
    $titleSectionsAfterSameGame = $detailWidgets[1].GetType().GetField('sectionPanels',$flags).GetValue($detailWidgets[1])[[LandingPage.Settings.FeaturedGameSection]::Title]
    if (![object]::ReferenceEquals($titleSectionsBeforeSameGame,$titleSectionsAfterSameGame)) { throw 'Preview rebuilt an unchanged game section' }
    [void]$setHover.Invoke($plugin,@($null,$hoverOwnerB,$hoverGroup))
    $savedPreviewLayout = Get-Setting $settingsModel.Settings 'GridLayout'
    $fallbackShelfId = [guid]::NewGuid(); $fallbackRoot = [LandingPage.Models.Layout.GridNode]::new()
    $fallbackShelfProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $fallbackShelfProperties.ViewId = 'GameShelves'; $fallbackShelfProperties.InstanceId = $fallbackShelfId; $fallbackRoot.ViewProperties = $fallbackShelfProperties
    $fallbackShelf = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.ViewModels.ShelveViewModel])
    $fallbackGames = [Collections.ObjectModel.ObservableCollection[LandingPage.Models.GameModel]]::new(); $fallbackGames.Add([LandingPage.Models.GameModel]::new($hoverPreviewGame)); $fallbackGames.Add([LandingPage.Models.GameModel]::new($featuredGame))
    $fallbackShelf.Games = $fallbackGames; $fallbackShelf.CollectionViewSource = [Windows.Data.CollectionViewSource]::new(); $fallbackShelf.CollectionViewSource.Source = $fallbackGames
    $fallbackGroup = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.ViewModels.ShelvesViewModel]); $fallbackGroup.ShelveViewModels = [Collections.ObjectModel.ObservableCollection[LandingPage.ViewModels.ShelveViewModel]]::new(); $fallbackGroup.ShelveViewModels.Add($fallbackShelf)
    $shelfRegistry = $plugin.GetType().GetField('shelvesViewModels',$flags).GetValue($plugin); $shelfRegistry.Add($fallbackShelfId,$fallbackGroup)
    Set-Setting $settingsModel.Settings 'GridLayout' $fallbackRoot
    $notifyFallback = $plugin.GetType().GetMethod('NotifyPreviewFallbackChanged',$flags)
    $beforeShelfFallbackQueries = [DashboardTestProxy]::LibraryEnumerations
    foreach($detailWidget in $detailWidgets) { $detailWidget.Settings.Source = [LandingPage.Settings.FeaturedGameSource]::HoveredGame; $detailWidget.Settings.KeepLastHoveredGame = $false; Wait-Task ($detailWidget.OnViewShownAsync()); if($detailWidget.FeaturedGame.Id -ne $hoverPreviewGame.Id) { throw 'Detail widget did not start with the first shelf game' } }
    $fallbackGames.Move(1,0); $notifyFallback.Invoke($plugin,@())
    foreach($detailWidget in $detailWidgets) { Wait-Task ($detailWidget.OnViewShownAsync()); if($detailWidget.FeaturedGame.Id -ne $featuredGame.Id) { throw 'Shelf first-game changes did not refresh detail fallbacks' } }
    if([DashboardTestProxy]::LibraryEnumerations -ne $beforeShelfFallbackQueries) { throw 'Shelf fallback enumerated the library instead of reusing shelf games' }
    Set-Setting $settingsModel.Settings 'GridLayout' ([LandingPage.Models.Layout.GridNode]::new()); $notifyFallback.Invoke($plugin,@())
    foreach($detailWidget in $detailWidgets) { Wait-Task ($detailWidget.OnViewShownAsync()) }
    $randomFallbackId = $detailWidgets[0].FeaturedGame.Id
    if(!$randomFallbackId -or @($detailWidgets | Where-Object { $_.FeaturedGame.Id -ne $randomFallbackId }).Count) { throw 'Widgets did not share a random fallback without shelves' }
    $randomFallbackQueries = [DashboardTestProxy]::LibraryEnumerations; $notifyFallback.Invoke($plugin,@())
    foreach($detailWidget in $detailWidgets) { Wait-Task ($detailWidget.OnViewShownAsync()); if($detailWidget.FeaturedGame.Id -ne $randomFallbackId) { throw 'Random fallback changed unnecessarily' } }
    if([DashboardTestProxy]::LibraryEnumerations -ne $randomFallbackQueries) { throw 'Cached random fallback rescanned the library' }
    Set-Setting $settingsModel.Settings 'GridLayout' $savedPreviewLayout; [void]$shelfRegistry.Remove($fallbackShelfId)
    'PASS: first shelf game on initial hover-only previews, shelf reorder refresh, synchronized random fallback and no repeat library scans.'
    for ($detailIndex = 0; $detailIndex -lt $detailWidgets.Count; ++$detailIndex) {
        $definition = [LandingPage.Models.GameDetailWidgets]::Definitions[$detailIndex]
        $plugin.OnViewRemoved($definition.Id,$detailIds[$detailIndex])
        if (!$detailWidgets[$detailIndex].GetType().GetField('closed',$flags).GetValue($detailWidgets[$detailIndex]) -or $detailWidgets[$detailIndex].GetType().GetField('subscribed',$flags).GetValue($detailWidgets[$detailIndex])) { throw 'Removed detail widget retained subscriptions' }
        $closedWorker = [ToolbarTestVisuals]::WorkerEvent($detailWidgets[$detailIndex],'GamesUpdated',$workerUpdateArgs)
        if(!$closedWorker.Wait(5000)) { throw 'Closed widget blocked a background import' }
        [void]$closedWorker.GetAwaiter().GetResult()

        [void]$layout.Children.Remove($detailNodes[$detailIndex])
    }
    'PASS: nine standalone detail widgets, Add catalog, actual ancestor settings binding, isolated sections/artwork, contextual editors, synchronized selected/hovered sources, no library scans and removal cleanup.'
    $statsCatalog = @($plugin.GetAvailableStartPageViews().Views | Where-Object ViewId -eq 'LibraryStatistics')
    if($statsCatalog.Count -ne 1 -or !$statsCatalog[0].AllowMultipleInstances) { throw 'Library statistics not registered correctly' }
    # The test plugin is allocated without its constructor; initialize its instance registry.
    Set-Field $plugin 'libraryStatisticsViews' ([Collections.Generic.Dictionary[guid,LandingPage.Views.LibraryStatisticsView]]::new())
    $statsQueryStart = [DashboardTestProxy]::LibraryEnumerations
    $statsId = [guid]::NewGuid(); $statsWidget = $plugin.GetStartPageView('LibraryStatistics',$statsId)
    $statsProps = [LandingPage.Models.Layout.ViewProperties]::new(); $statsProps.ViewId = 'LibraryStatistics'; $statsProps.InstanceId = $statsId
    $statsProps.LibraryStatisticsSettings.ValueSize = 41
    $statsNode = [LandingPage.Models.Layout.GridNode]::new(); $statsNode.ViewProperties = $statsProps
    $statsModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($statsNode); $statsModel.IsLeaf = $true; $statsModel.HasView = $true
    [void]$statsModel.View.Children.Add($statsWidget)
    $statsNodeView = [LandingPage.Views.GridNodeView]::new(); $statsNodeView.DataContext = $statsModel
    [void]$layout.Children.Add($statsNodeView); $window.UpdateLayout()
    Wait-Task ($statsWidget.OnViewShownAsync()); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $window.UpdateLayout()
    $statsText = @([ToolbarTestVisuals]::Text($statsWidget) | ForEach-Object Text)
    if(![object]::ReferenceEquals($statsWidget.Settings,$statsProps.LibraryStatisticsSettings) -or !($statsText -match 'Total games') -or !($statsText -match 'Total playtime')) { throw ('Statistics binding or rendered counters are missing: bound=' + [object]::ReferenceEquals($statsWidget.Settings,$statsProps.LibraryStatisticsSettings) + '; text=' + ($statsText -join '|')) }
    $statsEditor = [LandingPage.Views.Settings.LibraryStatisticsSettingsView]::new(); $statsEditor.DataContext = $statsWidget.Settings
    [void]$layout.Children.Add($statsEditor); $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if($statsEditor.FindName('Statuses') -or $statsEditor.FindName('CompletionMappingOptions')) { throw 'Statistics editor still contains the removed completion mapping' }
    $totalSection = $statsEditor.FindName('CounterOptions').Content.Children[0]; $totalSection.IsExpanded = $true; $window.UpdateLayout()
    $colorEditor = $totalSection.Content.Children[0]
    $totalColorModel = @($statsWidget.Settings.CounterPlacements | Where-Object Counter -eq ([LandingPage.Settings.StatisticsCounter]::Total))[0].Colors
    if(![object]::ReferenceEquals($colorEditor.Content,$totalColorModel)) { throw 'Per-statistic color editor did not bind to the correct statistic' }
    $colorSection = [ToolbarTestVisuals]::Named($colorEditor,'StatisticColorsExpander')
    if(!$colorSection -or [ToolbarTestVisuals]::Named($colorSection,'StatisticValueColorPicker')) { throw 'Statistic color pickers were instantiated before opening Colors' }
    $colorSection.IsExpanded = $true; $window.UpdateLayout()
    if(![ToolbarTestVisuals]::Named($colorSection,'StatisticValueColorPicker')) { throw 'Opening statistic Colors did not create its color pickers' }
    $totalSection.IsExpanded = $false
    if($statsEditor.FindName('SelectedCounterPlacements')) { throw 'A separate per-statistic positions list is still present' }
    if(![object]::ReferenceEquals($statsEditor.Content.Children[0],$statsEditor.FindName('CounterGridOptions'))) { throw 'Global grid setup is not at the top of statistic settings' }
    $totalSection.IsExpanded = $true; $window.UpdateLayout()
    $positionEditor = $totalSection.Content.Children[1]
    $storedPosition = @($statsWidget.Settings.CounterPlacements | Where-Object Counter -eq ([LandingPage.Settings.StatisticsCounter]::Total))[0]
    if(![object]::ReferenceEquals($positionEditor.Content,$storedPosition)) { throw 'Inline position editor did not bind to its own statistic' }
    $positionSection = [ToolbarTestVisuals]::Named($positionEditor,'StatisticPositionExpander')
    if($positionSection.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Inline positions should be hidden when grid mode is disabled' }
    $statsWidget.Settings.UseCounterGrid = $true; $window.UpdateLayout()
    if($positionSection.Visibility -ne [Windows.Visibility]::Visible) { throw 'Enabling grid mode did not reveal inline statistic positions' }
    $positionSection.IsExpanded = $true; $window.UpdateLayout()
    $rowInput = [ToolbarTestVisuals]::Named($positionSection,'StatisticPositionRow'); $rowInput.Value = 3
    if($storedPosition.Row -ne 3) { throw 'Inline row input did not update the persisted statistic position' }
    $statsWidget.Settings.ShowTotal = $false; $statsWidget.Settings.ShowTotal = $true
    if($storedPosition.Row -ne 3) { throw 'Toggling a statistic erased its inline position' }
    $statsWidget.Settings.UseCounterGrid = $false; $statsWidget.Settings.ResetCounterPositions(); $window.UpdateLayout()
    if($statsWidget.Settings.CounterPlacements[0].Row -ne 1) { throw 'Reset positions did not update inline placement models' }
    if($statsEditor.FindName('ValueColorOptions')) { throw 'Redundant separate global value color selector is still present' }
    $statsEditor.GetType().GetMethod('GlobalTextColor_Editing',$flags).Invoke($statsEditor,@($null,[Windows.RoutedEventArgs]::new()))
    if($statsWidget.Settings.ValueColorMode -ne [LandingPage.Settings.StatisticsValueColor]::Theme) { throw 'Unified global theme text color did not apply to values' }
    $statsWidget.Settings.CustomLabelColor = $true; $statsEditor.GetType().GetMethod('GlobalTextColor_Editing',$flags).Invoke($statsEditor,@($null,[Windows.RoutedEventArgs]::new()))
    if($statsWidget.Settings.ValueColorMode -ne [LandingPage.Settings.StatisticsValueColor]::MatchLabels) { throw 'Unified custom text color did not apply to values and labels' }
    $statsWidget.Settings.CustomLabelColor = $false; $statsWidget.Settings.ValueColorMode = [LandingPage.Settings.StatisticsValueColor]::Theme
    'PASS: top-level grid setup, lazy positions inside each statistic, saved inline row edits and unified global text color.'
    [void]$layout.Children.Remove($statsEditor)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)

    Wait-Task ($statsWidget.OnViewHiddenAsync()); Wait-Task ($statsWidget.OnViewShownAsync())
    $statsWidget.Settings.Rows = $true; $statsWidget.Settings.ShowInstalled = $false; $statsWidget.Settings.ShowChart = $false
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
     $window.UpdateLayout()
    if([DashboardTestProxy]::LibraryEnumerations -ne $statsQueryStart + 1 -or (@([ToolbarTestVisuals]::Text($statsWidget) | ForEach-Object Text) -match 'Installed')) { throw 'Statistics revisit rescanned the library or ignored counter settings' }
    $statsSource = $statsWidget.GetType().GetField('source',$flags).GetValue($statsWidget)
    $extraStatsGame = [Playnite.SDK.Models.Game]::new(); $extraStatsGame.Name = 'Statistics event fixture'; $extraStatsGame.IsInstalled = $true
    $addedStats = [Collections.Generic.List[Playnite.SDK.Models.Game]]::new(); $addedStats.Add($extraStatsGame)
    $removedStats = [Collections.Generic.List[Playnite.SDK.Models.Game]]::new()
    $collectionArgs = [Playnite.SDK.ItemCollectionChangedEventArgs[Playnite.SDK.Models.Game]]::new($addedStats,$removedStats)
    $statsSource.GetType().GetMethod('CollectionChanged',$flags).Invoke($statsSource,@($null,$collectionArgs))
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    $sourceTotals = $statsSource.GetType().GetMethod('Calculate',$flags).Invoke($statsSource,@($statsWidget.Settings,$false))
    if(!(@([ToolbarTestVisuals]::Text($statsWidget) | ForEach-Object Text) -contains $sourceTotals.Total.ToString('N0'))) { throw 'Library addition did not update rendered statistics' }
    $extraStatsGame.Playtime = 7200
    $updates = [Collections.Generic.List[Playnite.SDK.ItemUpdateEvent[Playnite.SDK.Models.Game]]]::new()
    $updates.Add([Playnite.SDK.ItemUpdateEvent[Playnite.SDK.Models.Game]]::new($extraStatsGame,$extraStatsGame))
    $statsSource.GetType().GetMethod('Updated',$flags).Invoke($statsSource,@($null,[Playnite.SDK.ItemUpdatedEventArgs[Playnite.SDK.Models.Game]]::new($updates)))
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    $sourceTotals = $statsSource.GetType().GetMethod('Calculate',$flags).Invoke($statsSource,@($statsWidget.Settings,$false))
    if(!(@([ToolbarTestVisuals]::Text($statsWidget) | ForEach-Object Text) -contains $sourceTotals.Playtime)) { throw 'Library update did not refresh rendered playtime' }
    $otherStatsId = [guid]::NewGuid(); $otherStatsWidget = $plugin.GetStartPageView('LibraryStatistics',$otherStatsId)
    Wait-Task ($otherStatsWidget.OnViewShownAsync())
    if(![object]::ReferenceEquals($otherStatsWidget.GetType().GetField('source',$flags).GetValue($otherStatsWidget),$statsSource) -or [DashboardTestProxy]::LibraryEnumerations -ne $statsQueryStart + 1) { throw 'Statistics instances did not share the library index' }
    $plugin.OnViewRemoved('LibraryStatistics',$otherStatsId)
    $statsWidget.Settings.ShowChart = $true
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    if([ToolbarTestVisuals]::Named($statsWidget,'StatisticsPlot')) { throw 'Library Statistics still renders graphs from old settings' }
    if($statsEditor.FindName('ChartOptions').Visibility -ne [Windows.Visibility]::Collapsed -or $statsEditor.FindName('ChartPaletteOptions').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Statistics still exposes graph settings' }
    if(@($plugin.GetAvailableStartPageViews().Views | Where-Object ViewId -eq 'LibraryChart').Count -ne 1) { throw 'Separate Library chart is not in the widget catalog' }
    function Wait-ChartRender {
        $until = [datetime]::UtcNow.AddMilliseconds(120)
        while([datetime]::UtcNow -lt $until) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
        $window.UpdateLayout()
    }
    $chartIds = @(); $chartNodes = @(); $chartWidgets = @()
    for($chartIndex = 0; $chartIndex -lt 2; $chartIndex++) {
        $chartId = [guid]::NewGuid(); $chartWidget = $plugin.GetStartPageView('LibraryChart',$chartId)
        $chartProps = [LandingPage.Models.Layout.ViewProperties]::new(); $chartProps.ViewId = 'LibraryChart'; $chartProps.InstanceId = $chartId
        $chartProps.LibraryStatisticsSettings.Metric = [LandingPage.Settings.StatisticsMetric]::Playtime
        $chartProps.LibraryStatisticsSettings.Breakdown = [LandingPage.Settings.StatisticsBreakdown]::Games
        $chartNode = [LandingPage.Models.Layout.GridNode]::new(); $chartNode.ViewProperties = $chartProps
        $chartModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($chartNode); $chartModel.IsLeaf = $true; $chartModel.HasView = $true; [void]$chartModel.View.Children.Add($chartWidget)
        $chartNodeView = [LandingPage.Views.GridNodeView]::new(); $chartNodeView.DataContext = $chartModel; [void]$layout.Children.Add($chartNodeView)
        Wait-Task ($chartWidget.OnViewShownAsync()); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
        $chartText = @([ToolbarTestVisuals]::Text($chartWidget) | ForEach-Object Text)
        if(![object]::ReferenceEquals($chartWidget.Settings,$chartProps.LibraryStatisticsSettings) -or ($chartText -contains 'Total games') -or !($chartText -match 'Playtime:') -or !($chartText -match ' h')) { throw 'Separate chart failed to bind or rendered counters instead of a playtime graph' }
        $chartIds += $chartId; $chartNodes += $chartNodeView; $chartWidgets += $chartWidget
    }
    $chartEditor = [LandingPage.Views.Settings.LibraryStatisticsSettingsView]::new(); $chartEditor.ChartOnly = $true; $chartEditor.DataContext = $chartWidgets[0].Settings
    if($chartEditor.FindName('CounterOptions').Visibility -ne [Windows.Visibility]::Collapsed -or $chartEditor.FindName('ValueSizeInput').Visibility -ne [Windows.Visibility]::Collapsed -or $chartEditor.FindName('ShowChartOption').Visibility -ne [Windows.Visibility]::Collapsed -or $chartEditor.FindName('CompletionMappingOptions') -or $chartEditor.FindName('ChartOptions').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Chart editor exposes irrelevant counter controls' }
    if($chartEditor.FindName('MultiChartOptions') -isnot [Windows.Controls.StackPanel] -or $chartEditor.FindName('ExtraChartList').Items.Count -ne 1 -or $chartEditor.FindName('RemoveChartButton').IsEnabled) { throw 'Unified Charts manager must initially contain one non-removable chart' }
    if($chartEditor.FindName('ChartColors').Visibility -ne [Windows.Visibility]::Collapsed -or $chartEditor.FindName('ScopeOptions').Visibility -ne [Windows.Visibility]::Collapsed -or $chartEditor.FindName('LayoutTextOptions').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Default-chart options still appear outside Charts' }
    $selectedChartEditor = $chartEditor.FindName('ExtraChartEditor').Content
    if(!$selectedChartEditor -or ![object]::ReferenceEquals($selectedChartEditor.DataContext,$chartWidgets[0].Settings) -or $selectedChartEditor.FindName('ChartOptions').Visibility -ne [Windows.Visibility]::Visible) { throw 'First chart is not editable inside Charts' }
    $chartEditor.GetType().GetMethod('RemoveChart_Click',$flags).Invoke($chartEditor,@($null,[Windows.RoutedEventArgs]::new()))
    if($chartEditor.FindName('ExtraChartList').Items.Count -ne 1) { throw 'The final chart could be removed' }
    $chartWidgets[0].Settings.CustomChartColors = $false
    $chartWidgets[0].Settings.Breakdown = [LandingPage.Settings.StatisticsBreakdown]::Installation
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $defaultPlot = [ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot')
    if($defaultPlot.Chart.Series[0].Fill.GradientStops[1].Color.ToString() -ne '#FF42A5F5') { throw 'Default chart palette is not colorful' }
    $chartWidgets[0].Settings.CustomChartColors = $true; $chartWidgets[0].Settings.ChartColor1 = '#FE1234'
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $customPlot = [ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot')
    if($customPlot.Chart.Series[0].Fill.GradientStops[1].Color.ToString() -ne '#FFFE1234' -or ($customPlot.Chart.InnerRadius -le 0)) { throw 'Custom colors or doughnut geometry failed' }
    $chartEditor.GetType().GetMethod('AddChart_Click',$flags).Invoke($chartEditor,@($null,[Windows.RoutedEventArgs]::new()))
    $extraChart = $chartWidgets[0].Settings.ExtraCharts[0]; $extraChart.ChartTitle = 'Second graph'; $extraChart.Metric = [LandingPage.Settings.StatisticsMetric]::GameCount
    $chartWidgets[0].Settings.ChartColumns = 2
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender; $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $chartGrid = [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartGrid')
    if($chartGrid.Children.Count -ne 2 -or $chartGrid.ColumnDefinitions.Count -ne 2 -or !(@([ToolbarTestVisuals]::Text($chartWidgets[0]) | ForEach-Object Text) -contains 'Second graph')) { throw 'Additional chart did not render in the same widget grid' }
    $chartWidgets[0].Height = 180
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $chartViewport = $chartWidgets[0].GetType().GetField('viewport',$flags).GetValue($chartWidgets[0])
    if($chartViewport.ScrollableHeight -le 0) { throw 'Multiple charts do not expose a usable scrolling extent' }
    $wheel = [Windows.Input.MouseWheelEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,-120); $wheel.RoutedEvent = [Windows.UIElement]::PreviewMouseWheelEvent; $chartWidgets[0].RaiseEvent($wheel)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if(!$wheel.Handled -or $chartViewport.VerticalOffset -le 0) { throw 'Chart wheel scrolling did not reach later charts' }
    $savedChartScroll = $chartViewport.VerticalOffset
    $chartWidgets[0].Settings.ChartTitle = 'Redraw with scroll'
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if([Math]::Abs($chartViewport.VerticalOffset-$savedChartScroll) -gt 1) { throw 'Chart redraw reset the scroll position' }
    $chartWidgets[0].ClearValue([Windows.FrameworkElement]::HeightProperty)
    $chartGrid = [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartGrid')
    $childChart = $chartGrid.Children[1]
    if(![object]::ReferenceEquals($childChart.Settings,$extraChart) -or $childChart.Settings.Metric -ne [LandingPage.Settings.StatisticsMetric]::GameCount) { throw 'Additional chart shares primary settings' }
    Wait-Task ($chartWidgets[0].OnViewHiddenAsync())
    if($childChart.GetType().GetField('source',$flags).GetValue($childChart)) { throw 'Hidden parent chart retained an additional-chart subscription' }
    Wait-Task ($chartWidgets[0].OnViewShownAsync())
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $resumedGrid = [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartGrid')
    if(![object]::ReferenceEquals($resumedGrid.Children[1],$childChart) -or !$childChart.GetType().GetField('source',$flags).GetValue($childChart)) { throw 'Additional chart did not resume with its cached control' }
    $chartEditor.GetType().GetMethod('DuplicateChart_Click',$flags).Invoke($chartEditor,@($null,[Windows.RoutedEventArgs]::new()))
    if($chartWidgets[0].Settings.ExtraCharts.Count -ne 2 -or [object]::ReferenceEquals($chartWidgets[0].Settings.ExtraCharts[0],$chartWidgets[0].Settings.ExtraCharts[1])) { throw 'Duplicate chart did not create independent settings' }
    $chartEditor.GetType().GetMethod('RemoveChart_Click',$flags).Invoke($chartEditor,@($null,[Windows.RoutedEventArgs]::new()))
    $chartEditor.FindName('ExtraChartList').SelectedItem = $extraChart
    $chartEditor.GetType().GetMethod('RemoveChart_Click',$flags).Invoke($chartEditor,@($null,[Windows.RoutedEventArgs]::new()))
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if(!$childChart.GetType().GetField('closed',$flags).GetValue($childChart)) { throw 'Removing an additional chart left subscriptions alive' }
    $primaryOptions = $chartWidgets[0].Settings; $primaryOptions.ChartTitle = 'First item'; $primaryOptions.ChartColumns = 2
    $chartEditor.GetType().GetMethod('AddChart_Click',$flags).Invoke($chartEditor,@($null,[Windows.RoutedEventArgs]::new()))
    $promoted = $primaryOptions.ExtraCharts[0]; $promoted.ChartTitle = 'Second item'
    $chartEditor.FindName('ExtraChartList').SelectedItem = $promoted
    $chartEditor.GetType().GetMethod('MoveChartUp_Click',$flags).Invoke($chartEditor,@($null,[Windows.RoutedEventArgs]::new()))
    if($primaryOptions.ChartTitle -ne 'Second item' -or $primaryOptions.ExtraCharts[0].ChartTitle -ne 'First item' -or $primaryOptions.ChartColumns -ne 2) { throw 'Charts could not reorder across the first position' }
    $chartEditor.FindName('ExtraChartList').SelectedItem = $primaryOptions
    $chartEditor.GetType().GetMethod('RemoveChart_Click',$flags).Invoke($chartEditor,@($null,[Windows.RoutedEventArgs]::new()))
    if($primaryOptions.ChartTitle -ne 'First item' -or $primaryOptions.ExtraCharts.Count -ne 0 -or $chartEditor.FindName('RemoveChartButton').IsEnabled) { throw 'Removing the first chart did not preserve the remaining chart or the one-chart minimum' }
    $chartWidgets[0].Settings.ChartColumns = 1
    $chartWidgets[0].Settings.ChartTitle = 'Independent graph'
    if($chartWidgets[1].Settings.ChartTitle -eq 'Independent graph') { throw 'Chart widgets share settings' }
    $chartWidgets[0].Settings.Breakdown = [LandingPage.Settings.StatisticsBreakdown]::Installation
    foreach($placement in [enum]::GetValues([LandingPage.Settings.StatisticsLegendPosition])) {
        $chartWidgets[0].Settings.LegendPosition = $placement
        foreach($alignment in @([Windows.HorizontalAlignment]::Left,[Windows.HorizontalAlignment]::Center,[Windows.HorizontalAlignment]::Right)) {
            $chartWidgets[0].Settings.LegendAlignment = $alignment
            $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
            $legend = [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartLegend')
            $expectedDock = [Enum]::Parse([Windows.Controls.Dock],$placement.ToString())
            if(!$legend -or [Windows.Controls.DockPanel]::GetDock($(if($legend.Parent -is [Windows.Controls.ScrollViewer]) { $legend.Parent } else { $legend })) -ne $expectedDock -or $legend.HorizontalAlignment -ne $alignment) { throw 'Legend placement or alignment ignored saved settings' }
        }
    }
    $chartWidgets[0].Settings.LabelFontFamily = 'Consolas'; $chartWidgets[0].Settings.LabelBold = $true; $chartWidgets[0].Settings.LabelItalic = $true; $chartWidgets[0].Settings.LabelUnderline = $true
    $chartWidgets[0].Settings.CustomLabelColor = $true; $chartWidgets[0].Settings.LabelColor = '#FEDCBA'
    foreach($style in [enum]::GetValues([LandingPage.Settings.StatisticsChartStyle])) {
        $chartWidgets[0].Settings.ChartStyle = $style
        foreach($position in @([LandingPage.Settings.StatisticsLabelPosition]::BesideSegments,[LandingPage.Settings.StatisticsLabelPosition]::AboveBars)) {
            $chartWidgets[0].Settings.LabelPosition = $position
            $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
            if([ToolbarTestVisuals]::Named($chartWidgets[0],'ChartLegend')) { throw 'Segment labels were rendered as a grouped legend' }
            $labelText = @([ToolbarTestVisuals]::Text($chartWidgets[0]) | Where-Object { $_.Text -match ' h' })
            if($labelText.Count -lt 1 -or $labelText[0].FontSize -ne $chartWidgets[0].Settings.LabelSize) { throw 'Direct chart labels are missing or their text size is scaled' }
            if($labelText[0].FontFamily.Source -ne 'Consolas' -or $labelText[0].FontWeight -ne [Windows.FontWeights]::Bold -or $labelText[0].FontStyle -ne [Windows.FontStyles]::Italic -or $labelText[0].TextDecorations.Count -eq 0 -or $labelText[0].Foreground.Color.ToString() -ne '#FFFEDCBA') { throw 'Label typography/custom color did not apply to every chart mode' }

            if($style -eq [LandingPage.Settings.StatisticsChartStyle]::Pie -or $style -eq [LandingPage.Settings.StatisticsChartStyle]::Doughnut) {
                if($position -eq [LandingPage.Settings.StatisticsLabelPosition]::AboveBars) {
                    if(![ToolbarTestVisuals]::Named($chartWidgets[0],'ChartSliceLabel') -and ![ToolbarTestVisuals]::Named($chartWidgets[0],'ChartOutsideLabel')) { throw 'Circular inside/fallback mode has no labels' }
                } else {
                    if(![ToolbarTestVisuals]::Named($chartWidgets[0],'ChartOutsideLabel') -or [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartSliceLabel')) { throw 'Circular beside mode does not use outside labels' }
                }
            }
            if($style -eq [LandingPage.Settings.StatisticsChartStyle]::Bars) {
                $nativePlot = [ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot')
                if($nativePlot.Chart -isnot [LiveCharts.Wpf.CartesianChart] -or $nativePlot.Chart.AxisY[0].Labels.Count -lt 1) { throw 'Horizontal bars have no real category axis' }
            }

        }
    }
    $chartWidgets[0].Settings.LabelPosition = [LandingPage.Settings.StatisticsLabelPosition]::Legend
    $chartWidgets[0].Settings.LegendPosition = [LandingPage.Settings.StatisticsLegendPosition]::Right
    $chartWidgets[0].Settings.ChartStyle = [LandingPage.Settings.StatisticsChartStyle]::Doughnut
    $window.UpdateLayout(); Wait-ChartRender
    $beforeEditPlot = [ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot'); $beforeEditWidth = $beforeEditPlot.ActualWidth; $beforeEditRenderCount = $chartWidgets[0].GetType().GetField('renderCount',$flags).GetValue($chartWidgets[0])
    $chartNodeModel = $chartNodes[0].DataContext
    $chartNodeModel.EditModeEnabled = $true; $chartWidgets[0].Settings.EditCharts = $true
    $window.UpdateLayout(); Wait-ChartRender
    $editingPlot = [ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot')
    if(![object]::ReferenceEquals($editingPlot,$beforeEditPlot) -or $chartWidgets[0].GetType().GetField('renderCount',$flags).GetValue($chartWidgets[0]) -ne $beforeEditRenderCount) { throw 'Entering chart editing rebuilt the plot instead of toggling its overlay' }
    if([Math]::Abs($editingPlot.ActualWidth-$beforeEditWidth) -gt 1) { throw ('Entering chart editing changed automatic plot width: '+$beforeEditWidth+' -> '+$editingPlot.ActualWidth) }
    $chartWidgets[0].Settings.ChartStyle = [LandingPage.Settings.StatisticsChartStyle]::Bars
    $chartWidgets[0].Settings.LabelPosition = [LandingPage.Settings.StatisticsLabelPosition]::BesideSegments
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $grip = [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartResizeHandle')
    if(!$grip -or $chartNodes[0].FindName('EditPanel').IsHitTestVisible) { throw 'Chart editing did not expose resize handles or release the dashboard overlay' }
    $oldChartHeight = $chartWidgets[0].Settings.ChartHeight
    $beginResize = [Windows.Controls.Primitives.DragStartedEventArgs]::new(0,0); $beginResize.RoutedEvent = [Windows.Controls.Primitives.Thumb]::DragStartedEvent; $grip.RaiseEvent($beginResize)
    $resizeDelta = [Windows.Controls.Primitives.DragDeltaEventArgs]::new(80,40); $resizeDelta.RoutedEvent = [Windows.Controls.Primitives.Thumb]::DragDeltaEvent; $grip.RaiseEvent($resizeDelta)
    $endResize = [Windows.Controls.Primitives.DragCompletedEventArgs]::new(80,40,$false); $endResize.RoutedEvent = [Windows.Controls.Primitives.Thumb]::DragCompletedEvent; $grip.RaiseEvent($endResize)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if($chartWidgets[0].Settings.ChartHeight -le $oldChartHeight -or $chartWidgets[0].Settings.ChartWidth -le 0) { throw 'Edit-mode resize grip did not change persisted chart geometry' }
    $movableLabel = [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartBarLabel')
    if($movableLabel.Cursor -ne [Windows.Input.Cursors]::SizeAll) { throw 'Edit-mode labels do not expose drag interaction' }
    $beforeDragPlot = [ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot'); $beforeDragCount = $chartWidgets[0].GetType().GetField('renderCount',$flags).GetValue($chartWidgets[0])
    $downLabel = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left); $downLabel.RoutedEvent = [Windows.UIElement]::PreviewMouseLeftButtonDownEvent; $movableLabel.RaiseEvent($downLabel)
    # Win32 SetCapture only succeeds for a background window when the cursor is over it or a button is down.
    # Synthetic input has no pressed button, so on a desktop in use the capture (and drag) may end at once.
    $labelCaptured = [object]::ReferenceEquals([Windows.Input.Mouse]::Captured,$movableLabel)
    if(!$labelCaptured) { 'NOTE: mouse capture unavailable for synthetic input; mid-drag deferral checks skipped.' }
    $upLabel = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left); $upLabel.RoutedEvent = [Windows.UIElement]::PreviewMouseLeftButtonUpEvent; $movableLabel.RaiseEvent($upLabel)
    $labelReleased = ($upLabel.Handled -or !$labelCaptured) -and ![object]::ReferenceEquals([Windows.Input.Mouse]::Captured,$movableLabel)
    if(!$downLabel.Handled -or !$labelReleased -or $chartWidgets[0].GetType().GetField('manipulating',$flags).GetValue($chartWidgets[0])) { throw 'Label drag start/end did not capture and release cleanly' }
    $window.UpdateLayout(); Wait-ChartRender
    if(![object]::ReferenceEquals($beforeDragPlot,[ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot')) -or $chartWidgets[0].GetType().GetField('renderCount',$flags).GetValue($chartWidgets[0]) -ne $beforeDragCount) { throw 'Completing a label drag rebuilt the chart' }
    $originalOffset = $chartWidgets[0].Settings.LabelOffsetX
    $beforeLabelPosition = $movableLabel.TranslatePoint([Windows.Point]::new(0,0),$chartWidgets[0]).X
    $chartWidgets[0].Settings.LabelOffsetX += 17
    $window.UpdateLayout(); Wait-ChartRender
    if(![object]::ReferenceEquals($beforeDragPlot,[ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot')) -or [Math]::Abs($movableLabel.TranslatePoint([Windows.Point]::new(0,0),$chartWidgets[0]).X-$beforeLabelPosition-17) -gt 1) { throw 'Offset edits did not move existing labels without rebuilding the graph' }
    $chartWidgets[0].Settings.LabelOffsetX = $originalOffset
    $pendingCount = $chartWidgets[0].GetType().GetField('renderCount',$flags).GetValue($chartWidgets[0])
    $deferredDown = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left); $deferredDown.RoutedEvent = [Windows.UIElement]::PreviewMouseLeftButtonDownEvent; $movableLabel.RaiseEvent($deferredDown)
    $chartWidgets[0].Settings.ChartTitle = 'Deferred visual change'
    $window.UpdateLayout(); Wait-ChartRender
    if($labelCaptured -and $chartWidgets[0].GetType().GetField('renderCount',$flags).GetValue($chartWidgets[0]) -ne $pendingCount) { throw 'Visual setting rebuilt the chart in the middle of a label drag' }
    $deferredUp = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left); $deferredUp.RoutedEvent = [Windows.UIElement]::PreviewMouseLeftButtonUpEvent; $movableLabel.RaiseEvent($deferredUp)
    $window.UpdateLayout(); Wait-ChartRender
    if($chartWidgets[0].GetType().GetField('renderCount',$flags).GetValue($chartWidgets[0]) -ne $pendingCount+1 -or !(@([ToolbarTestVisuals]::Text($chartWidgets[0]) | ForEach-Object Text) -contains 'Deferred visual change')) { throw 'Genuine deferred visual change was dropped or rebuilt more than once' }
    $beforeDragPlot = [ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot')
    $chartNodeModel.EditModeEnabled = $false
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if(([ToolbarTestVisuals]::Named($chartWidgets[0],'ChartResizeHandle')).Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Chart resize handles remain visible outside dashboard Edit Mode' }
    if(![object]::ReferenceEquals($beforeDragPlot,[ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot'))) { throw 'Leaving chart editing rebuilt the chart' }
    $editAppearance = [LandingPage.Views.PanelAppearanceView]::new(); $editAppearance.DataContext = $chartNodeModel
    [void]$layout.Children.Add($editAppearance); $window.UpdateLayout(); Wait-ChartRender; $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if($editAppearance.FindName('GeneralAppearanceSection').IsExpanded) { throw 'Appearance settings did not collapse when specialized settings were present' }
    $plainNode = [LandingPage.Models.Layout.GridNode]::new(); $plainNode.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $plainNode.ViewProperties.ViewId = 'ExternalWidget'
    $editAppearance.DataContext = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($plainNode)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if(!$editAppearance.FindName('GeneralAppearanceSection').IsExpanded) { throw 'Appearance-only editor should remain expanded' }
    [void]$layout.Children.Remove($editAppearance)
    $selectedChartEditor = $chartEditor.FindName('ExtraChartEditor').Content
    if($selectedChartEditor.FindName('RotationSlider') -isnot [Windows.Controls.Slider]) { throw 'Rotation is not exposed as a slider' }
    'PASS: conditional Appearance expansion, rotation slider, edit-mode resizing, draggable labels, overlay input and cleanup outside Edit Mode.'
    $chartWidgets[0].Settings.ChartStyle = [LandingPage.Settings.StatisticsChartStyle]::Pie
    $chartWidgets[0].Settings.LabelPosition = [LandingPage.Settings.StatisticsLabelPosition]::Legend
    $chartWidgets[0].Settings.Breakdown = [LandingPage.Settings.StatisticsBreakdown]::Games; $chartWidgets[0].Settings.Metric = [LandingPage.Settings.StatisticsMetric]::GameCount
    $chartWidgets[0].Settings.ChartRotation = 0; $chartWidgets[0].Settings.ChartWidth = 500; $chartWidgets[0].Settings.ChartHeight = 260; $chartWidgets[0].Settings.ChartScale = 1
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $sizedPlot = [ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot'); $originalDiameter = $sizedPlot.Width; $originalGeometry = $sizedPlot.Chart.StartingRotationAngle
    $chartWidgets[0].Settings.ChartScale = 0.5; $chartWidgets[0].Settings.ChartRotation = 90
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $rotatedPlot = [ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot')
    if([Math]::Abs($rotatedPlot.Width - $originalDiameter/2) -gt 1 -or $rotatedPlot.Chart.StartingRotationAngle -eq $originalGeometry) { throw 'Chart scaling or circular rotation did not alter geometry' }
    $chartWidgets[0].Settings.ChartStyle = [LandingPage.Settings.StatisticsChartStyle]::Pie; $chartWidgets[0].Settings.ChartWidth = 800; $chartWidgets[0].Settings.ChartHeight = 600; $chartWidgets[0].Settings.LabelSize = 12
    $chartWidgets[0].Settings.LabelPosition = [LandingPage.Settings.StatisticsLabelPosition]::AboveBars
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if(![ToolbarTestVisuals]::Named($chartWidgets[0],'ChartSliceLabel')) { throw 'Inside mode never places labels inside even when the pie is large enough' }
    foreach($labelOffset in $chartWidgets[0].Settings.LabelOffsets) { $labelOffset.X = 600 }
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if(![ToolbarTestVisuals]::Named($chartWidgets[0],'ChartConnector')) { throw 'An inside label moved outside its pie slice has no connector' }
    foreach($labelOffset in $chartWidgets[0].Settings.LabelOffsets) { $labelOffset.X = 0 }
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $chartWidgets[0].Settings.LabelPosition = [LandingPage.Settings.StatisticsLabelPosition]::BesideSegments
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if([ToolbarTestVisuals]::Named($chartWidgets[0],'ChartSliceLabel') -or ![ToolbarTestVisuals]::Named($chartWidgets[0],'ChartOutsideLabel')) { throw 'Beside and inside modes produce the same label placement' }
    $chartWidgets[0].Settings.ChartStyle = [LandingPage.Settings.StatisticsChartStyle]::VerticalBars
    $chartWidgets[0].Settings.LabelPosition = [LandingPage.Settings.StatisticsLabelPosition]::AboveBars
    $chartWidgets[0].Settings.ChartScale = 1
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if(([ToolbarTestVisuals]::Named($chartWidgets[0],'StatisticsPlot')).Chart.Series[0] -isnot [LiveCharts.Wpf.ColumnSeries]) { throw 'Vertical bars did not render' }
    $chartWidgets[0].Settings.ChartStyle = [LandingPage.Settings.StatisticsChartStyle]::Doughnut
    $chartWidgets[0].Settings.ChartHeight = 60; $chartWidgets[0].Settings.ChartWidth = 400; $chartWidgets[0].Settings.LabelSize = 60; $chartWidgets[0].Settings.HoleSize = 0.85
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if([ToolbarTestVisuals]::Named($chartWidgets[0],'ChartSliceLabel') -or ![ToolbarTestVisuals]::Named($chartWidgets[0],'ChartOutsideLabel')) { throw 'Oversized inside labels did not fall back outside the doughnut' }
    $chartWidgets[0].Settings.LabelPosition = [LandingPage.Settings.StatisticsLabelPosition]::BesideSegments
    $chartWidgets[0].Settings.LabelSize = 14; $chartWidgets[0].Settings.LabelOffsetX = 0; $chartWidgets[0].Settings.LabelOffsetY = 0
    $chartWidgets[0].Settings.ShowConnectorLines = $true; $chartWidgets[0].Settings.ConnectorStyle = [LandingPage.Settings.ChartConnectorStyle]::Straight
    $chartWidgets[0].Settings.CustomConnectorColor = $true; $chartWidgets[0].Settings.ConnectorColor = '#12AB34'; $chartWidgets[0].Settings.ConnectorThickness = 3
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $outsideBefore = [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartOutsideLabel'); $originalLabelX = $outsideBefore.TranslatePoint([Windows.Point]::new(0,0),$chartWidgets[0]).X; $originalLabelY = $outsideBefore.TranslatePoint([Windows.Point]::new(0,0),$chartWidgets[0]).Y
    $connector = [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartConnector')
    if(!$connector -or $connector.Points.Count -ne 2 -or $connector.Stroke.Color.ToString() -ne '#FF12AB34' -or $connector.StrokeThickness -ne 3) { throw 'Connector style/color/thickness controls failed' }
    $chartWidgets[0].Settings.LabelOffsetX = 25; $chartWidgets[0].Settings.LabelOffsetY = 15
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    $outsideAfter = [ToolbarTestVisuals]::Named($chartWidgets[0],'ChartOutsideLabel')
    if($outsideAfter.TranslatePoint([Windows.Point]::new(0,0),$chartWidgets[0]).X -eq $originalLabelX -or $outsideAfter.TranslatePoint([Windows.Point]::new(0,0),$chartWidgets[0]).Y -eq $originalLabelY) { throw 'Label offset controls did not move the label' }
    $chartWidgets[0].Settings.ShowConnectorLines = $false
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); Wait-ChartRender
    if([ToolbarTestVisuals]::Named($chartWidgets[0],'ChartConnector')) { throw 'Connector line visibility was ignored' }
    'PASS: vertical bars, explicit chart sizing/scale, circular rotation, inside-label fit/fallback, label offsets and connector customization.'
    for($chartIndex = 0; $chartIndex -lt 2; $chartIndex++) { $plugin.OnViewRemoved('LibraryChart',$chartIds[$chartIndex]); [void]$layout.Children.Remove($chartNodes[$chartIndex]); if(!$chartWidgets[$chartIndex].GetType().GetField('closed',$flags).GetValue($chartWidgets[$chartIndex])) { throw 'Chart removal did not clean up its subscriptions' } }
    'PASS: independent chart registrations/binding, playtime graphs, all legend positions/alignment, direct labels across chart styles and chart removal cleanup.'
    $registryCharts = $plugin.AllAvailableViews['StartPage']
    if(@($registryCharts | Where-Object ViewId -eq 'LibraryChart').Count -eq 0) { $chartArgs = [LandingPage.Models.Layout.StartPageViewArgs]::new(); $chartArgs.ViewId = 'LibraryChart'; $chartArgs.Name = 'Library chart'; $chartArgs.PluginId = $plugin.Id; $chartArgs.AllowMultipleInstances = $true; $registryCharts.Add($chartArgs) }
    $boundedChartId = [guid]::NewGuid(); $boundedChartProps = [LandingPage.Models.Layout.ViewProperties]::new(); $boundedChartProps.ViewId = 'LibraryChart'; $boundedChartProps.PluginId = $plugin.Id; $boundedChartProps.InstanceId = $boundedChartId
    $boundedChartProps.LibraryStatisticsSettings.ChartHeight = 300
    for($extraIndex = 0; $extraIndex -lt 4; $extraIndex++) { $extraOptions = [LandingPage.Settings.LibraryStatisticsSettings]::CreateChartSettings(); $extraOptions.ChartHeight = 300; $boundedChartProps.LibraryStatisticsSettings.ExtraCharts.Add($extraOptions) }
    $boundedWidgetCreated = $plugin.GetStartPageView('LibraryChart',$boundedChartId)
    [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($boundedChartProps,$boundedWidgetCreated)
    $boundedLeaf = [LandingPage.Models.Layout.GridNode]::new(); $boundedLeaf.ViewProperties = $boundedChartProps
    $boundedColumns = [LandingPage.Models.Layout.GridNode]::new(); $boundedColumns.Orientation = [Windows.Controls.Orientation]::Horizontal; $boundedColumns.Children.Add([LandingPage.Models.Layout.GridNode]::new()); $boundedColumns.Children.Add($boundedLeaf)
    $boundedRoot = [LandingPage.Models.Layout.GridNode]::new(); $boundedRoot.Orientation = [Windows.Controls.Orientation]::Vertical
    $boundedTop = [LandingPage.Models.Layout.GridNode]::new(); $boundedTop.Size = [Windows.GridLength]::new(40); $boundedRoot.Children.Add($boundedTop); $boundedRoot.Children.Add($boundedColumns)
    $boundedModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($boundedRoot); Wait-Task ($boundedModel.InititalizeGrid())
    $boundedView = [LandingPage.Views.GridNodeView]::new(); $boundedView.DataContext = $boundedModel; $boundedView.Height = 320; $boundedView.Width = 700
    $boundedPageScroll = [Windows.Controls.ScrollViewer]::new(); $boundedPageScroll.Height = 320; $boundedPageScroll.Width = 700; $boundedPageScroll.Content = $boundedView
    [void]$scene.Children.Add($boundedPageScroll)
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    $boundedWidget = [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).GetValue($boundedChartProps)
    $boundedViewport = $boundedWidget.GetType().GetField('viewport',$flags).GetValue($boundedWidget)
    if($boundedViewport.ViewportHeight -gt 300 -or $boundedViewport.ScrollableHeight -lt 500 -or $boundedViewport.ComputedVerticalScrollBarVisibility -ne [Windows.Visibility]::Visible) { throw ('Nested dashboard chart has no bounded widget scrollbar: viewport=' + $boundedViewport.ViewportHeight + ', extent=' + $boundedViewport.ExtentHeight) }
    $boundedViewport.ScrollToBottom(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    if($boundedViewport.VerticalOffset -le 0 -or [Math]::Abs($boundedViewport.VerticalOffset-$boundedViewport.ScrollableHeight) -gt 1) { throw 'Nested dashboard widget cannot scroll to its final chart' }

    $boundedCharts = [ToolbarTestVisuals]::Named($boundedWidget,'ChartGrid')
    if($boundedCharts.Children.Count -ne 5 -or @($boundedCharts.Children | Where-Object ActualHeight -lt 300).Count -gt 0) { throw 'Additional charts collapsed instead of contributing their full height' }
    $lastChart = $boundedCharts.Children[$boundedCharts.Children.Count-1]
    $lastBottom = $lastChart.TranslatePoint([Windows.Point]::new(0,$lastChart.ActualHeight),$boundedViewport).Y
    if($lastBottom -gt $boundedViewport.ViewportHeight+2) { throw 'Final chart is below the viewport even at its maximum scroll offset' }
    foreach($additionalView in $boundedWidget.GetType().GetField('extraViews',$flags).GetValue($boundedWidget).Values) {
        if($additionalView.Content -is [Windows.Controls.ScrollViewer]) { throw 'Additional charts still create nested widget scrollbars' }
    }
    # A fixed dashboard can be taller than the window: both scroll ranges must work.
    $beforeWindowResizePlot = [ToolbarTestVisuals]::Named($boundedWidget,'StatisticsPlot')
    $beforeWindowResizeCount = $boundedWidget.GetType().GetField('renderCount',$flags).GetValue($boundedWidget)
    $childResizeCounts = @($boundedWidget.GetType().GetField('extraViews',$flags).GetValue($boundedWidget).Values | ForEach-Object { $_.GetType().GetField('renderCount',$flags).GetValue($_) })
    $boundedView.Width = 620; $window.UpdateLayout(); Wait-ChartRender
    if(![object]::ReferenceEquals($beforeWindowResizePlot,[ToolbarTestVisuals]::Named($boundedWidget,'StatisticsPlot')) -or $boundedWidget.GetType().GetField('renderCount',$flags).GetValue($boundedWidget) -ne $beforeWindowResizeCount) { throw 'Window resize rebuilt the root chart' }
    $afterChildResizeCounts = @($boundedWidget.GetType().GetField('extraViews',$flags).GetValue($boundedWidget).Values | ForEach-Object { $_.GetType().GetField('renderCount',$flags).GetValue($_) })
    if(($childResizeCounts -join ',') -ne ($afterChildResizeCounts -join ',')) { throw 'Window resize rebuilt additional charts' }
    $boundedView.Width = 700; $window.UpdateLayout(); Wait-ChartRender
    $boundedView.Height = 1080; $window.UpdateLayout(); Wait-ChartRender
    $boundedViewport.ScrollToTop(); $boundedPageScroll.ScrollToTop(); $window.UpdateLayout()
    $firstWheel = [Windows.Input.MouseWheelEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,-120); $firstWheel.RoutedEvent = [Windows.UIElement]::PreviewMouseWheelEvent; $boundedWidget.RaiseEvent($firstWheel)
    $window.UpdateLayout()
    if(!$firstWheel.Handled -or $boundedViewport.VerticalOffset -le 0 -or $boundedPageScroll.VerticalOffset -ne 0) { throw 'Dashboard stole the wheel before the chart widget scrolled' }
    $boundedViewport.ScrollToBottom(); $window.UpdateLayout()
    $endWheel = [Windows.Input.MouseWheelEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,-120); $endWheel.RoutedEvent = [Windows.UIElement]::PreviewMouseWheelEvent; $boundedWidget.RaiseEvent($endWheel)
    $window.UpdateLayout()
    if(!$endWheel.Handled -or $boundedPageScroll.VerticalOffset -le 0) { throw 'Widget trapped the wheel at its bottom instead of scrolling the dashboard' }
    $boundedViewport.ScrollToTop(); $boundedPageScroll.ScrollToBottom(); $window.UpdateLayout(); $outerBefore = $boundedPageScroll.VerticalOffset
    $topWheel = [Windows.Input.MouseWheelEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,120); $topWheel.RoutedEvent = [Windows.UIElement]::PreviewMouseWheelEvent; $boundedWidget.RaiseEvent($topWheel)
    $window.UpdateLayout()
    if(!$topWheel.Handled -or $boundedPageScroll.VerticalOffset -ge $outerBefore) { throw 'Widget trapped the wheel at its top instead of scrolling the dashboard' }
    $leafView = @($boundedModel.View.Children | Where-Object { $_ -is [LandingPage.Views.GridNodeView] })[1]
    $leafModel = @($leafView.DataContext.View.Children | Where-Object { $_ -is [LandingPage.Views.GridNodeView] })[1].DataContext
    $leafModel.SplitVerticallyCommand.Execute($null); $window.UpdateLayout(); Wait-ChartRender
    if(![object]::ReferenceEquals($boundedWidget.Settings,$boundedChartProps.LibraryStatisticsSettings) -or $boundedWidget.Settings.ExtraCharts.Count -ne 4) { throw ('Vertical split lost the configured chart collection: current=' + $boundedWidget.Settings.ExtraCharts.Count + ', original=' + $boundedChartProps.LibraryStatisticsSettings.ExtraCharts.Count + ', loaded=' + $boundedWidget.IsLoaded + ', closed=' + $boundedWidget.GetType().GetField('closed',$flags).GetValue($boundedWidget) + ', parent=' + $boundedWidget.Parent) }
    $splitCharts = [ToolbarTestVisuals]::Named($boundedWidget,'ChartGrid')
    if($splitCharts.Children.Count -ne 5 -or @($splitCharts.Children | Where-Object ActualHeight -lt 300).Count -gt 0) { throw 'Vertical split left only one visible chart' }
    $sixth = [LandingPage.Settings.LibraryStatisticsSettings]::CreateChartSettings(); $boundedWidget.Settings.ExtraCharts.Add($sixth); $boundedWidget.Settings.ChartColumns = 2
    $boundedWidget.Settings.ChartHeight = 220; foreach($item in $boundedWidget.Settings.ExtraCharts) { $item.ChartHeight = 220 }
    $window.UpdateLayout(); Wait-ChartRender; $window.UpdateLayout(); Wait-ChartRender
    $sixCharts = [ToolbarTestVisuals]::Named($boundedWidget,'ChartGrid')
    if($sixCharts.Children.Count -ne 6 -or $sixCharts.ColumnDefinitions.Count -ne 2 -or @($sixCharts.Children | Where-Object ActualHeight -lt 220).Count -gt 0) { throw 'Six-chart/two-column split layout collapsed or hid additional charts' }
    Set-Setting $settingsModel.Settings 'EnableDashboardDiagnostics' $true
    Wait-Task ($boundedWidget.GetType().GetMethod('LogChartLayout',$flags).Invoke($boundedWidget,@()))
    $deadline = [datetime]::UtcNow.AddSeconds(3)
    while(![IO.File]::ReadAllText($diagnosticFile).Contains('configuredCharts=6; renderedCharts=6; columns=2') -and [datetime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 10 }
    if(![IO.File]::ReadAllText($diagnosticFile).Contains('configuredCharts=6; renderedCharts=6; columns=2')) { throw ('Chart layout diagnostics did not record configured/rendered chart counts and layout: visible=' + $boundedWidget.GetType().GetField('visible',$flags).GetValue($boundedWidget) + '; last=' + (([IO.File]::ReadAllText($diagnosticFile) -split [Environment]::NewLine | Where-Object { $_.Contains('Library chart layout:') }) -join '|')) }
    Set-Setting $settingsModel.Settings 'EnableDashboardDiagnostics' $false
    'PASS: all charts retain full measured height, final chart is visible at the bottom, wheel transfers at both boundaries, and vertical split preserves every chart.'
    $plugin.OnViewRemoved('LibraryChart',$boundedChartId); [void]$scene.Children.Remove($boundedPageScroll)
    'PASS: nested vertical/horizontal dashboard panels constrain chart viewport height and expose an independent scrollbar reaching the final chart.'
    $statsWidget.Settings.UseCounterGrid = $true; $statsWidget.Settings.CounterRows = 3; $statsWidget.Settings.CounterColumns = 3
    $totalPlacement = @($statsWidget.Settings.CounterPlacements | Where-Object Counter -eq ([LandingPage.Settings.StatisticsCounter]::Total))[0]
    $totalPlacement.Row = 2; $totalPlacement.Column = 2; $totalPlacement.ColumnSpan = 2
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    $counterGrid = [ToolbarTestVisuals]::Named($statsWidget,'CounterGrid')
    $totalTile = @($counterGrid.Children | Where-Object Tag -eq ([LandingPage.Settings.StatisticsCounter]::Total))[0]
    if(!$totalTile -or [Windows.Controls.Grid]::GetRow($totalTile) -ne 1 -or [Windows.Controls.Grid]::GetColumn($totalTile) -ne 1 -or [Windows.Controls.Grid]::GetColumnSpan($totalTile) -ne 2) { throw 'Counter grid row/column/span settings were ignored' }
    $totalPlacement.Row = 3
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    $counterGrid = [ToolbarTestVisuals]::Named($statsWidget,'CounterGrid'); $totalTile = @($counterGrid.Children | Where-Object Tag -eq ([LandingPage.Settings.StatisticsCounter]::Total))[0]
    if([Windows.Controls.Grid]::GetRow($totalTile) -ne 2) { throw 'Editing a nested counter position did not refresh the grid' }
    $statsWidget.Settings.ShowTodayPlaytime = $true; $statsWidget.Settings.ShowWeekPlaytime = $true; $statsWidget.Settings.ShowMonthPlaytime = $true; $statsWidget.Settings.ShowYearPlaytime = $true
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    if(!(@([ToolbarTestVisuals]::Text($statsWidget) | ForEach-Object Text) -match 'requires GameActivity session history')) { throw 'Missing period history was silently reported as zero' }
    $historyFixture = [LandingPage.ViewModels.GameActivity.GameActivityViewModel].GetConstructors()[0].Invoke([object[]]@($null,$api,$settingsModel))
    Set-Field $historyFixture 'isInitialized' $true
    $activityFixture = [LandingPage.Models.GameActivity.Activity]::new(); $activityFixture.Id = $extraStatsGame.Id
    $sessionFixture = [LandingPage.Models.GameActivity.Session]::new(); $sessionFixture.DateSession = [datetime]::Now.Date.ToUniversalTime(); $sessionFixture.ElapsedSeconds = [uint64][Math]::Min(3600,[Math]::Floor([datetime]::Now.TimeOfDay.TotalSeconds))
    $activityFixture.Items.Add($sessionFixture); $historyFixture.Activities.Add($activityFixture)
    $historyLazy = [Lazy[LandingPage.ViewModels.GameActivity.GameActivityViewModel]]::new([Func[LandingPage.ViewModels.GameActivity.GameActivityViewModel]]{ return $historyFixture })
    Set-Field $plugin 'gameActivityViewModel' $historyLazy
    $statsWidget.OnDayChangedAsync([datetime]::Now).GetAwaiter().GetResult()
    $historyDeadline = [datetime]::UtcNow.AddSeconds(5)
    while(!$statsWidget.GetType().GetField('periodTotals',$flags).GetValue($statsWidget) -and [datetime]::UtcNow -lt $historyDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 5 }
    $actualPeriods = $statsWidget.GetType().GetField('periodTotals',$flags).GetValue($statsWidget)
    if(!$actualPeriods -or $actualPeriods.TodaySeconds -ne $sessionFixture.ElapsedSeconds -or $actualPeriods.MonthSeconds -ne $sessionFixture.ElapsedSeconds) { throw 'Real statistics widget failed to load shared GameActivity session periods' }
    Wait-Task ($statsWidget.OnViewHiddenAsync())
    if($statsWidget.GetType().GetField('periodTimer',$flags).GetValue($statsWidget).IsEnabled -or $statsWidget.GetType().GetField('observedActivities',$flags).GetValue($statsWidget)) { throw 'Hidden statistics widget retained its period timer or history listener' }
    $historyFixture.GetType().GetField('timer',$flags).GetValue($historyFixture).Stop()
    'PASS: missing-history explanation, asynchronous shared GameActivity periods, all four counters and hidden timer/listener cleanup.'
    $window.Resources['FontSizeLargest'] = [double]47; $window.Resources['FontSizeLarge'] = [double]27
    $statsWidget.Settings.ValueFontFamily = 'Consolas'; $statsWidget.Settings.ValueBold = $true; $statsWidget.Settings.ValueItalic = $true; $statsWidget.Settings.ValueUnderline = $true; $statsWidget.Settings.ValueStrikeThrough = $true
    $statsWidget.Settings.ValueSizePreset = [LandingPage.Settings.StatisticsTextSize]::ThemeLargest; $statsWidget.Settings.LabelSizePreset = [LandingPage.Settings.StatisticsTextSize]::ThemeLarge
    $statsWidget.Settings.ValueColorMode = [LandingPage.Settings.StatisticsValueColor]::Custom; $statsWidget.Settings.ValueColor = '#1177AA'; $statsWidget.Settings.ValueOpacity = .7
    $statsWidget.Settings.LabelStrikeThrough = $true; $statsWidget.Settings.LabelOpacity = .8; $statsWidget.Settings.LabelLineHeight = 24; $statsWidget.Settings.WrapCounterText = $false; $statsWidget.Settings.CounterTextTrimming = [Windows.TextTrimming]::CharacterEllipsis
    $statsWidget.Settings.InheritTextRendering = $false; $statsWidget.Settings.TextFormatting = [Windows.Media.TextFormattingMode]::Display; $statsWidget.Settings.TextRendering = [Windows.Media.TextRenderingMode]::Grayscale
    Wait-Task ($statsWidget.OnViewShownAsync()); $window.UpdateLayout(); Wait-ChartRender
    $styledValue = [ToolbarTestVisuals]::Named($statsWidget,'StatisticValue'); $styledLabel = [ToolbarTestVisuals]::Named($statsWidget,'StatisticLabel')
    if($styledValue.FontFamily.Source -ne 'Consolas' -or $styledValue.FontSize -ne 47 -or $styledValue.FontWeight -ne [Windows.FontWeights]::Bold -or $styledValue.FontStyle -ne [Windows.FontStyles]::Italic -or $styledValue.TextDecorations.Count -ne 2 -or $styledValue.Foreground.Color.ToString() -ne '#FF1177AA' -or $styledValue.Opacity -ne .7) { throw 'Statistic value styling did not reach rendered text' }
    if($styledLabel.FontSize -ne 27 -or $styledLabel.Opacity -ne .8 -or $styledLabel.LineHeight -ne 24 -or $styledLabel.TextWrapping -ne [Windows.TextWrapping]::NoWrap -or $styledLabel.TextTrimming -ne [Windows.TextTrimming]::CharacterEllipsis -or [Windows.Media.TextOptions]::GetTextRenderingMode($styledLabel) -ne [Windows.Media.TextRenderingMode]::Grayscale) { throw 'Statistic label styling or Playnite rendering override failed' }
    $window.Resources['FontSizeLargest'] = [double]49; $window.UpdateLayout()
    if($styledValue.FontSize -ne 49) { throw 'Statistic theme-size preset does not follow changing theme resources' }
    $statsWidget.Settings.CustomLabelColor = $true; $statsWidget.Settings.LabelColor = '#ABCDEF'; $statsWidget.Settings.ValueColorMode = [LandingPage.Settings.StatisticsValueColor]::MatchLabels
    $window.UpdateLayout(); Wait-ChartRender
    if(([ToolbarTestVisuals]::Named($statsWidget,'StatisticValue')).Foreground.Color.ToString() -ne '#FFABCDEF') { throw 'Value color did not follow the label color' }
    'PASS: actual statistic text supports independent value/label styling, live theme sizes, matching colors and text rendering/wrapping controls.'
    $totalColors = @($statsWidget.Settings.CounterPlacements | Where-Object Counter -eq ([LandingPage.Settings.StatisticsCounter]::Total))[0].Colors
    $totalColors.UseGlobalValueColor = $false; $totalColors.ValueColor = '#112233'; $totalColors.UseGlobalLabelColor = $false; $totalColors.LabelColor = '#445566'
    $window.UpdateLayout(); Wait-ChartRender
    $coloredTile = @(([ToolbarTestVisuals]::Named($statsWidget,'CounterGrid')).Children | Where-Object Tag -eq ([LandingPage.Settings.StatisticsCounter]::Total))[0]
    if(([ToolbarTestVisuals]::Named($coloredTile,'StatisticValue')).Foreground.Color.ToString() -ne '#FF112233' -or ([ToolbarTestVisuals]::Named($coloredTile,'StatisticLabel')).Foreground.Color.ToString() -ne '#FF445566') { throw 'Per-statistic value/label overrides did not reach the live widget' }
    $totalColors.UseGlobalValueColor = $true; $window.UpdateLayout(); Wait-ChartRender
    $coloredTile = @(([ToolbarTestVisuals]::Named($statsWidget,'CounterGrid')).Children | Where-Object Tag -eq ([LandingPage.Settings.StatisticsCounter]::Total))[0]
    if(([ToolbarTestVisuals]::Named($coloredTile,'StatisticValue')).Foreground.Color.ToString() -ne '#FF445566') { throw 'Global match-label mode did not follow the local label color' }
    $totalColors.UseGlobalLabelColor = $true; $window.UpdateLayout(); Wait-ChartRender
    $coloredTile = @(([ToolbarTestVisuals]::Named($statsWidget,'CounterGrid')).Children | Where-Object Tag -eq ([LandingPage.Settings.StatisticsCounter]::Total))[0]
    if(([ToolbarTestVisuals]::Named($coloredTile,'StatisticLabel')).Foreground.Color.ToString() -ne '#FFABCDEF' -or ([ToolbarTestVisuals]::Named($coloredTile,'StatisticValue')).Foreground.Color.ToString() -ne '#FFABCDEF') { throw 'Restoring global colors left a stale statistic override' }
    'PASS: independent per-statistic value/label colors, global inheritance and match-label precedence.'

    foreach($option in $statsWidget.Settings.AdditionalCounters) { $option.Enabled = $true }
    foreach($groupOption in $statsWidget.Settings.StatisticGroups) { $groupOption.Enabled = $true; $groupOption.ShowPercentages = $true }
    $window.UpdateLayout(); Wait-ChartRender
    $allStatisticText = @([ToolbarTestVisuals]::Text($statsWidget) | ForEach-Object Text)
    foreach($counterOption in $statsWidget.Settings.AdditionalCounters) { if($allStatisticText -notcontains $counterOption.Label) { throw ('Extended statistic did not render: '+$counterOption.Label) } }
    foreach($groupTitle in @('Games by source','Games by platform','Games by genre','Games by category','Games by tag','Games by feature','Games by developer','Games by publisher','Games by completion status')) { if($allStatisticText -notcontains $groupTitle) { throw ('Metadata breakdown did not render: '+$groupTitle) } }
    if($allStatisticText -contains 'Unavailable' -or $allStatisticText -match 'NaN|Infinity') { throw 'Expanded counters produced unavailable or invalid numbers' }
    if($statsEditor.FindName('AdditionalCounterOptions').Visibility -ne [Windows.Visibility]::Visible -or $selectedChartEditor.FindName('AdditionalCounterOptions').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Additional statistics settings are missing from counters or leaking into charts' }
    $sourceGroup = @($statsWidget.Settings.StatisticGroups | Where-Object Group -eq ([LandingPage.Settings.StatisticGroup]::Sources))[0]
    $sourceGroup.Metric = [LandingPage.Settings.StatisticsMetric]::Playtime
    $window.UpdateLayout(); Wait-ChartRender
    if(@([ToolbarTestVisuals]::Text($statsWidget) | ForEach-Object Text) -notcontains 'Playtime by source') { throw 'Changing breakdown metric did not refresh the live widget' }
    $sourceGroup.SelectedOnly = $true; $sourceGroup.SelectedId = [guid]::NewGuid(); $sourceGroup.Metric = [LandingPage.Settings.StatisticsMetric]::GameCount
    $sourceGroup.ShowIcon = $true; $sourceGroup.ShowName = $false; $sourceGroup.ShowHeading = $false; $sourceGroup.IconSize = 31
    $window.UpdateLayout(); Wait-ChartRender
    $sourceBlock = @(([ToolbarTestVisuals]::Named($statsWidget,'CounterGrid')).Children | Where-Object Tag -eq ([LandingPage.Settings.StatisticsCounter]::SourcesBreakdown))[0]
    $selectedEntries = $sourceBlock.Children[0]
    if($selectedEntries.Children.Count -ne 1) { throw 'Selected-source statistic did not reduce to one counter' }
    $zeroCounter = [ToolbarTestVisuals]::Named($sourceBlock,'StatisticValue'); $sourceCaption = [ToolbarTestVisuals]::Named($sourceBlock,'StatisticLabel'); $sourceBadge = [ToolbarTestVisuals]::Named($sourceBlock,'StatisticSourceBadge')
    if(!$zeroCounter.Text.StartsWith('0') -or !$sourceBadge -or $sourceCaption.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Zero-match source/icon-only display failed' }
    $sourceGroup.ShowName = $true; $window.UpdateLayout(); Wait-ChartRender
    $sourceBlock = @(([ToolbarTestVisuals]::Named($statsWidget,'CounterGrid')).Children | Where-Object Tag -eq ([LandingPage.Settings.StatisticsCounter]::SourcesBreakdown))[0]
    if(([ToolbarTestVisuals]::Named($sourceBlock,'StatisticLabel')).Visibility -ne [Windows.Visibility]::Visible -or ![ToolbarTestVisuals]::Named($sourceBlock,'StatisticSourceBadge')) { throw 'Combined name and icon display failed' }
    $sourceGroup.ShowIcon = $false; $sourceGroup.SelectedId = [guid]::Empty; $window.UpdateLayout(); Wait-ChartRender
    $sourceBlock = @(([ToolbarTestVisuals]::Named($statsWidget,'CounterGrid')).Children | Where-Object Tag -eq ([LandingPage.Settings.StatisticsCounter]::SourcesBreakdown))[0]
    if([ToolbarTestVisuals]::Named($sourceBlock,'StatisticSourceBadge') -or !(@([ToolbarTestVisuals]::Text($sourceBlock) | ForEach-Object Text) -contains 'Playnite / no source')) { throw 'Selected Playnite/no-source text-only counter failed' }
    $platformGroup = @($statsWidget.Settings.StatisticGroups | Where-Object Group -eq ([LandingPage.Settings.StatisticGroup]::Platforms))[0]
    $platformGroup.SelectedOnly = $true; $platformGroup.SelectedId = [guid]::NewGuid(); $platformGroup.ShowIcon = $true; $platformGroup.ShowHeading = $false
    $window.UpdateLayout(); Wait-ChartRender
    $platformBlock = @(([ToolbarTestVisuals]::Named($statsWidget,'CounterGrid')).Children | Where-Object Tag -eq ([LandingPage.Settings.StatisticsCounter]::PlatformsBreakdown))[0]
    if($platformBlock.Children[0].Children.Count -ne 1 -or !([ToolbarTestVisuals]::Named($platformBlock,'StatisticValue')).Text.StartsWith('0')) { throw 'Selected-platform statistic did not preserve an explicit zero count' }
    'PASS: selected source/platform counters, zero matches, icon-only, text-only and combined badge modes.'
    $extraCounterSubscription = $statsWidget.GetType().GetField('statisticOptionSubscriptions',$flags).GetValue($statsWidget)
    if($extraCounterSubscription.Count -lt 48) { throw 'Statistic option changes are not being observed' }
    'PASS: all 39 additional counters and nine metadata breakdowns render, follow live settings, and remain separate from chart controls.'
    $plugin.OnViewRemoved('LibraryStatistics',$statsId)
    if(!$statsWidget.GetType().GetField('closed',$flags).GetValue($statsWidget) -or $statsWidget.GetType().GetField('source',$flags).GetValue($statsWidget)) { throw 'Statistics widget retained source subscription after removal' }
    [void]$layout.Children.Remove($statsNodeView)
    if($extraCounterSubscription.Count -ne 0) { throw 'Removed statistics widget retained nested option subscriptions' }
    'PASS: statistics catalog, ancestor settings binding, real counters/editor, cached revisits, counter toggles and removal cleanup.'

    [DashboardTestProxy]::FeaturedFixtureGames.Clear()
    'PASS: featured game registration, pinned rendering, live theme/color/size controls, cached revisits, settings editor and removal cleanup.'
    $legacyTotal = Get-Setting $settingsModel.Settings 'MaxNumberRecentAchievements'
    $legacyPerGame = Get-Setting $settingsModel.Settings 'MaxNumberRecentAchievementsPerGame'
    Set-Setting $settingsModel.Settings 'MaxNumberRecentAchievements' 17
    Set-Setting $settingsModel.Settings 'MaxNumberRecentAchievementsPerGame' 3
    $legacyRecent = [LandingPage.Models.Layout.ViewProperties]::new(); $legacyRecent.ViewId = 'RecentAchivements'
    $legacyRoot = [LandingPage.Models.Layout.GridNode]::new(); $legacyRoot.ViewProperties = $legacyRecent
    $legacyOwner = [LandingPage.LandingPageSettings]::new(); Set-Setting $legacyOwner 'GridLayout' $legacyRoot; Set-Setting $legacyOwner 'MaxNumberRecentAchievements' 17; Set-Setting $legacyOwner 'MaxNumberRecentAchievementsPerGame' 3
    [LandingPage.Settings.AchievementWidgetSettings]::MigrateLegacy($legacyOwner)
    if ($legacyRecent.AchievementSettings.Limit -ne 17 -or $legacyRecent.AchievementSettings.PerGameLimit -ne 3) { throw 'Old recent achievement limits did not migrate to panel settings' }
    $legacyRecent.AchievementSettings.Limit = 31
    if ((Get-Setting $settingsModel.Settings 'MaxNumberRecentAchievements') -ne 17) { throw 'Editing panel limits modified legacy shared settings' }
    Set-Setting $settingsModel.Settings 'MaxNumberRecentAchievements' $legacyTotal
    Set-Setting $settingsModel.Settings 'MaxNumberRecentAchievementsPerGame' $legacyPerGame
    'PASS: legacy recent achievement count/per-game limits migrate once and become independent panel settings.'
    # Text-element selectors keep one editor visible and do not modify settings on selection.
    function Find-TextEditors($root) {
        if ($root -is [LandingPage.Controls.TextSettingsSelector]) { $root; return }
        if ($root -is [Windows.DependencyObject]) { foreach ($child in [Windows.LogicalTreeHelper]::GetChildren($root)) { if ($child -is [Windows.DependencyObject]) { Find-TextEditors $child } } }
    }
    $textEditors = @(
        @([LandingPage.Views.Settings.AchievementWidgetSettingsView]::new(),[LandingPage.Settings.AchievementWidgetSettings]::Create('GameAchievementList')),
        @([LandingPage.Views.Settings.ClockSettingsView]::new(),[LandingPage.Settings.ClockSettings]::new()),
        @([LandingPage.Views.Settings.FeaturedGameSettingsView]::new(),[LandingPage.Settings.FeaturedGameSettings]::new()),
        @([LandingPage.Views.Settings.LibraryStatisticsSettingsView]::new(),[LandingPage.Settings.LibraryStatisticsSettings]::new()),
        @([LandingPage.Views.Settings.GameDetailSettingsView]::new(),[LandingPage.Settings.FeaturedGameSettings]::new())
    )
    foreach ($pair in $textEditors) {
        $editorControl = $pair[0]; $editorSettings = $pair[1]
        if ($editorControl -is [LandingPage.Views.Settings.AchievementWidgetSettingsView]) { $editorControl.WidgetId = 'GameAchievementList' }
        if ($editorControl -is [LandingPage.Views.Settings.GameDetailSettingsView]) { $editorControl.WidgetId = 'GameDetailTitle' }
        $editorControl.DataContext = $editorSettings
        [void]$view.Content.Children.Add($editorControl); $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
        $beforeTextSwitch = [Newtonsoft.Json.JsonConvert]::SerializeObject($editorSettings)
        $selectors = @(Find-TextEditors $editorControl)
        if (!$selectors.Count) { throw ('No text selector in '+$editorControl.GetType().Name) }
        foreach ($selector in $selectors) {
            $ancestor = [Windows.LogicalTreeHelper]::GetParent($selector)
            while ($ancestor -and ![object]::ReferenceEquals($ancestor,$editorControl)) { if ($ancestor -is [Windows.Controls.Expander]) { $ancestor.IsExpanded = $true }; $ancestor = [Windows.LogicalTreeHelper]::GetParent($ancestor) }
            $window.UpdateLayout()
            foreach ($entry in @($selector.ElementSelector.Items)) {
                $selector.ElementSelector.SelectedItem = $entry; $window.UpdateLayout()
                if (@($selector.Entries | Where-Object Visibility -eq ([Windows.Visibility]::Visible)).Count -ne 1 -or $entry.Entry.Visibility -ne [Windows.Visibility]::Visible) { throw 'Text selector exposes more than the selected editor' }
                $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
                if ($selector.ElementSelector.Visibility -eq [Windows.Visibility]::Visible) {
                    $visibleLabel = @([ToolbarTestVisuals]::Text($selector.ElementSelector) | ForEach-Object Text)
                    if ($selector.ElementSelector.IsDropDownOpen -or $visibleLabel -notcontains $entry.Header) { throw ('Closed text dropdown does not display its selected label: '+$entry.Header) }
                }

            }
        }
        if ([Newtonsoft.Json.JsonConvert]::SerializeObject($editorSettings) -ne $beforeTextSwitch) { throw ('Switching text elements modified '+$editorControl.GetType().Name+' settings') }
        if ($editorControl -is [LandingPage.Views.Settings.AchievementWidgetSettingsView]) {
            $opacityField = [ToolbarTestVisuals]::Named($editorControl,'CardOpacityField'); $dimensions = [ToolbarTestVisuals]::Named($editorControl,'AppearanceDimensions')
            if ($opacityField.Parent -is [Windows.Controls.Primitives.UniformGrid] -or $opacityField.ActualWidth -lt $dimensions.ActualWidth*.9) { throw 'Card opacity does not span both settings columns' }
            $selector = $selectors[0]; $selector.ElementSelector.SelectedIndex = 0
            $color = @([Windows.LogicalTreeHelper]::GetChildren($selector.Entries[0].Content) | Where-Object { $_ -is [Windows.Controls.WrapPanel] })[0].Children | Where-Object { $_ -is [LandingPage.Controls.PopupColorPicker] }
            if (!$color) { throw 'Text color picker is not inline beside the size/style controls' }
        }
        [void]$view.Content.Children.Remove($editorControl)
    }
    'PASS: achievement, clock, featured-game, statistics and standalone game text selectors show one editor and preserve all settings when switching.'
    # Compact color popups preserve theme defaults and write through both bindings.
    $popupSettings = [LandingPage.Settings.AchievementWidgetSettings]::Create('GameAchievementList')
    $popupSettings.CustomTextColors = $false; $popupSettings.TitleColor = '#123456'
    $compactColor = [LandingPage.Controls.PopupColorPicker]::new()
    $compactColor.DataContext = $popupSettings
    foreach ($bindingPair in @(@('SelectedColor','TitleColor'),@('UseCustomColor','CustomTextColors'))) {
        $binding = [Windows.Data.Binding]::new($bindingPair[1]); $binding.Mode = 'TwoWay'; $binding.UpdateSourceTrigger = 'PropertyChanged'
        [void]$compactColor.SetBinding([LandingPage.Controls.PopupColorPicker].GetField(($bindingPair[0]+'Property')).GetValue($null),$binding)
    }
    [void]$view.Content.Children.Add($compactColor)
    $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $compactColor.GetType().GetMethod('BuildEditor',$flags).Invoke($compactColor,$null)
    $customCheck = $compactColor.GetType().GetField('custom',$flags).GetValue($compactColor)
    $colorViewport = $compactColor.GetType().GetField('editorViewport',$flags).GetValue($compactColor)
    $compactColor.GetType().GetMethod('UpdatePreview',$flags).Invoke($compactColor,$null)
    if ($popupSettings.CustomTextColors -or $colorViewport.IsEnabled -or [Windows.Media.ColorConverter]::ConvertFromString($compactColor.SelectedColor) -ne [Windows.Media.ColorConverter]::ConvertFromString('#123456')) { throw 'Opening color editor changed defaults or enabled a disabled palette' }
    $customCheck.IsChecked = $true
    if (!$popupSettings.CustomTextColors -or !$colorViewport.IsEnabled) { throw 'Popup custom-color toggle did not update settings' }
    $colorViewport.Content.SetCurrentValue([LandingPage.Controls.ColorPicker]::SelectedColorProperty,'#ABCDEF')
    if ([Windows.Media.ColorConverter]::ConvertFromString($popupSettings.TitleColor) -ne [Windows.Media.ColorConverter]::ConvertFromString('#ABCDEF')) { throw 'Popup palette did not write color to widget settings' }
    $customCheck.IsChecked = $false
    if ($popupSettings.CustomTextColors -or $colorViewport.IsEnabled -or [Windows.Media.ColorConverter]::ConvertFromString($popupSettings.TitleColor) -ne [Windows.Media.ColorConverter]::ConvertFromString('#ABCDEF')) { throw 'Turning custom color off lost the saved color' }
    $compactColor.ShowCustomColorOption = $false
    if ($customCheck.Visibility -ne [Windows.Visibility]::Collapsed -or !$colorViewport.IsEnabled) { throw 'Shared-toggle palette did not remain usable' }
    [void]$view.Content.Children.Remove($compactColor)
    'PASS: compact popup custom-color and palette bindings preserve theme defaults and saved colors.'
    # Actual nested picker menus preserve add commands and single-instance availability.
    $oldRegistry = $plugin.AllAvailableViews
    $oldChartViews = $plugin.GetType().GetField('libraryStatisticsViews',$flags).GetValue($plugin)
    if (!$oldChartViews) { Set-Field $plugin 'libraryStatisticsViews' ([Collections.Generic.Dictionary[guid,LandingPage.Views.LibraryStatisticsView]]::new()) }
    $menuRegistry = [Collections.Generic.Dictionary[string,Collections.Generic.List[LandingPage.Models.Layout.StartPageViewArgs]]]::new()
    $builtinMenuViews = [Collections.Generic.List[LandingPage.Models.Layout.StartPageViewArgs]]::new()
    foreach ($args in $plugin.GetAvailableStartPageViews().Views) { $builtinMenuViews.Add([LandingPage.Models.Layout.StartPageViewArgs]::new($args,$plugin.Id)) }
    $menuRegistry.Add('StartPage',$builtinMenuViews)
    $foreignMenuViews = [Collections.Generic.List[LandingPage.Models.Layout.StartPageViewArgs]]::new()
    $foreignArgs = [LandingPage.Models.Layout.StartPageViewArgs]::new(); $foreignArgs.ViewId = 'ExternalFixture'; $foreignArgs.Name = 'External entry'; $foreignArgs.PluginId = [guid]::NewGuid(); $foreignMenuViews.Add($foreignArgs); $menuRegistry.Add('External provider',$foreignMenuViews)
    $plugin.AllAvailableViews = $menuRegistry
    $existingRecentPage = [LandingPage.Models.DashboardPage]::new(); $existingRecentPage.Layout = [LandingPage.Models.Layout.GridNode]::new()
    $existingRecentPage.Layout.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $existingRecentPage.Layout.ViewProperties.PluginId = $plugin.Id; $existingRecentPage.Layout.ViewProperties.ViewId = 'RecentAchivements'
    $vm.Pages.Add($existingRecentPage)
    $pickerNode = [LandingPage.Models.Layout.GridNode]::new(); $pickerModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($pickerNode); $pickerModel.EditModeEnabled = $true
    Wait-Task ($pickerModel.InititalizeGrid())
    $pickerHost = [LandingPage.Views.GridNodeView]::new(); $pickerHost.DataContext = $pickerModel
    [void]$view.Content.Children.Add($pickerHost)
    try {
        $window.UpdateLayout(); $picker = $pickerHost.FindName('WidgetPicker')
        $providerMenu = $picker.Items[0]
        if ((@($providerMenu.Children | ForEach-Object Name) -join ',') -ne 'General,Achievements,Library,Game details') { throw 'StartPage widget folders are missing or out of order' }
        $foreignMenu = $picker.Items[1]
        if ($foreignMenu.Children.Count -ne 1 -or $foreignMenu.Children[0].Name -ne 'External entry' -or !$foreignMenu.Children[0].View) { throw 'External provider entries were unexpectedly regrouped' }
        # Exercise the actual WPF leaf style without relying on native popup focus/capture.
        $leafStyle = $pickerHost.Resources['WidgetMenuItemStyle']
        $recentEntry = @($providerMenu.Children[1].Children | Where-Object { $_.View.ViewArgs.ViewId -eq 'RecentAchivements' })[0]
        $recentMenu = [Windows.Controls.MenuItem]::new(); $recentMenu.Style = $leafStyle; $recentMenu.DataContext = $recentEntry
        [void]$pickerHost.Content.Children.Add($recentMenu); $window.UpdateLayout()
        if (!$recentMenu.Command -or $recentMenu.IsEnabled -or $recentMenu.CommandParameter.ViewArgs.ViewId -ne 'RecentAchivements') { throw 'Nested picker lost the recent-achievement single-instance restriction' }
        $chartEntry = @($providerMenu.Children[2].Children | Where-Object { $_.View.ViewArgs.ViewId -eq 'LibraryChart' })[0]
        $chartMenu = [Windows.Controls.MenuItem]::new(); $chartMenu.Style = $leafStyle; $chartMenu.DataContext = $chartEntry
        [void]$pickerHost.Content.Children.Add($chartMenu); $window.UpdateLayout()
        if (!$chartMenu.IsEnabled -or !$chartMenu.Command) { throw 'Nested Library chart entry is not addable' }
        $chartMenu.Command.Execute($chartMenu.CommandParameter)
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
        if ($pickerNode.ViewProperties.ViewId -ne 'LibraryChart' -or $pickerNode.ViewProperties.PluginId -ne $plugin.Id) { throw 'Nested chart entry did not add the selected widget' }
        $plugin.OnViewRemoved('LibraryChart',$pickerNode.ViewProperties.InstanceId)
        'PASS: nested General/Achievements/Library/Game details menus, external-provider structure, disabled duplicate entries and working nested add commands.'
    } finally { [void]$view.Content.Children.Remove($pickerHost); [void]$vm.Pages.Remove($existingRecentPage); $plugin.AllAvailableViews = $oldRegistry; Set-Field $plugin 'libraryStatisticsViews' $oldChartViews }
    # Reorient aligned split groups without destroying widgets, including a fixed leading toolbar.
    foreach ($axis in @([Windows.Controls.Orientation]::Horizontal,[Windows.Controls.Orientation]::Vertical)) {
        $matrix = [LandingPage.Models.Layout.GridNode]::new(); $matrix.Orientation = $(if ($axis -eq [Windows.Controls.Orientation]::Horizontal) { [Windows.Controls.Orientation]::Vertical } else { [Windows.Controls.Orientation]::Horizontal })
        $leading = [LandingPage.Models.Layout.GridNode]::new(); $leading.Size = [Windows.GridLength]::Auto
        $leading.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $leading.ViewProperties.ViewId = 'Fixed toolbar'
        $fixedControl = [DashboardTestWidget]::new(); $fixedControl.Width = 30; $fixedControl.Height = 30
        [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($leading.ViewProperties,$fixedControl); $matrix.Children.Add($leading)
        $cells = @(); $controls = @()
        for ($r=0; $r -lt 2; $r++) {
            $stripe = [LandingPage.Models.Layout.GridNode]::new(); $stripe.Orientation = $axis; $stripe.Size = [Windows.GridLength]::new(.5,[Windows.GridUnitType]::Star)
            for ($c=0; $c -lt 2; $c++) {
                $cell = [LandingPage.Models.Layout.GridNode]::new(); $cell.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $cell.ViewProperties.ViewId = ('Matrix '+$r+','+$c)
                $control = [DashboardTestWidget]::new(); $controls += $control; [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($cell.ViewProperties,$control)
                $stripe.Children.Add($cell); $cells += $cell
            }
            $matrix.Children.Add($stripe)
        }
        $matrixModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($matrix); $matrixModel.EditModeEnabled = $true
        Wait-Task ($matrixModel.InititalizeGrid())
        $matrixHost = [LandingPage.Views.GridNodeView]::new(); $matrixHost.DataContext = $matrixModel; $matrixHost.Width = 800; $matrixHost.Height = 600
        [void]$layout.Children.Add($matrixHost); $window.UpdateLayout()
        $oldModels = @($matrixModel.AllNodes)
        $selectedModel = @($oldModels | Where-Object { [object]::ReferenceEquals($_.GridNode,$cells[1]) })[0]
        $dimension = $(if ($axis -eq [Windows.Controls.Orientation]::Horizontal) { 'ActualHeight' } else { 'ActualWidth' })
        $untouchedControl = $controls[0]; $oldDimension = $untouchedControl.$dimension
        $command = $(if ($axis -eq [Windows.Controls.Orientation]::Horizontal) { $selectedModel.IndependentColumnHeightsCommand } else { $selectedModel.IndependentRowWidthsCommand })
        if (!$command.CanExecute($null)) { throw 'Aligned matrix does not offer independent split conversion' }
        $idsBefore = @($matrix.Enumerate() | Where-Object ViewProperties | ForEach-Object { $_.ViewProperties.InstanceId.ToString() }) | Sort-Object
        if ($axis -eq [Windows.Controls.Orientation]::Horizontal) {
            $view.GetType().GetMethod('ShowAppearance',$flags).Invoke($view,@($selectedModel.PSObject.BaseObject))
            $editor = $view.FindName('AppearanceEditor'); $editor.FindName('PanelLayoutSection').IsExpanded = $true; $window.UpdateLayout()
            $layoutButton = @([ToolbarTestVisuals]::Buttons($editor) | Where-Object Content -eq 'Make column heights independent')[0]
            if (!$layoutButton.IsEnabled) { throw 'Independent column layout button is unavailable in widget settings' }
            [void]$layoutButton.GetType().GetMethod('OnClick',$flags).Invoke($layoutButton,@())
            $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
            if ($view.FindName('AppearanceOverlay').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Independent layout action did not return to dashboard editing' }
        } else { $command.Execute($null) }
        $window.UpdateLayout()
        if ($matrix.Children.Count -ne 2 -or ![object]::ReferenceEquals($matrix.Children[0],$leading) -or $matrix.Children[1].Orientation -ne $axis) { throw 'Split conversion changed the fixed toolbar or failed to group the matrix' }
        $region = $matrix.Children[1]
        if (![object]::ReferenceEquals($region.Children[0].Children[0],$cells[0]) -or ![object]::ReferenceEquals($region.Children[1].Children[0],$cells[1]) -or ![object]::ReferenceEquals($region.Children[0].Children[1],$cells[2])) { throw 'Split conversion changed widget positions/instances' }
        if ([Math]::Abs($untouchedControl.$dimension-$oldDimension) -gt 1) { throw 'Converting aligned splits shifted the initial geometry' }
        $region.Children[1].Children[0].Size = [Windows.GridLength]::new(3,[Windows.GridUnitType]::Star)
        $region.Children[1].Children[1].Size = [Windows.GridLength]::new(1,[Windows.GridUnitType]::Star); $window.UpdateLayout()
        if ([Math]::Abs($untouchedControl.$dimension-$oldDimension) -gt 1 -or $controls[1].$dimension -lt $oldDimension*1.3) { throw 'Resizing one column/row still changes its neighbor' }
        $idsAfter = @($matrix.Enumerate() | Where-Object ViewProperties | ForEach-Object { $_.ViewProperties.InstanceId.ToString() }) | Sort-Object
        if (($idsBefore -join ',') -ne ($idsAfter -join ',')) { throw 'Independent sizing lost widget identities' }
        foreach ($control in $controls) { if (!$control.Parent -or $control.CloseCount -ne 0) { throw 'Independent sizing closed or detached a retained widget' } }
        $restoredMatrix = [LandingPage.Models.DashboardProfiles]::Clone($matrix)
        if ($restoredMatrix.Children[1].Children[1].Children[0].Size.Value -ne 3 -or $restoredMatrix.ViewCount -ne 5) { throw 'Independent split sizes did not survive serialization' }
        # Obsolete view models must not steal a retained widget when its settings/model change later.
        $newProps = [LandingPage.Models.Layout.ViewProperties]::new(); $newProps.ViewId = 'Changed retained widget'; $newControl = [DashboardTestWidget]::new()
        [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($newProps,$newControl); $cells[0].ViewProperties = $newProps; $window.UpdateLayout()
        $newModel = @($matrixModel.AllNodes | Where-Object { [object]::ReferenceEquals($_.GridNode,$cells[0]) })[0]
        if (![object]::ReferenceEquals($newControl.Parent,$newModel.View)) { throw 'An obsolete layout model stole a reused widget after conversion' }
        [void]$layout.Children.Remove($matrixHost)
    }
    'PASS: independent column heights/row widths, fixed toolbar, unchanged neighbor geometry, retained widgets, serialization and detached obsolete observers.'
    # One top-right gear opens appearance plus any existing provider settings.
    foreach ($widgetId in @('MostPlayed','GameShelves','LibraryChart','FeaturedGame')) {
        $properties = [LandingPage.Models.Layout.ViewProperties]::new(); $properties.ViewId = $widgetId; $properties.PluginId = $plugin.Id
        $properties.StartPageViewArgs = @($plugin.GetAvailableStartPageViews().Views | Where-Object ViewId -eq $widgetId)[0]
        [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).SetValue($properties,[DashboardTestWidget]::new())
        if ($widgetId -eq 'GameShelves') {
            $shelves = Get-Setting $settingsModel.Settings 'ShelveInstanceSettings'
            if (!$shelves) { $shelves = [Collections.Generic.Dictionary[guid,LandingPage.Settings.ShelvesSettings]]::new(); Set-Setting $settingsModel.Settings 'ShelveInstanceSettings' $shelves }
            $shelves[$properties.InstanceId] = [LandingPage.Settings.ShelvesSettings]::new($settingsModel.Settings)
        }
        if ($widgetId -eq 'FeaturedGame') { $properties.FeaturedGameSettings.UseCustomSectionLayout = $true }
        $node = [LandingPage.Models.Layout.GridNode]::new(); $node.ViewProperties = $properties
        $nodeModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($node); $nodeModel.IsLeaf = $true; $nodeModel.HasView = $true; $nodeModel.EditModeEnabled = $true
        $nodeHost = [LandingPage.Views.GridNodeView]::new(); $nodeHost.DataContext = $nodeModel; $nodeHost.Width = 700; $nodeHost.Height = 220
        [void]$view.Content.Children.Add($nodeHost); $window.UpdateLayout()
        $gear = $nodeHost.FindName('SettingsButton')
        if ($gear -isnot [Windows.Controls.Button] -or $gear.HorizontalAlignment -ne [Windows.HorizontalAlignment]::Right -or $nodeHost.FindName('SettingsPopup') -or @([ToolbarTestVisuals]::Buttons($nodeHost) | Where-Object Content -eq 'Appearance').Count) { throw 'Duplicate Appearance/legacy settings entry remains on the panel' }
        if ($widgetId -eq 'LibraryChart' -or $widgetId -eq 'FeaturedGame') {
            $edit = $nodeHost.FindName($(if ($widgetId -eq 'LibraryChart') { 'ChartEditingButton' } else { 'FeaturedSectionsButton' }))
            $left = $gear.TranslatePoint([Windows.Point]::new(0,0),$nodeHost).X
            $editRight = $edit.TranslatePoint([Windows.Point]::new($edit.ActualWidth,0),$nodeHost).X
            if ($edit.Visibility -ne [Windows.Visibility]::Visible -or $editRight -gt $left) { throw 'Gear overlaps the chart/section editor button' }
        }
        $gear.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)); $window.UpdateLayout()
        $editor = $view.FindName('AppearanceEditor'); $provider = $editor.FindName('ProviderSettingsHost'); $section = $editor.FindName('ProviderSettingsSection')
        if ($view.FindName('AppearanceOverlay').Visibility -ne [Windows.Visibility]::Visible -or ![object]::ReferenceEquals($editor.DataContext,$nodeModel)) { throw 'Gear did not open the unified widget window' }
        if ($widgetId -eq 'MostPlayed' -and $provider.Content -isnot [LandingPage.Views.Settings.MostPlayedSettingsView]) { throw 'Most Played settings were lost from the unified window' }
        if ($widgetId -eq 'GameShelves' -and $provider.Content -isnot [LandingPage.Views.Settings.ShelvesSettingsView]) { throw 'Shelf settings were lost from the unified window' }
        $view.GetType().GetMethod('CloseAppearance',$flags).Invoke($view,@())
        if ($provider.Content) { throw 'Closing the unified window retained provider settings in a parent' }
        [void]$view.Content.Children.Remove($nodeHost)
    }
    'PASS: single top-right gear, unified appearance/provider controls, Most Played/shelf editors, close cleanup and chart/section button spacing.'
    $externalSettingsProvider = [WidgetSettingsTestProvider]::new(); [DashboardTestProxy]::ExtraPlugins.Add($externalSettingsProvider)
    try {
        $externalProps = [LandingPage.Models.Layout.ViewProperties]::new(); $externalProps.ViewId = 'ExternalFixture'; $externalProps.PluginId = $externalSettingsProvider.Id
        $externalProps.StartPageViewArgs = [StartPage.SDK.StartPageViewArgsBase]::new(); $externalProps.StartPageViewArgs.HasSettings = $true
        $externalNode = [LandingPage.Models.Layout.GridNode]::new(); $externalNode.ViewProperties = $externalProps
        $externalModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($externalNode)
        $showEditor = $view.GetType().GetMethod('ShowAppearance',$flags)
        [void]$showEditor.Invoke($view,@($externalModel)); $window.UpdateLayout()
        $editor = $view.FindName('AppearanceEditor'); $providerHost = $editor.FindName('ProviderSettingsHost')
        if (![object]::ReferenceEquals($providerHost.Content,$externalSettingsProvider.Editor) -or $externalSettingsProvider.Requests -ne 1) { throw 'External settings were dropped or created repeatedly while opening' }
        $view.GetType().GetMethod('CloseAppearance',$flags).Invoke($view,@())
        [void]$showEditor.Invoke($view,@($externalModel)); $window.UpdateLayout()
        if (![object]::ReferenceEquals($providerHost.Content,$externalSettingsProvider.Editor) -or $externalSettingsProvider.Requests -ne 2) { throw 'Cached external settings could not be reopened after detaching' }
        $view.GetType().GetMethod('CloseAppearance',$flags).Invoke($view,@())
    } finally { [void][DashboardTestProxy]::ExtraPlugins.Remove($externalSettingsProvider) }
    'PASS: SDK external settings reuse their own control, open once per session and detach/reopen without a second-parent error.'

    # StartPage-owned achievement widgets retain real ancestor bindings and stop hidden timers.
    foreach ($achievementId in @('AchievementProgress','AchievementFinishNext','AchievementGoals','GameAchievementList','RecentAchivements')) {
        if (!@($plugin.GetAvailableStartPageViews().Views | Where-Object { $_.ViewId -eq $achievementId -and ($_.AllowMultipleInstances -or $achievementId -eq 'RecentAchivements') })) { throw 'New achievement widget missing from Add view' }
        $instanceId = [guid]::NewGuid(); $achievementWidget = $plugin.GetStartPageView($achievementId,$instanceId)
        $achievementProps = [LandingPage.Models.Layout.ViewProperties]::new(); $achievementProps.ViewId = $achievementId; $achievementProps.InstanceId = $instanceId
        $achievementProps.AchievementSettings.TitleSize = 37
        $achievementNode = [LandingPage.Models.Layout.GridNode]::new(); $achievementNode.ViewProperties = $achievementProps
        $achievementModel = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($achievementNode); $achievementModel.IsLeaf = $true; $achievementModel.HasView = $true
        [void]$achievementModel.View.Children.Add($achievementWidget)
        $achievementHost = [LandingPage.Views.GridNodeView]::new(); $achievementHost.DataContext = $achievementModel
        [void]$layout.Children.Add($achievementHost); $window.UpdateLayout()
        Wait-Task ($achievementWidget.OnViewShownAsync())
        if (![object]::ReferenceEquals($achievementWidget.Settings,$achievementProps.AchievementSettings) -or $achievementWidget.Settings.TitleSize -ne 37) { throw 'Achievement widget lost its real panel settings binding' }
        $replacement = [LandingPage.Settings.AchievementWidgetSettings]::Create($achievementId); $replacement.TitleSize = 24
        $achievementProps.AchievementSettings = $replacement; $window.UpdateLayout()
        if (![object]::ReferenceEquals($achievementWidget.Settings,$replacement)) { throw 'Achievement settings reset did not update the actual widget' }
        if ($achievementId -eq 'RecentAchivements') {
            if ($achievementWidget.Content -isnot [LandingPage.Views.RecentAchievementsView]) { throw 'Recent Achievements no longer uses its original renderer' }
            $legacyView = $achievementWidget.Content
            $entryType = $plugin.GetType().Assembly.GetType('LandingPage.Models.GameAchievementEntry')
            $entry = [Activator]::CreateInstance($entryType); $entry.Name = 'Original achievement'; $entry.Description = 'Original description'; $entry.Unlocked = 1; $entry.UnlockedOn = [datetime]::UtcNow
            $entryList = [Activator]::CreateInstance([Collections.Generic.List`1].MakeGenericType($entryType)); $entryList.Add($entry)
            $results = [Array]::CreateInstance([Collections.Generic.IList`1].MakeGenericType($entryType),1); $results.SetValue($entryList,0)
            $game = [Playnite.SDK.Models.Game]::new(); $game.Id = [guid]::NewGuid(); $game.Name = 'Original game'
            $gameList = [Collections.Generic.List[Playnite.SDK.Models.Game]]::new(); $gameList.Add($game)
            $progressType = $plugin.GetType().Assembly.GetType('LandingPage.Models.ShelfAchievementProgress')
            $summary = [Activator]::CreateInstance([Collections.Generic.Dictionary`2].MakeGenericType([guid],$progressType))
            $progress = [Activator]::CreateInstance($progressType); $progress.Total = 10; $progress.Unlocked = 8; $summary.Add($game.Id,$progress)
            $options = [LandingPage.Settings.AchievementWidgetSettings]::Create('RecentAchivements')
            $feedArgs = [object[]]::new(4); $feedArgs[0] = $gameList; $feedArgs[1] = $results; $feedArgs[2] = $summary; $feedArgs[3] = $options
            $setFeed = $legacyView.GetType().GetMethod('SetFeed',$flags); [void]$setFeed.Invoke($legacyView,$feedArgs)
            $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
            $title = [ToolbarTestVisuals]::Named($legacyView,'AchievementTitle'); $description = [ToolbarTestVisuals]::Named($legacyView,'Description')
            $icon = [ToolbarTestVisuals]::Named($legacyView,'AchievementIcon'); $bar = [ToolbarTestVisuals]::Named($legacyView,'AchievementsProgress')
            if (!$title -or $title.Text -ne 'Original achievement' -or !$description -or $icon.Width -ne 35 -or !$bar -or ![ToolbarTestVisuals]::Named($legacyView,'Toggle')) { throw 'Original compact row/icon/description/progress/toggle template was not preserved' }
            if ($legacyView.DataContext.Inner.LatestAchievementsView.Groups[0].Name.GameName -ne 'Original game' -or $legacyView.DataContext.Inner.LatestAchievementsView.Groups[0].Name.Progress -ne .8) { throw 'Original game grouping/progress was not preserved' }
            $options.ShowDescription = $false; $options.ShowArtwork = $false; $window.UpdateLayout()
            if ($description.Visibility -ne [Windows.Visibility]::Collapsed -or $icon.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Optional visibility settings did not update the original row' }
            $options.ShowDescription = $true; $options.ShowArtwork = $true; $options.CustomizeRecentTypography = $true; $options.TitleSize = 29; $window.UpdateLayout()
            if ($title.FontSize -ne 29) { throw 'Opt-in typography did not affect the original achievement title' }
            $descriptionStyle = $options.TextStyles['Description']; $descriptionStyle.UseDefaults = $false; $descriptionStyle.Size = 21; $descriptionStyle.Italic = $true
            $groupStyle = $options.TextStyles['GroupTitle']; $groupStyle.UseDefaults = $false; $groupStyle.Size = 26; $groupStyle.Bold = $true
            $window.UpdateLayout()
            if ($description.FontSize -ne 21 -or $description.FontStyle -ne [Windows.FontStyles]::Italic) { throw 'Recent description style did not override the shared detail typography' }
            $groupTitle = [ToolbarTestVisuals]::Named($legacyView,'RecentGroupTitle')
            if ($groupTitle.FontSize -ne 26 -or $groupTitle.FontWeight -ne [Windows.FontWeights]::Bold) { throw 'Recent group header typography is not independently configurable' }
            $options.RecentGrouping = [LandingPage.Settings.RecentAchievementGrouping]::Source; [void]$setFeed.Invoke($legacyView,$feedArgs)
            $window.UpdateLayout()
            if ($legacyView.DataContext.Inner.LatestAchievementsView.Groups[0].Name.GameName -ne 'Playnite') { throw 'Source grouping did not use the original group template' }
            $options.RecentGrouping = [LandingPage.Settings.RecentAchievementGrouping]::None; [void]$setFeed.Invoke($legacyView,$feedArgs)
            if ($legacyView.DataContext.Inner.LatestAchievementsView.Groups.Count) { throw 'Ungrouped mode retained game headers' }
            'PASS: original Recent Achievements compact template, icons, descriptions, expansion toggle, game headers/progress and optional source/none grouping.'
        }
        Wait-Task ($achievementWidget.OnViewHiddenAsync())
        if ($achievementWidget.GetType().GetField('timer',$flags).GetValue($achievementWidget).IsEnabled -or $achievementWidget.GetType().GetField('observed',$flags).GetValue($achievementWidget)) { throw 'Hidden achievement widget retained refresh work/listeners' }
        $plugin.OnViewRemoved($achievementId,$instanceId)
        if (!$achievementWidget.GetType().GetField('closed',$flags).GetValue($achievementWidget)) { throw 'Removed achievement widget was not closed' }
        [void]$layout.Children.Remove($achievementHost)
    }
    'PASS: five achievement registrations, actual ancestor settings/reset binding, hidden refresh cleanup and removal lifecycle.'
    $retainedWidget = [LandingPage.Views.AchievementWidgetView]::new('GameAchievementList')
    $retainedWidget.Settings.GameSource = [LandingPage.Settings.AchievementGoalSource]::HoveredOrSelectedGame
    $hoverFixture = [Playnite.SDK.Models.Game]::new(); $hoverFixture.Id = [guid]::NewGuid()
    $selectionFixture = [Playnite.SDK.Models.Game]::new(); $selectionFixture.Id = [guid]::NewGuid()
    $plugin.GetType().GetField('<FeaturedHoveredGame>k__BackingField',$flags).SetValue($plugin,$null)
    $plugin.GetType().GetField('<LastFeaturedHoveredGame>k__BackingField',$flags).SetValue($plugin,$hoverFixture)
    $plugin.GetType().GetField('<FeaturedSelectedGame>k__BackingField',$flags).SetValue($plugin,$selectionFixture)
    $resolve = $retainedWidget.GetType().GetMethod('ResolveContextGame',$flags)
    if ($resolve.Invoke($retainedWidget,@($plugin)) -ne $hoverFixture.Id) { throw 'Leaving the shelf lost the last hovered achievements' }
    $retainedWidget.Settings.KeepLastHoveredGame = $false
    if ($resolve.Invoke($retainedWidget,@($plugin)) -ne $selectionFixture.Id) { throw 'Disabling hover retention did not restore selected-game fallback' }
    $retainedWidget.OnViewClosed()
    $plugin.GetType().GetField('<LastFeaturedHoveredGame>k__BackingField',$flags).SetValue($plugin,$null)
    $plugin.GetType().GetField('<FeaturedSelectedGame>k__BackingField',$flags).SetValue($plugin,$null)
    'PASS: hover retention preserves the last game on shelf exit, with an opt-out for selected-game fallback.'

    $hoverSettings = [LandingPage.Models.ShelveProperties]::new()
    $hoverSettings.HoverEffect = [LandingPage.Models.ShelfHoverEffect]::Dock; $hoverSettings.HoverScale = 1.4; $hoverSettings.HoverRadius = 2
    $hoverSettings.HoverGlow = $true; $hoverSettings.HoverDimOthers = $true
    $hoverList = [Windows.Controls.ListBox]::new(); $hoverList.IsHitTestVisible = $false; $hoverList.Width = 500; $hoverList.Height = 230; $hoverList.Padding = [Windows.Thickness]::new(3)
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
        # Synthetic pointer tests must discard pending real-pointer/scroll work.
        $window.UpdateLayout()
        $hoverController.GetType().GetMethod('CancelScheduled',$flags).Invoke($hoverController,@())
        $animationDeadline = [datetime]::UtcNow.AddSeconds(2)
        while ([datetime]::UtcNow -lt $animationDeadline -and [Math]::Abs($middleState.Scale.ScaleX - 1.4) -gt 0.01) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 5 }
        if ([Math]::Abs($middleState.Scale.ScaleX - 1.4) -gt 0.01) { throw ('Hover animation did not settle: ' + $animatedMode + '; scale=' + $middleState.Scale.ScaleX + '; target=' + $middleState.Target.Scale + '; visible=' + $middleCard.IsVisible + '; focused=' + $middleState.Focused) }
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
    $beforePendingPage = $vm.SelectedPage.Id; $beforePendingCount = $vm.Pages.Count; $beforePendingRoot = $vm.RootNodeViewModel
    $vm.BeginPageEditing()
    $pendingPage = [LandingPage.Models.DashboardPages]::Add($vm.Settings.Settings,'Pending page')
    Wait-Task ($vm.SwitchPageAsync($pendingPage))
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    if(!$vm.RootNodeViewModel.View.RowDefinitions[0].Height.IsAuto) { throw 'Toolbar row did not retain automatic height in WPF' }
    if($vm.RootNodeViewModel.View.RowDefinitions[0].ActualHeight -lt 12) { throw 'Default toolbar collapsed to an invisible row' }
    $pendingToolbar = [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).GetValue($pendingPage.Layout.Children[0].ViewProperties)
    if ($pendingToolbar -isnot [LandingPage.Views.ToolbarWidgetView]) { throw 'New-page test rendered a missing-provider placeholder instead of the real toolbar' }
    Wait-Task ($vm.EndPageEditingAsync($false))
    if($vm.Pages.Count -ne $beforePendingCount -or $vm.SelectedPage.Id -ne $beforePendingPage -or ![object]::ReferenceEquals($vm.RootNodeViewModel,$beforePendingRoot)) { throw 'Dismiss did not remove a pending page and restore the original dashboard' }
    $savedInsetToolbar = $view.GetType().GetField('insetToolbar',$flags).GetValue($view)
    $topProbe = [Windows.Controls.Border]::new(); $topProbe.Height = 52; $topProbe.VerticalAlignment = [Windows.VerticalAlignment]::Top
    [void]$layout.Children.Add($topProbe); $window.UpdateLayout()
    $view.GetType().GetField('insetToolbar',$flags).SetValue($view,$topProbe)
    [void]$view.GetType().GetMethod('UpdateDashboardTopInset',$flags).Invoke($view,@())
    $surface = $view.FindName('DashboardSurface')
    if ($surface.Margin.Top -lt 52) { throw 'Overlapping native toolbar did not reserve space above widgets' }
    $originalViewMargin = $view.Margin
    $view.Margin = [Windows.Thickness]::new(0,80,0,0); $window.UpdateLayout()
    [void]$view.GetType().GetMethod('UpdateDashboardTopInset',$flags).Invoke($view,@())
    if ($surface.Margin.Top -gt 0.5) { throw 'Dashboard already below the native toolbar received duplicate top spacing' }
    $view.Margin = $originalViewMargin; $layout.Children.Remove($topProbe)
    $view.GetType().GetField('insetToolbar',$flags).SetValue($view,$savedInsetToolbar)
    $window.UpdateLayout(); [void]$view.GetType().GetMethod('UpdateDashboardTopInset',$flags).Invoke($view,@())
    $originalChrome = [Windows.Shell.WindowChrome]::GetWindowChrome($window)
    try {
        $hiddenChrome = [Windows.Shell.WindowChrome]::new(); $hiddenChrome.CaptionHeight = 0
        [Windows.Shell.WindowChrome]::SetWindowChrome($window,$hiddenChrome)
        $view.GetType().GetField('insetToolbar',$flags).SetValue($view,$null)
        $window.UpdateLayout(); [void]$view.GetType().GetMethod('UpdateDashboardTopInset',$flags).Invoke($view,@())
        $expectedCaptionInset = [Math]::Max(0,32 - $view.TranslatePoint([Windows.Point]::new(0,0),$window).Y)
        if ([Math]::Abs($surface.Margin.Top - $expectedCaptionInset) -gt 0.5) { throw 'Hidden-caption theme did not reserve hover title-bar clearance' }
    } finally {
        [Windows.Shell.WindowChrome]::SetWindowChrome($window,$originalChrome)
        $view.GetType().GetField('insetToolbar',$flags).SetValue($view,$savedInsetToolbar)
        $window.UpdateLayout(); [void]$view.GetType().GetMethod('UpdateDashboardTopInset',$flags).Invoke($view,@())
    }
    'PASS: overlapping host toolbar reserves top clearance while normally positioned dashboards keep their original spacing.'
    $measureToolbar = [LandingPage.Views.ToolbarWidgetView]::new()
    [void]$measureToolbar.GetType().GetMethod('RenderToolbar',$flags).Invoke($measureToolbar,@())
    $measureToolbar.Measure([Windows.Size]::new(600,[double]::PositiveInfinity))
    $measureToolbar.Arrange([Windows.Rect]::new(0,0,600,600)); $measureToolbar.UpdateLayout()
    $measureToolbar.InvalidateMeasure(); $measureToolbar.Measure([Windows.Size]::new(600,[double]::PositiveInfinity))
    if ($measureToolbar.DesiredSize.Height -gt 160) { throw 'Content-sized toolbar retained its previous stretched height' }
    $menuOpacity = $view.ContextMenu.Opacity
    try {
        $view.ContextMenu.Opacity = 0
        [void]$view.GetType().GetMethod('PositionDashboardContextMenu',$flags).Invoke($view,@([Windows.Point]::new(980,780)))
        $view.ContextMenu.IsOpen = $true
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
        $view.ContextMenu.UpdateLayout()
        $menuScreen = $view.ContextMenu.PointToScreen([Windows.Point]::new(0,0))
        $windowScreen = $window.PointToScreen([Windows.Point]::new(0,0))
        if ($menuScreen.X -lt $windowScreen.X - 2 -or $menuScreen.Y -lt $windowScreen.Y - 2 -or $menuScreen.X + $view.ContextMenu.ActualWidth -gt $windowScreen.X + $window.ActualWidth + 2 -or $menuScreen.Y + $view.ContextMenu.ActualHeight -gt $windowScreen.Y + $window.ActualHeight + 2) { throw 'Background context menu escaped the Playnite window bounds' }
    } finally { $view.ContextMenu.IsOpen = $false; $view.ContextMenu.Opacity = $menuOpacity }
    $dropdownProbe = [Windows.Controls.ComboBox]::new(); [void]$dropdownProbe.Items.Add('Probe')
    [void]$layout.Children.Add($dropdownProbe); $window.UpdateLayout(); $dropdownProbe.ApplyTemplate() | Out-Null
    $dropdownPopup = $dropdownProbe.Template.FindName('PART_Popup',$dropdownProbe)
    if (!$dropdownPopup) { throw 'Probe combo has no real popup template' }
    $dropdownPopup.Placement = [Windows.Controls.Primitives.PlacementMode]::AbsolutePoint
    $dropdownPopup.HorizontalOffset = -3000; $dropdownPopup.VerticalOffset = -3000
    [void]$view.GetType().GetMethod('PrepareDropdown',$flags).Invoke($view,@($dropdownProbe))
    if (![object]::ReferenceEquals($dropdownPopup.PlacementTarget,$dropdownProbe) -or $dropdownPopup.Placement -ne [Windows.Controls.Primitives.PlacementMode]::Bottom -or $dropdownPopup.HorizontalOffset -ne 0 -or $dropdownPopup.VerticalOffset -ne 0) { throw 'Dropdown popup retained a detached/absolute screen anchor' }
    $layout.Children.Remove($dropdownProbe)
    'PASS: Auto toolbar shrinks after a stretched allocation and dropdown anchoring replaces invalid screen coordinates.'
    $persistedNewPageIds = @()
    foreach ($attempt in @(1,2)) {
        $view.ShowPages(); $vm.NewPageName = 'Saved new page attempt ' + $attempt
        $view.FindName('CreatePageSection').IsExpanded = $true; $window.UpdateLayout()
        $createButton = $view.FindName('CreatePageButton')
        [void]$createButton.GetType().GetMethod('OnClick',$flags).Invoke($createButton,@())
        $createDeadline = [datetime]::UtcNow.AddSeconds(15)
        while ($vm.IsSwitchingPage -and [datetime]::UtcNow -lt $createDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
        if ($vm.SelectedPage.Name -ne $vm.NewPageName) { throw 'Create page button did not select the created page' }
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
        if ($vm.RootNodeViewModel.View.RowDefinitions[0].ActualHeight -gt 160) { throw 'New page toolbar consumed the empty content section' }
        $createdId = $vm.SelectedPage.Id
        $persistedNewPageIds += $createdId
        $createdToolbar = [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).GetValue($vm.SelectedPage.Layout.Children[0].ViewProperties)
        if ($createdToolbar -isnot [LandingPage.Views.ToolbarWidgetView] -or !$createdToolbar.IsVisible -or $createdToolbar.ActualHeight -lt 12) { throw 'Created page toolbar is missing or invisible' }
        $leafPresenter = $vm.RootNodeViewModel.View.Children[0].FindName('ViewContentControl')
        if ($leafPresenter.Opacity -lt 0.99) { throw 'New toolbar is hidden by a stale leaf opacity animation' }
        $doneButton = $view.FindName('SavePagesButton')
        [void]$doneButton.GetType().GetMethod('OnClick',$flags).Invoke($doneButton,@())
        $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
        if ($view.FindName('PagesOverlay').Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Done did not complete the page save' }
        $diskSettings = [Newtonsoft.Json.JsonConvert]::DeserializeObject([IO.File]::ReadAllText($savedFile.FullName),[LandingPage.LandingPageSettings])
        $diskPages = [LandingPage.LandingPageSettings].GetProperty('DashboardPages').GetValue($diskSettings)
        foreach ($pageId in $persistedNewPageIds) {
            $diskPage = @($diskPages | Where-Object Id -eq $pageId)[0]
            if (!$diskPage -or $diskPage.Layout.Children.Count -ne 2 -or $diskPage.Layout.Children[0].ViewProperties.ViewId -ne 'ToolbarWidget') { throw 'Saved settings lost a newly created page or its toolbar/content layout' }
        }
        $reloadedActive = [LandingPage.Models.DashboardPages]::Ensure($diskSettings)
        if ($reloadedActive.Id -ne $createdId -or $reloadedActive.Layout.Children[0].ViewProperties.ViewId -ne 'ToolbarWidget') { throw 'Reloading settings lost the active new page or toolbar' }
    }
    'PASS: real Create page/Done buttons persist first and second pages, render the real toolbar visibly and retain layouts after settings reload.'
    $bulkOne = [LandingPage.Models.DashboardPages]::Add($vm.Settings.Settings,'Inactive bulk deletion A')
    $bulkTwo = [LandingPage.Models.DashboardPages]::Add($vm.Settings.Settings,'Inactive bulk deletion B')
    $vm.SaveCurrentPage()
    $beforeBulkRoot = $vm.RootNodeViewModel; $beforeBulkActive = $vm.SelectedPage.Id; $beforeBulkCount = $vm.Pages.Count
    $view.ShowPages(); $view.FindName('DeletePagesSection').IsExpanded = $true; $window.UpdateLayout()
    $pageDeleteList = $view.FindName('PagesToDeleteList')
    $pageDeleteList.SelectedItems.Add($bulkOne); $pageDeleteList.SelectedItems.Add($bulkTwo)
    if (!$view.FindName('RequestDeleteSelectedPagesButton').IsEnabled) { throw 'Bulk delete button waited for a dispatcher requery after selection changed' }
    if ($vm.PagesSelectedForDeletion.Count -ne 2 -or $vm.SelectedPage.Id -ne $beforeBulkActive -or ![object]::ReferenceEquals($beforeBulkRoot,$vm.RootNodeViewModel)) { throw 'Selecting deletion targets navigated or replaced the active dashboard' }
    $vm.RequestDeleteSelectedPagesCommand.Execute($null)
    if (!$view.FindName('ConfirmDeleteSelectedPagesButton').IsEnabled) { throw 'Confirmation delete button did not enable immediately' }
    $vm.CancelDeletePageCommand.Execute($null)
    if ($view.FindName('ConfirmDeleteSelectedPagesButton').IsEnabled) { throw 'Cancelled delete button stayed enabled until the next requery' }
    if ($vm.DeleteSelectedPagesCommand.CanExecute($null) -or $vm.Pages.Count -ne $beforeBulkCount) { throw 'Cancel did not cancel the bulk delete confirmation' }
    $vm.RequestDeleteSelectedPagesCommand.Execute($null); $vm.DeleteSelectedPagesCommand.Execute($null)
    if ($vm.Pages.Count -ne $beforeBulkCount - 2 -or $vm.Pages.Contains($bulkOne) -or $vm.Pages.Contains($bulkTwo) -or ![object]::ReferenceEquals($beforeBulkRoot,$vm.RootNodeViewModel) -or $vm.SelectedPage.Id -ne $beforeBulkActive) { throw 'Inactive bulk deletion loaded targets or changed the active page' }
    Wait-Task ($vm.EndPageEditingAsync($false))
    if ($vm.Pages.Count -ne $beforeBulkCount -or !@($vm.Pages | Where-Object Id -eq $bulkOne.Id) -or !@($vm.Pages | Where-Object Id -eq $bulkTwo.Id)) { throw 'Dismissing page manager did not restore bulk-deleted pages' }
    $vm.BeginPageEditing()
    $pageDeleteList.SelectAll()
    if ($view.FindName('RequestDeleteSelectedPagesButton').IsEnabled) { throw 'Selecting every page did not immediately disable deletion' }
    if ($vm.RequestDeleteSelectedPagesCommand.CanExecute($null) -or $vm.DeleteSelectedPagesCommand.CanExecute($null)) { throw 'Bulk deletion allows deleting every dashboard page' }
    $pageDeleteList.UnselectAll()
    $bulkOne = @($vm.Pages | Where-Object Name -eq 'Inactive bulk deletion A')[0]
    $bulkTwo = @($vm.Pages | Where-Object Name -eq 'Inactive bulk deletion B')[0]
    $pageDeleteList.SelectedItems.Add($bulkOne); $pageDeleteList.SelectedItems.Add($bulkTwo)
    $vm.RequestDeleteSelectedPagesCommand.Execute($null); $vm.DeleteSelectedPagesCommand.Execute($null)
    Wait-Task ($vm.EndPageEditingAsync($true))
    $bulkDisk = [Newtonsoft.Json.JsonConvert]::DeserializeObject([IO.File]::ReadAllText($savedFile.FullName),[LandingPage.LandingPageSettings])
    $bulkDiskPages = [LandingPage.LandingPageSettings].GetProperty('DashboardPages').GetValue($bulkDisk)
    if (@($bulkDiskPages | Where-Object { $_.Id -eq $bulkOne.Id -or $_.Id -eq $bulkTwo.Id }).Count) { throw 'Done did not persist bulk page deletion' }
    $vm.BeginPageEditing()
    $activeDeleteId = $vm.SelectedPage.Id
    $extraBulk = [LandingPage.Models.DashboardPages]::Add($vm.Settings.Settings,'Active bulk deletion companion')
    $pageDeleteList.UnselectAll(); $pageDeleteList.SelectedItems.Add($vm.SelectedPage); $pageDeleteList.SelectedItems.Add($extraBulk)
    $vm.RequestDeleteSelectedPagesCommand.Execute($null); $vm.DeleteSelectedPagesCommand.Execute($null)
    $bulkDeadline = [datetime]::UtcNow.AddSeconds(15)
    while (@($vm.Pages | Where-Object Id -eq $activeDeleteId).Count -and [datetime]::UtcNow -lt $bulkDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
    if ($vm.SelectedPage.Id -eq $activeDeleteId -or $vm.Pages.Contains($extraBulk) -or !@($vm.Pages | Where-Object Id -eq $vm.SelectedPage.Id)) { throw 'Deleting the active page did not switch to a surviving page first' }
    Wait-Task ($vm.EndPageEditingAsync($false))
    if (!@($vm.Pages | Where-Object Id -eq $activeDeleteId)) { throw 'Cancelling active bulk deletion did not restore the deleted page' }
    $view.GetType().GetMethod('ClosePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    'PASS: real multi-select page list, inactive deletion without navigation, confirmation cancel, all-pages protection, Done persistence, active-page fallback and editor rollback.'
    $quickBefore = $vm.Pages.Count
    $vm.QuickAddPageCommand.Execute($null)
    $quickDeadline = [datetime]::UtcNow.AddSeconds(15)
    while ($vm.IsSwitchingPage -and [datetime]::UtcNow -lt $quickDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
    $quickPage = $vm.SelectedPage
    if ($vm.Pages.Count -ne $quickBefore + 1 -or $quickPage.Layout.Children.Count -ne 2 -or $quickPage.Layout.Children[0].ViewProperties.ViewId -ne 'ToolbarWidget') { throw 'Quick add did not create and select the default two-panel page' }
    $quickDisk = [Newtonsoft.Json.JsonConvert]::DeserializeObject([IO.File]::ReadAllText($savedFile.FullName),[LandingPage.LandingPageSettings])
    if (!@([LandingPage.LandingPageSettings].GetProperty('DashboardPages').GetValue($quickDisk) | Where-Object Id -eq $quickPage.Id)) { throw 'Quick add was not saved immediately' }
    $quickToolbar = [LandingPage.Models.Layout.ViewProperties].GetField('view',$flags).GetValue($quickPage.Layout.Children[0].ViewProperties)
    $quickButtons = @([ToolbarTestVisuals]::Buttons($quickToolbar))
    if (!@($quickButtons | Where-Object { $_.ToolTip -eq 'Add dashboard page' -and [object]::ReferenceEquals($_.Command,$vm.QuickAddPageCommand) }) -or !@($quickButtons | Where-Object { $_.ToolTip -eq 'Remove current dashboard page' -and [object]::ReferenceEquals($_.Command,$vm.QuickRemovePageCommand) })) { throw 'Quick page commands are not connected to default toolbar buttons' }
    $vm.QuickRemovePageCommand.Execute($null)
    if ($view.FindName('QuickDeletePageOverlay').Visibility -ne [Windows.Visibility]::Visible -or $vm.Pages.Count -ne $quickBefore + 1) { throw 'Quick remove deleted without in-app confirmation' }
    [void]$view.GetType().GetMethod('CancelQuickPageDeletion_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    if ($view.FindName('QuickDeletePageOverlay').Visibility -ne [Windows.Visibility]::Collapsed -or !$vm.Pages.Contains($quickPage)) { throw 'Quick-remove cancellation changed the page' }
    $vm.QuickRemovePageCommand.Execute($null)
    $quickConfirm = $view.FindName('QuickDeletePageConfirmButton')
    [void]$quickConfirm.GetType().GetMethod('OnClick',$flags).Invoke($quickConfirm,@())
    $quickDeadline = [datetime]::UtcNow.AddSeconds(15)
    while ($vm.Pages.Contains($quickPage) -and [datetime]::UtcNow -lt $quickDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
    if ($vm.Pages.Count -ne $quickBefore -or $vm.SelectedPage.Id -eq $quickPage.Id) { throw 'Quick remove did not delete and select a surviving page' }
    $quickDisk = [Newtonsoft.Json.JsonConvert]::DeserializeObject([IO.File]::ReadAllText($savedFile.FullName),[LandingPage.LandingPageSettings])
    if (@([LandingPage.LandingPageSettings].GetProperty('DashboardPages').GetValue($quickDisk) | Where-Object Id -eq $quickPage.Id)) { throw 'Quick removal was not saved immediately' }
    $vm.BeginPageEditing()
    if ($vm.QuickAddPageCommand.CanExecute($null) -or $vm.QuickRemovePageCommand.CanExecute($null)) { throw 'Quick actions can bypass pending page-manager edits' }
    Wait-Task ($vm.EndPageEditingAsync($false))
    'PASS: quick add/remove default toolbar bindings, immediate persistence, in-app confirmation/cancel, surviving-page navigation and pending-editor protection.'
    $orderExtraA = [LandingPage.Models.DashboardPages]::Add($vm.Settings.Settings,'Reorder A')
    $orderExtraB = [LandingPage.Models.DashboardPages]::Add($vm.Settings.Settings,'Reorder B')
    $vm.SaveCurrentPage()
    $orderBefore = @($vm.Pages | ForEach-Object Id); $orderActive = $vm.SelectedPage.Id; $orderRoot = $vm.RootNodeViewModel
    $view.ShowPages(); $window.UpdateLayout()
    $orderList = $view.FindName('PageOrderList'); $orderList.SelectedItem = $orderExtraB
    if ($vm.PageSelectedForOrdering.Id -ne $orderExtraB.Id -or $vm.SelectedPage.Id -ne $orderActive) { throw 'Selecting a page for ordering opened it' }
    $vm.MoveOrderedPageFirstCommand.Execute($null)
    if ($vm.Pages[0].Id -ne $orderExtraB.Id -or $vm.MoveOrderedPageFirstCommand.CanExecute($null) -or $vm.MoveOrderedPageUpCommand.CanExecute($null) -or ![object]::ReferenceEquals($orderRoot,$vm.RootNodeViewModel)) { throw 'Move to first failed or changed the active dashboard' }
    $vm.MoveOrderedPageDownCommand.Execute($null)
    if ($vm.Pages[1].Id -ne $orderExtraB.Id) { throw 'Move down did not reorder the highlighted page' }
    $vm.MoveOrderedPageUpCommand.Execute($null); $vm.MoveOrderedPageLastCommand.Execute($null)
    if ($vm.Pages[$vm.Pages.Count-1].Id -ne $orderExtraB.Id -or $vm.MoveOrderedPageLastCommand.CanExecute($null)) { throw 'Move to last or its boundary state failed' }
    Wait-Task ($vm.EndPageEditingAsync($false))
    if ((@($vm.Pages | ForEach-Object Id) -join ',') -ne ($orderBefore -join ',')) { throw 'Dismissing the manager did not restore page order' }
    $vm.BeginPageEditing(); $orderList.SelectedItem = @($vm.Pages | Where-Object Id -eq $orderExtraB.Id)[0]
    $vm.MoveOrderedPageFirstCommand.Execute($null)
    Wait-Task ($vm.EndPageEditingAsync($true))
    $orderDisk = [Newtonsoft.Json.JsonConvert]::DeserializeObject([IO.File]::ReadAllText($savedFile.FullName),[LandingPage.LandingPageSettings])
    if ([LandingPage.LandingPageSettings].GetProperty('DashboardPages').GetValue($orderDisk)[0].Id -ne $orderExtraB.Id -or $vm.SelectedPage.Id -ne $orderActive) { throw 'Done did not persist page order while preserving the active page' }
    [void]$view.GetType().GetMethod('ClosePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    'PASS: independent page-order selection, up/down/first/last, immediate boundaries, unchanged active widgets, dismissal rollback and saved order.'
    $fitOriginalPadding = Get-Setting $vm.Settings.Settings 'Padding'; $fitOriginalFixed = Get-Setting $vm.Settings.Settings 'FixGridSize'; $fitOriginalHeight = Get-Setting $vm.Settings.Settings 'FixedGridHeight'
    $fitWindowHeight = $window.Height
    try {
        Set-Setting $vm.Settings.Settings 'FixGridSize' $false
        Set-Setting $vm.Settings.Settings 'Padding' ([double]12)
        $fitViewport = $view.FindName('StartPageScrollViewer')
        foreach ($height in @(500,800,1100)) {
            $window.Height = $height; $window.UpdateLayout()
            $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
            if ($fitViewport.ScrollableHeight -gt 0.5 -or $fitViewport.ComputedVerticalScrollBarVisibility -ne [Windows.Visibility]::Collapsed) { throw ('Automatic dashboard height includes outer padding and creates fake overflow at ' + $height + ': viewport=' + $fitViewport.ViewportHeight + '; extent=' + $fitViewport.ExtentHeight) }
        }
        Set-Setting $vm.Settings.Settings 'FixedGridHeight' ([double]1600); Set-Setting $vm.Settings.Settings 'FixGridSize' $true
        $window.Height = 700; $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout()
        if ($fitViewport.ScrollableHeight -lt 500 -or $fitViewport.ComputedVerticalScrollBarVisibility -ne [Windows.Visibility]::Visible) { throw 'Deliberately tall fixed dashboards lost scrolling' }
    } finally {
        Set-Setting $vm.Settings.Settings 'Padding' $fitOriginalPadding; Set-Setting $vm.Settings.Settings 'FixedGridHeight' $fitOriginalHeight; Set-Setting $vm.Settings.Settings 'FixGridSize' $fitOriginalFixed
        $window.Height = $fitWindowHeight; $window.UpdateLayout()
    }
    'PASS: automatic dashboard fits its inner viewport with padding at three window heights; fixed oversized canvases still scroll.'
    $beforeFilterGames = @([DashboardTestProxy]::FeaturedFixtureGames)
    $refillShelf = $null
    try {
        [DashboardTestProxy]::FeaturedFixtureGames.Clear()
        $nsfwTag = [guid]::NewGuid()
        for ($index = 0; $index -lt 30; $index++) {
            $candidate = [Playnite.SDK.Models.Game]::new(); $candidate.Name = 'Refill ' + $index.ToString('00')
            $candidate.TagIds = [Collections.Generic.List[guid]]::new()
            if ($index -lt 10) { $candidate.TagIds.Add($nsfwTag) }
            [DashboardTestProxy]::FeaturedFixtureGames.Add($candidate)
        }
        $refillRules = [LandingPage.Models.ShelveProperties]::new(); $refillRules.NumberOfGames = 20
        $refillRules.SortBy = [LandingPage.Models.SortingField]::Name; $refillRules.Order = [LandingPage.Models.Order]::Ascending
        $refillShelf = [LandingPage.ViewModels.ShelveViewModel].GetConstructors()[0].Invoke([object[]]@($refillRules,[LandingPage.Settings.ShelvesSettings]::new(),$api,[Collections.ObjectModel.ObservableCollection[LandingPage.ViewModels.ShelveViewModel]]::new(),$null))
        $refillShelf.UpdateGames($refillRules,$true)
        if ($refillShelf.Games.Count -ne 20) { throw 'Refill fixture did not initially contain 20 games' }
        $excludeNsfw = [LandingPage.ViewModels.ShelveViewModel+ToggleFilter]::new($nsfwTag,'NSFW',$refillRules.ExcludedTags,$refillShelf,$false)
        $script:filterViewResets = 0
        $filterViewListener = [Collections.Specialized.NotifyCollectionChangedEventHandler]{ param($sender,$change) if ($change.Action -eq 'Reset') { $script:filterViewResets++ } }
        $refillShelf.CollectionViewSource.View.add_CollectionChanged($filterViewListener)
        $excludeNsfw.IsChecked = $true
        if ($script:filterViewResets -ne 1) { throw 'Filter replacement did not refresh the displayed collection as one batch' }
        if ($refillShelf.Games.Count -ne 20 -or @($refillShelf.Games | Where-Object { $_.Game.TagIds.Contains($nsfwTag) }).Count -ne 0 -or @($refillShelf.CollectionViewSource.View).Count -ne 20) { throw 'Tag exclusion removed games from the limited window instead of refilling it to 20 eligible games' }
        $script:filterViewResets = 0
        $refillShelf.UpdateGames($refillRules,$true)
        if ($script:filterViewResets -ne 0) { throw 'Unchanged manual refresh reset all shelf cards unnecessarily' }
        $excludeNsfw.IsChecked = $false
        if ($refillShelf.Games.Count -ne 20) { throw 'Removing an exclusion changed the configured shelf count' }
    } finally {
        if ($refillShelf) { $refillShelf.OnViewClosed() }
        [DashboardTestProxy]::FeaturedFixtureGames.Clear()
        foreach ($originalGame in $beforeFilterGames) { [DashboardTestProxy]::FeaturedFixtureGames.Add($originalGame) }
    }
    'PASS: excluding ten NSFW-tagged games from a 20-game shelf backfills from later matches and retains 20 models/view entries.'
    $deletablePage = [LandingPage.Models.DashboardPages]::Add($vm.Settings.Settings,'Deletion rollback fixture')
    Wait-Task ($vm.SwitchPageAsync($deletablePage))
    $countBeforePendingDelete = $vm.Pages.Count
    $vm.BeginPageEditing(); $vm.ConfirmPageDeletion = $true; $vm.DeletePageCommand.Execute($null)
    $deleteDeadline = [datetime]::UtcNow.AddSeconds(10)
    while(@($vm.Pages | Where-Object Id -eq $deletablePage.Id).Count -gt 0 -and [datetime]::UtcNow -lt $deleteDeadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 5 }
    if($vm.Pages.Count -ne $countBeforePendingDelete - 1) { throw 'Pending deletion did not run' }
    Wait-Task ($vm.EndPageEditingAsync($false))
    $restoredDeleted = @($vm.Pages | Where-Object Id -eq $deletablePage.Id)[0]
    if(!$restoredDeleted -or $vm.Pages.Count -ne $countBeforePendingDelete) { throw 'Dismiss failed to restore a deleted page' }
    Wait-Task ($vm.SwitchPageAsync($restoredDeleted))
    if($vm.RootNodeViewModel.GridNode.Children[0].ViewProperties.ViewId -ne 'ToolbarWidget') { throw 'Restored deleted page could not rebuild its toolbar layout' }
    'PASS: Done-only rename, cancellation of background edits/page additions/deletions, unchanged layout reuse, and fitted toolbar/content defaults.'

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
