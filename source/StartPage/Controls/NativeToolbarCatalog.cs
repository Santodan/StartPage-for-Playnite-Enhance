using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace LandingPage.Controls
{
    public class ToolbarOption : ObservableObjectExt
    {
        public string Id { get; set; }
        private string title;
        public string Title { get => title; set => SetValue(ref title, value); }
        public string Glyph { get; set; }
        public string Kind { get; set; } = "button";
        private DataTemplate iconTemplate;
        public DataTemplate IconTemplate { get => iconTemplate; set => SetValue(ref iconTemplate, value); }
        public bool IsNativeFont { get; set; }
        public string[] Aliases { get; set; } = new string[0];
        private FrameworkElement nativeControl;
        public FrameworkElement NativeControl { get => nativeControl; set => SetValue(ref nativeControl, value); }
        public ICommand Command { get; set; }
        public object CommandParameter { get; set; }
        public object NativeModel { get; set; }
        public SidebarItem SidebarItem { get; set; }
    }

    public class ToolbarExtensionSource
    {
        public TopPanelItem PanelItem { get; }
        public string Title => PanelItem.Title;
        public ICommand Command { get; }
        public object IconObject => ResolveIcon(PanelItem.Icon);
        public static object ResolveIcon(object icon)
        {
            var helpers = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Playnite.SdkHelpers", false)).FirstOrDefault(type => type != null);
            var resolver = helpers?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "ResolveUiItemIcon" && method.GetParameters().Length == 2);
            try { return resolver?.Invoke(null, new object[] { icon, null }) ?? icon; }
            catch (Exception) { return icon; }
        }
        public ToolbarExtensionSource(TopPanelItem item)
        {
            PanelItem = item;
            Command = new RelayCommand(() => item.Activated?.Invoke(), () => item.Activated != null);
        }
    }

    public class ToolbarSidebarSource
    {
        public SidebarItem SideItem { get; }
        public object IconObject => ToolbarExtensionSource.ResolveIcon(SideItem.Icon);
        public ToolbarSidebarSource(SidebarItem item) { SideItem = item; }
    }

    public class ToolbarIcon : ContentControl
    {
        public static readonly DependencyProperty OptionProperty = DependencyProperty.Register(nameof(Option), typeof(ToolbarOption), typeof(ToolbarIcon),
            new PropertyMetadata(null, (sender, args) => ((ToolbarIcon)sender).Changed(args.OldValue as ToolbarOption)));
        public ToolbarOption Option { get => (ToolbarOption)GetValue(OptionProperty); set => SetValue(OptionProperty, value); }
        private ToolbarOption observed;
        public ToolbarIcon()
        {
            Width = Height = 22;
            IsHitTestVisible = false;
            SetResourceReference(ForegroundProperty, "TextBrush");
            Loaded += (sender, args) => Observe();
            Unloaded += (sender, args) => { if (observed != null) observed.PropertyChanged -= OptionChanged; observed = null; };
        }
        private void Changed(ToolbarOption old)
        {
            if (old != null) old.PropertyChanged -= OptionChanged;
            Observe();
        }
        private void Observe()
        {
            if (observed != null) observed.PropertyChanged -= OptionChanged;
            observed = Option;
            if (observed != null) observed.PropertyChanged += OptionChanged;
            RenderIcon();
        }
        private void OptionChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args) => RenderIcon();

        private static object FindIconVisual(DependencyObject visual)
        {
            if (visual == null) return null;
            if (visual is Image || visual is Path) return visual;
            if (visual is TextBlock text && !string.IsNullOrEmpty(text.Text)
                && (text.Text.Any(ch => ch >= '\uE000' && ch <= '\uF8FF')
                    || text.FontFamily.Source.IndexOf("Ico", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.FontFamily.Source.IndexOf("Awesome", StringComparison.OrdinalIgnoreCase) >= 0)) return text;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(visual); ++i)
            {
                var icon = FindIconVisual(VisualTreeHelper.GetChild(visual, i));
                if (icon != null) return icon;
            }
            return null;
        }

        internal void RenderIcon()
        {
            if (Option == null) { Content = null; return; }
            var native = Option.NativeControl as ContentControl;
            var template = native?.ContentTemplate ?? Option.IconTemplate;
            if (template != null)
            {
                Content = new ContentControl { ContentTemplate = template, Content = native?.Content ?? native?.DataContext };
                return;
            }
            (Option.NativeControl as Control)?.ApplyTemplate();
            var icon = NativeToolbarCatalog.ReadMember(native?.DataContext, "IconObject")
                ?? native?.Content
                ?? NativeToolbarCatalog.ReadMember(NativeToolbarCatalog.ReadMember(native?.DataContext, "PanelItem"), "Icon")
                ?? ToolbarExtensionSource.ResolveIcon(NativeToolbarCatalog.ReadMember(NativeToolbarCatalog.ReadMember(native?.DataContext, "SideItem"), "Icon"))
                ?? FindIconVisual(Option.NativeControl);
            FrameworkElement rendered = null;
            if (icon is TextBlock glyph)
            {
                var copy = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                copy.SetBinding(TextBlock.TextProperty, new Binding(nameof(glyph.Text)) { Source = glyph });
                copy.SetBinding(TextBlock.FontFamilyProperty, new Binding(nameof(glyph.FontFamily)) { Source = glyph });
                copy.SetBinding(TextBlock.FontWeightProperty, new Binding(nameof(glyph.FontWeight)) { Source = glyph });
                copy.SetBinding(TextBlock.FontStyleProperty, new Binding(nameof(glyph.FontStyle)) { Source = glyph });
                copy.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(glyph.FontSize)) { Source = glyph });
                copy.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                rendered = copy;
            }
            else if (icon is Image image) rendered = new Image { Source = image.Source, Stretch = Stretch.Uniform };
            else if (icon is ImageSource imageSource) rendered = new Image { Source = imageSource, Stretch = Stretch.Uniform };
            else if (icon is Path path) rendered = new Path { Data = path.Data, Fill = path.Fill, Stroke = path.Stroke, StrokeThickness = path.StrokeThickness, Stretch = Stretch.Uniform };
            else if (icon is Geometry geometry) rendered = new Path { Data = geometry, Fill = TryFindResource("TextBrush") as Brush ?? Brushes.White, Stretch = Stretch.Uniform };
            else if (icon is FrameworkElement element)
            {
                // Render arbitrary extension visuals without reparenting their controls.
                rendered = new Rectangle { Fill = new VisualBrush(element) { Stretch = Stretch.Uniform } };
            }
            else if (icon is string text)
            {
                if (System.IO.File.Exists(text))
                {
                    try { rendered = new Image { Source = new BitmapImage(new Uri(System.IO.Path.GetFullPath(text))), Stretch = Stretch.Uniform }; }
                    catch (Exception) { }
                }
                else if (text.Length <= 4) rendered = new TextBlock { Text = text, FontFamily = TryFindResource("FontIcoFont") as FontFamily ?? new FontFamily("Segoe UI Symbol"), FontSize = 20 };
            }
            if (rendered == null && !string.IsNullOrEmpty(Option.Glyph))
            {
                var fallback = new TextBlock { Text = Option.Glyph, FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
                fallback.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                if (Option.IsNativeFont) fallback.SetResourceReference(TextBlock.FontFamilyProperty, "FontIcoFont");
                else if (Option.Id == "native.details" || Option.Id == "native.grid" || Option.Id == "native.list") fallback.FontFamily = new FontFamily("Segoe UI Symbol");
                rendered = fallback;
            }
            Content = rendered == null ? null : new Viewbox { Child = rendered, Stretch = Stretch.Uniform };
        }
    }

    // Uses existing controls and commands rather than depending on DesktopApp assemblies
    // or duplicating Playnite's sorting/grouping/filter-preset menu implementation.
    public static class NativeToolbarCatalog
    {
        private static readonly Dictionary<Plugin, List<TopPanelItem>> providerItems = new Dictionary<Plugin, List<TopPanelItem>>();
        private static readonly Dictionary<Plugin, List<SidebarItem>> sidebarProviderItems = new Dictionary<Plugin, List<SidebarItem>>();
        public static void RefreshProviders() { providerItems.Clear(); sidebarProviderItems.Clear(); }

        public static void AddProviderOptions(List<ToolbarOption> options, IEnumerable<Plugin> providers)
        {
            var owners = new Dictionary<string, int>();
            var titles = new Dictionary<string, int>();
            foreach (var provider in providers ?? Enumerable.Empty<Plugin>())
            {
                List<TopPanelItem> items;
                if (!providerItems.TryGetValue(provider, out items))
                {
                    try { items = provider.GetTopPanelItems()?.Where(item => item != null).ToList() ?? new List<TopPanelItem>(); }
                    catch (Exception ex) { LandingPageExtension.logger?.Debug(ex, "Toolbar provider is not ready: " + provider.GetType().FullName); continue; }
                    // An empty early-startup response can be retried when the widget polls.
                    if (items.Count > 0) providerItems[provider] = items;
                }
                foreach (var item in items)
                {
                    var owner = item.Activated?.Method.DeclaringType?.Assembly.GetName().Name ?? provider.GetType().Assembly.GetName().Name;
                    owners.TryGetValue(owner, out var ordinal); owners[owner] = ordinal + 1;
                    var id = "extension." + Uri.EscapeDataString(owner) + "." + ordinal;
                    var title = string.IsNullOrWhiteSpace(item.Title) ? owner : item.Title;
                    titles.TryGetValue(title, out var titleIndex); titles[title] = titleIndex + 1;
                    var legacyId = "plugin." + Uri.EscapeDataString(title) + "." + titleIndex;
                    if (options.Any(option => option.Id == id || option.Aliases.Contains(legacyId))) continue;
                    var wrapper = new ToolbarExtensionSource(item);
                    var source = new Button { DataContext = wrapper, Command = wrapper.Command };
                    options.Add(new ToolbarOption
                    {
                        Id = id, Title = "Extension: " + title, Aliases = new[] { legacyId }, NativeControl = source,
                        Command = wrapper.Command, Glyph = "\u25C7"
                    });
                }
            }
        }

        private static IEnumerable<object> SidebarModels(object preferred)
        {
            if (preferred != null) yield return preferred;
            if (Application.Current != null)
                foreach (Window window in Application.Current.Windows)
                    if (window.DataContext != null && !ReferenceEquals(window.DataContext, preferred)) yield return window.DataContext;
        }

        private static string SidebarOwner(SidebarItem item, string fallback)
            => ActivationOwner(item.Opened) ?? ActivationOwner(item.Activated) ?? fallback;

        private static IEnumerable<Tuple<string, object, SidebarItem, ICommand>> NativeSidebarEntries(object model)
        {
            var entries = ReadMember(model, "SidebarItems") as System.Collections.IEnumerable;
            if (entries == null) yield break;
            var owners = new Dictionary<string, int>();
            foreach (var wrapper in entries)
            {
                if (!(ReadMember(wrapper, "SideItem") is SidebarItem item)) continue;
                var owner = SidebarOwner(item, "Playnite");
                owners.TryGetValue(owner, out var ordinal); owners[owner] = ordinal + 1;
                var id = "sidebar." + Uri.EscapeDataString(owner) + "." + ordinal;
                var command = ReadMember(wrapper, "Command") as ICommand ?? ReadMember(model, "SelectSidebarViewCommand") as ICommand;
                yield return Tuple.Create(id, wrapper, item, command);
            }
        }

        private static Tuple<ICommand, object> SidebarRoute(string id, object model)
        {
            foreach (var candidate in SidebarModels(model))
                foreach (var entry in NativeSidebarEntries(candidate))
                    if (entry.Item1 == id && entry.Item4 != null) return Tuple.Create(entry.Item4, entry.Item2);
            return Tuple.Create<ICommand, object>(null, null);
        }

        public static void AddSidebarOptions(List<ToolbarOption> options, object preferredModel, IEnumerable<Plugin> providers)
        {
            foreach (var model in SidebarModels(preferredModel))
            {
                var entries = NativeSidebarEntries(model).ToList();
                if (entries.Count == 0) continue;
                foreach (var entry in entries)
                {
                    var id = entry.Item1;
                    if (options.Any(option => option.Id == id)) continue;
                    var source = new Button { DataContext = entry.Item2 };
                    var command = new RelayCommand(() => { var route = SidebarRoute(id, model); if (route.Item1?.CanExecute(route.Item2) == true) route.Item1.Execute(route.Item2); },
                        () => { var route = SidebarRoute(id, model); return route.Item1?.CanExecute(route.Item2) == true; });
                    options.Add(new ToolbarOption { Id = id, Title = "Sidebar: " + (entry.Item3.Title ?? "Extension page"),
                        NativeControl = source, NativeModel = model, SidebarItem = entry.Item3, Command = command });
                }
                break;
            }
            // Also fill gaps while native sidebar wrappers are only partially initialized.
            var owners = new Dictionary<string, int>();
            foreach (var provider in providers ?? Enumerable.Empty<Plugin>())
            {
                if (!sidebarProviderItems.TryGetValue(provider, out var items))
                {
                    try { items = provider.GetSidebarItems()?.Where(item => item != null).ToList() ?? new List<SidebarItem>(); }
                    catch (Exception ex) { LandingPageExtension.logger?.Debug(ex, "Sidebar provider is not ready: " + provider.GetType().FullName); continue; }
                    if (items.Count > 0) sidebarProviderItems[provider] = items;
                }
                foreach (var item in items)
                {
                    var owner = SidebarOwner(item, provider.GetType().Assembly.GetName().Name);
                    owners.TryGetValue(owner, out var ordinal); owners[owner] = ordinal + 1;
                    var id = "sidebar." + Uri.EscapeDataString(owner) + "." + ordinal;
                    if (options.Any(option => option.Id == id)) continue;
                    var command = new RelayCommand(() => { var route = SidebarRoute(id, preferredModel); if (route.Item1?.CanExecute(route.Item2) == true) route.Item1.Execute(route.Item2); },
                        () => { var route = SidebarRoute(id, preferredModel); return route.Item1?.CanExecute(route.Item2) == true; });
                    options.Add(new ToolbarOption { Id = id, Title = "Sidebar: " + (item.Title ?? owner), SidebarItem = item,
                        NativeControl = new Button { DataContext = new ToolbarSidebarSource(item) }, Command = command, NativeModel = preferredModel });
                }
            }
        }

        private static readonly string[][] nativeItems =
        {
            new[] { "search", "Search", "ButtonSearch", "TopPanelSearchButtonTemplate", "OpenGlobalSearchCommand", "\ued12" },
            new[] { "view-settings", "View settings", "ButtonViewSettings", "TopPanelGeneralViewSettingsTemplate", "", "\uefe2" },
            new[] { "filter-presets", "Filter presets", "ButtonFilterPresets", "TopPanelFilterPresetsSelectionTemplate", "", "\ueec0" },
            new[] { "group", "Group by", "ButtonGroupSettings", "TopPanelGroupSettingsTemplate", "", "\uee03" },
            new[] { "sort", "Sort by", "ButtonSortSettings", "TopPanelSortSettingsTemplate", "", "\uefee" },
            new[] { "details", "Details view", "ButtonSwitchDetailsView", "TopPanelSwitchDetailsViewTemplate", "SwitchDetailsViewCommand", "\uE145" },
            new[] { "grid", "Grid view", "ButtonSwitchGridView", "TopPanelSwitchGridViewTemplate", "SwitchGridViewCommand", "\uE138" },
            new[] { "list", "List view", "ButtonSwitchListView", "TopPanelSwitchListViewTemplate", "SwitchListViewCommand", "\uE14C" },
            new[] { "random", "Select random game", "ButtonSelectRandomGame", "TopPanelSelectRandomGameButtonTemplate", "SelectRandomGameCommand", "\uef03" },
            new[] { "random-visible", "Select random game in current view", "ButtonViewSelectRandomGame", "TopPanelViewSelectRandomGameButtonTemplate", "ViewSelectRandomGameCommand", "\uefcf" },
            new[] { "explorer", "Explorer panel", "ButtonExplorerSwitch", "TopPanelExplorerSwitchTemplate", "ToggleExplorerPanelCommand", "\ueeed" },
            new[] { "update", "Playnite updates", "", "TopPanelUpdateButtonTemplate", "CheckForUpdateCommand", "\uef08" }
        };

        public static object ReadMember(object source, string name)
        {
            if (source == null || string.IsNullOrEmpty(name)) return null;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = source.GetType();
            try { return type.GetProperty(name, flags)?.GetValue(source) ?? type.GetField(name, flags)?.GetValue(source); }
            catch (Exception) { return null; } // Theme/version/provider-specific members are optional.
        }

        public static FrameworkElement FindTopPanel(DependencyObject root)
        {
            if (root == null) return null;
            if (root is FrameworkElement element && root.GetType().FullName == "Playnite.DesktopApp.Controls.Views.TopPanel")
            { (element as Control)?.ApplyTemplate(); return element; }
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
            {
                var found = FindTopPanel(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }

        public static FrameworkElement FindNativeTopPanel(DependencyObject scope)
        {
            var found = FindTopPanel(scope);
            if (found != null || Application.Current == null) return found;
            foreach (Window window in Application.Current.Windows)
            {
                if (ReferenceEquals(window, scope)) continue;
                found = FindTopPanel(window);
                if (found != null) return found;
            }
            return null;
        }

        public static string ActivationOwner(object action)
            => (action as Delegate)?.Method.DeclaringType?.Assembly.GetName().Name;

        private static string ExtensionOwner(ToolbarOption option)
            => ActivationOwner(ReadMember(ReadMember(option.NativeControl?.DataContext, "PanelItem"), "Activated"));

        private static bool TryPageRoute(ToolbarOption option, out ICommand command, out object parameter)
        {
            command = null; parameter = null;
            var owner = ExtensionOwner(option);
            if (owner == null) return false;
            var candidates = new List<object> { option.NativeModel, ReadMember(option.NativeControl?.DataContext, "model") };
            if (Application.Current != null) candidates.AddRange(Application.Current.Windows.OfType<Window>().Select(window => window.DataContext));
            foreach (var model in candidates.Where(model => model != null).Distinct())
            {
                var pages = ReadMember(model, "SidebarItems") as System.Collections.IEnumerable;
                var select = ReadMember(model, "SelectSidebarViewCommand") as ICommand;
                if (pages == null || select == null) continue;
                foreach (var wrapper in pages)
                {
                    var item = ReadMember(wrapper, "SideItem") as SidebarItem;
                    if (item == null || item.Opened == null) continue;
                    if (!string.Equals(ActivationOwner(item.Opened), owner, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(ActivationOwner(item.Activated), owner, StringComparison.OrdinalIgnoreCase)) continue;
                    command = select; parameter = wrapper;
                    return true;
                }
            }
            return false;
        }

        public static ICommand ExtensionCommand(ToolbarOption option, LandingPage.Settings.ExtensionOpenMode mode)
        {
            // Preserve the provider's existing overlay action only for integrations
            // verified to support it. Never reparent arbitrary extension page controls.
            if (option.SidebarItem != null) return option.Command;
            var supportsOverlay = string.Equals(ExtensionOwner(option), "PlayniteAchievements", StringComparison.OrdinalIgnoreCase);
            Func<Tuple<ICommand, object>> route = () =>
            {
                if ((mode == LandingPage.Settings.ExtensionOpenMode.ExtensionPage || !supportsOverlay)
                    && TryPageRoute(option, out var pageCommand, out var pageParameter)) return Tuple.Create(pageCommand, pageParameter);
                return Tuple.Create(option.Command, option.CommandParameter);
            };
            return new RelayCommand(() =>
            {
                var target = route();
                if (target.Item1?.CanExecute(target.Item2) == true) target.Item1.Execute(target.Item2);
            }, () => { var target = route(); return target.Item1?.CanExecute(target.Item2) == true; });
        }

        public static ToolbarOption Resolve(IEnumerable<ToolbarOption> options, string id)
            => options.FirstOrDefault(option => option.Id == id) ?? options.FirstOrDefault(option => option.Aliases.Contains(id));

        public static ToolbarOption WithId(ToolbarOption source, string id) => new ToolbarOption
        {
            Id = id, Title = source.Title, Glyph = source.Glyph, Kind = source.Kind, NativeControl = source.NativeControl,
            NativeModel = source.NativeModel, Command = source.Command, CommandParameter = source.CommandParameter,
            IconTemplate = source.IconTemplate, IsNativeFont = source.IsNativeFont, Aliases = source.Aliases, SidebarItem = source.SidebarItem
        };

        public static List<ToolbarOption> Discover(FrameworkElement topPanel, object nativeModel = null)
        {
            var options = new List<ToolbarOption>
            {
                new ToolbarOption { Id = "page.previous", Title = "Previous dashboard page", Glyph = "\u25C0" },
                new ToolbarOption { Id = "page.select", Title = "Dashboard page selector", Kind = "pages" },
                new ToolbarOption { Id = "page.next", Title = "Next dashboard page", Glyph = "\u25B6" },
                new ToolbarOption { Id = "page.manage", Title = "Manage dashboard pages", Glyph = "\u25A4" },
                new ToolbarOption { Id = "separator", Title = "Separator", Kind = "separator" }
            };
            var panel = ReadMember(topPanel, "PanelMainItems") as Panel;
            var controls = panel?.Children.OfType<FrameworkElement>().ToList() ?? new List<FrameworkElement>();
            foreach (var spec in nativeItems)
            {
                var wrapper = ReadMember(topPanel, spec[2]);
                var template = topPanel?.TryFindResource(spec[3]) ?? Application.Current?.TryFindResource(spec[3]);
                var source = controls.FirstOrDefault(control => wrapper != null && ReferenceEquals(control.DataContext, wrapper))
                    ?? controls.OfType<ContentControl>().FirstOrDefault(control => template != null && ReferenceEquals(control.ContentTemplate, template));
                var commandSource = source as ICommandSource;
                var command = commandSource?.Command ?? ReadMember(wrapper, "Command") as ICommand
                    ?? ReadMember(nativeModel, spec[4]) as ICommand;
                options.Add(new ToolbarOption
                {
                    Id = "native." + spec[0], Title = ReadMember(wrapper, "Title") as string ?? spec[1], Glyph = spec[5],
                    NativeControl = source, Command = command, CommandParameter = commandSource?.CommandParameter, NativeModel = nativeModel,
                    IconTemplate = template as DataTemplate, IsNativeFont = spec[0] != "details" && spec[0] != "grid" && spec[0] != "list"
                });
            }
            options.Add(new ToolbarOption { Id = "native.main-menu", Title = "Playnite main menu", Glyph = "\u2630", NativeControl = ReadMember(topPanel, "ElemMainMenu") as FrameworkElement });
            options.Add(new ToolbarOption { Id = "native.search-box", Title = "Library search box", Kind = "search", NativeControl = ReadMember(topPanel, "TextMainSearch") as FrameworkElement });
            options.Add(new ToolbarOption { Id = "native.filters", Title = "Filters panel", Glyph = "\uef29", IsNativeFont = true, Kind = "toggle", NativeControl = ReadMember(topPanel, "ToggleFilter") as FrameworkElement, NativeModel = nativeModel });
            options.Add(new ToolbarOption { Id = "native.notifications", Title = "Notifications panel", Glyph = "\ueea3", IsNativeFont = true, Kind = "toggle", NativeControl = ReadMember(topPanel, "ToggleNotifications") as FrameworkElement, NativeModel = nativeModel });
            options.Add(new ToolbarOption { Id = "native.progress", Title = "Global operation progress", Kind = "progress", NativeControl = ReadMember(topPanel, "ProgressGlobal") as FrameworkElement, NativeModel = nativeModel });
            var cancel = ReadMember(topPanel, "ButtonProgressCancel") as FrameworkElement;
            options.Add(new ToolbarOption { Id = "native.cancel-progress", Title = "Cancel current operation", Glyph = "\u00D7", NativeControl = cancel,
                Command = (cancel as ICommandSource)?.Command ?? ReadMember(nativeModel, "CancelProgressCommand") as ICommand });
            options.Add(new ToolbarOption { Id = "native.progress-text", Title = "Global operation status", Kind = "status", NativeControl = ReadMember(topPanel, "TextProgressText") as FrameworkElement });
            var plugins = ReadMember(topPanel, "PanelMainPluginItems") as Panel;
            var seen = new Dictionary<string, int>();
            var owners = new Dictionary<string, int>();
            foreach (var source in plugins?.Children.OfType<FrameworkElement>() ?? Enumerable.Empty<FrameworkElement>())
            {
                var title = ReadMember(source.DataContext, "Title") as string ?? source.ToolTip as string;
                if (string.IsNullOrWhiteSpace(title)) continue;
                seen.TryGetValue(title, out var count); seen[title] = count + 1;
                var legacyId = "plugin." + Uri.EscapeDataString(title) + "." + count;
                var activation = ReadMember(ReadMember(source.DataContext, "PanelItem"), "Activated") as Delegate;
                var owner = activation?.Method.DeclaringType?.Assembly.GetName().Name;
                var id = legacyId;
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    owners.TryGetValue(owner, out var ordinal); owners[owner] = ordinal + 1;
                    id = "extension." + Uri.EscapeDataString(owner) + "." + ordinal;
                }
                options.Add(new ToolbarOption
                {
                    Id = id, Aliases = new[] { legacyId }, Title = "Extension: " + title,
                    NativeControl = source, Command = (source as ICommandSource)?.Command ?? ReadMember(source.DataContext, "Command") as ICommand,
                    CommandParameter = (source as ICommandSource)?.CommandParameter, Glyph = "\u25C7"
                });
            }
            var api = LandingPageExtension.Instance?.PlayniteApi ?? API.Instance;
            var providers = api?.Addons?.Plugins;
            AddProviderOptions(options, providers);
            AddSidebarOptions(options, nativeModel ?? ReadMember(topPanel, "mainModel"), providers);
            foreach (var option in options.Where(option => option.Id.StartsWith("extension.") || option.Id.StartsWith("plugin.")))
                option.NativeModel = nativeModel ?? ReadMember(topPanel, "mainModel");
            return options;
        }
    }
}
