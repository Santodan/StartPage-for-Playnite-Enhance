using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LandingPage.Settings;
using LandingPage.Controls;
using Playnite.SDK;

namespace LandingPage.Views.Settings
{
    public sealed class AchievementWidgetSettingsView : UserControl
    {
        public static readonly DependencyProperty WidgetIdProperty = DependencyProperty.Register(nameof(WidgetId),typeof(string),typeof(AchievementWidgetSettingsView),
            new PropertyMetadata(null,(sender,e) => ((AchievementWidgetSettingsView)sender).UpdateSourceVisibility()));
        public string WidgetId { get => (string)GetValue(WidgetIdProperty); set => SetValue(WidgetIdProperty,value); }
        private Expander sourceSection, listSection, contentSection, goalsSection, layoutSection, colorsSection;
        private StackPanel listFields;
        private StackPanel goalTarget, recentOptions;
        private ComboBox listSortChoice;
        private FrameworkElement listFilterLabel, listFilterChoice;
        private TextBlock pinnedDescription;
        private FrameworkElement progressLabel, progressChoice;
        private readonly Dictionary<AchievementTextRole,TextSettingsEntry> textStyleFields = new Dictionary<AchievementTextRole,TextSettingsEntry>();
        private readonly List<FrameworkElement> recentTypographyChecks = new List<FrameworkElement>();
        private readonly List<FrameworkElement> graphSizingControls = new List<FrameworkElement>();
        private void UpdateSourceVisibility() {
            var recent = WidgetId == "RecentAchivements";
            foreach (var field in textStyleFields) {
                var role = field.Key;
                var relevant = string.IsNullOrEmpty(WidgetId) || (recent ? role == AchievementTextRole.Description || role == AchievementTextRole.GameName || role == AchievementTextRole.UnlockDate || role == AchievementTextRole.GroupTitle
                    : WidgetId == "GameAchievementList" ? role == AchievementTextRole.Description || role == AchievementTextRole.GameName || role == AchievementTextRole.UnlockState || role == AchievementTextRole.UnlockDate
                    : role == AchievementTextRole.Counts || role == AchievementTextRole.Percentage || role == AchievementTextRole.Remaining || (WidgetId == "AchievementGoals" && role == AchievementTextRole.Goal));
                field.Value.IsAvailable = relevant;
            }
            foreach (var check in recentTypographyChecks) check.Visibility = recent ? Visibility.Visible : Visibility.Collapsed;
            var list = WidgetId == "GameAchievementList" || recent;
            if (listSortChoice != null) listSortChoice.ItemsSource = recent ? new[] { AchievementListOrder.RecentUnlock, AchievementListOrder.Name } : Enum.GetValues(typeof(AchievementListOrder));
            if (layoutSection != null) layoutSection.Visibility = recent ? Visibility.Collapsed : Visibility.Visible;
            if (colorsSection != null) colorsSection.Visibility = recent ? Visibility.Collapsed : Visibility.Visible;
            if (listFields != null) listFields.Visibility = recent ? Visibility.Collapsed : Visibility.Visible;
            if (recentOptions != null) recentOptions.Visibility = recent ? Visibility.Visible : Visibility.Collapsed;
            if (listFilterLabel != null) listFilterLabel.Visibility = recent ? Visibility.Collapsed : Visibility.Visible;
            if (listFilterChoice != null) listFilterChoice.Visibility = recent ? Visibility.Collapsed : Visibility.Visible;
            foreach (var control in graphSizingControls) control.Visibility = list ? Visibility.Collapsed : Visibility.Visible;
            if (sourceSection != null) sourceSection.Visibility = WidgetId == "AchievementGoals" || WidgetId == "GameAchievementList" ? Visibility.Visible : Visibility.Collapsed;
            if (progressLabel != null) progressLabel.Visibility = list ? Visibility.Collapsed : Visibility.Visible;
            if (progressChoice != null) progressChoice.Visibility = list ? Visibility.Collapsed : Visibility.Visible;
            if (listSection != null) listSection.Visibility = list ? Visibility.Visible : Visibility.Collapsed;
            if (contentSection != null) contentSection.Visibility = list ? Visibility.Collapsed : Visibility.Visible;
            if (goalTarget != null) goalTarget.Visibility = list ? Visibility.Collapsed : Visibility.Visible;
            if (goalsSection != null) { goalsSection.Header = list ? "Pinned games" : "Goals"; goalsSection.Visibility = recent ? Visibility.Collapsed : Visibility.Visible; }
            if (pinnedDescription != null) pinnedDescription.Text = list ? "Add games for the pinned game source. Percentage targets are only used by Achievement goals." : "Used by Achievement goals. Add a game, then set its target percentage.";
        }
        private readonly StackPanel body = new StackPanel();
        public AchievementWidgetSettingsView()
        {
            Content = body;
            var source = Section("Game source"); sourceSection = (Expander)source.Parent; UpdateSourceVisibility();
            var sources = new ComboBox { SelectedValuePath = "Tag", Name = "GoalGameSource" };
            foreach (var option in new[] {
                new { Name = "Selected library game", Value = AchievementGoalSource.SelectedGame },
                new { Name = "Hovered StartPage game", Value = AchievementGoalSource.HoveredGame },
                new { Name = "Hovered game, otherwise selected game", Value = AchievementGoalSource.HoveredOrSelectedGame },
                new { Name = "Pinned games", Value = AchievementGoalSource.PinnedGames } })
                sources.Items.Add(new ComboBoxItem { Content = option.Name, Tag = option.Value });
            sources.SetBinding(ComboBox.SelectedValueProperty,Bind("GameSource")); source.Children.Add(sources);
            Check(source,"Keep last hovered game when leaving the shelf","KeepLastHoveredGame");
            goalTarget = new StackPanel(); source.Children.Add(goalTarget);
            Number(goalTarget,"Default target percentage (selected / hovered game)","TargetPercent");
            Hint(goalTarget,"For Achievement goals. Saved per-game targets take priority over the default target.");
            var listOptions = Section("Achievement list"); listSection = (Expander)listOptions.Parent;
            Choice(listOptions,"Show achievements",typeof(AchievementListFilter),"ListFilter");
            listFilterLabel = (FrameworkElement)listOptions.Children[0]; listFilterChoice = (FrameworkElement)listOptions.Children[1];
            Choice(listOptions,"Sort achievements",typeof(AchievementListOrder),"ListOrder");
            listSortChoice = (ComboBox)listOptions.Children[3];
            Number(listOptions,"Maximum achievements (0 = all)","Limit");
            recentOptions = new StackPanel(); listOptions.Children.Add(recentOptions);
            var recentLimits = Columns(recentOptions);
            NumberField(recentLimits,"Maximum achievements per game (0 = all)","PerGameLimit");
            NumberField(recentLimits,"Unlocked within last N days (0 = any date)","RecentDays");
            ChoiceField(recentLimits,"Group achievements by",typeof(RecentAchievementGrouping),"RecentGrouping");
            ChoiceField(recentLimits,"Group order",typeof(RecentAchievementGroupOrder),"RecentGroupOrder");
            var recentFilters = Columns(recentOptions);
            TextField(recentFilters,"Game name contains","GameNameFilter"); TextField(recentFilters,"Source name contains","SourceNameFilter"); TextField(recentFilters,"Platform name contains","PlatformNameFilter");
            var recentChecks = Columns(recentOptions);
            Check(recentChecks,"Include hidden games","IncludeHidden"); Check(recentChecks,"Installed games only","InstalledOnly"); Check(recentChecks,"Favorite games only","FavoritesOnly");
            Check(recentChecks,"Show game-group progress bar","ShowGroupProgress"); Check(recentChecks,"Show achievement icons","ShowArtwork");
            Check(recentChecks,"Show achievement names","ShowTitle"); Check(recentChecks,"Show descriptions","ShowDescription");
            Check(recentChecks,"Show game name inside each achievement row","ShowRecentGameName"); Check(recentChecks,"Show unlock date inside each achievement row","ShowRecentExtraDetails");
            var recentIcon = Columns(recentOptions);
            Check(recentIcon,"Customize icon size","CustomizeRecentIconSize"); NumberField(recentIcon,"Icon size","ArtworkSize");
            var recentColors = new StackPanel(); recentOptions.Children.Add(new Expander { Header = "Colors", Content = recentColors, Margin = new Thickness(0,6,0,6) });
            var recentPalette = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 }; recentColors.Children.Add(recentPalette);
            foreach (var pair in new[] { new[] { "Progress color","ProgressColor" } }) {
                var picker = InlineTextColor(pair[1],"CustomizeRecentProgress"); picker.Label = "Progress"; recentPalette.Children.Add(picker);
            }
            listFields = new StackPanel(); listOptions.Children.Add(listFields);
            var listChecks = Columns(listFields);
            Check(listChecks,"Show achievement icons","ShowArtwork"); Check(listChecks,"Show achievement names","ShowTitle");
            Check(listChecks,"Show descriptions","ShowDescription"); Check(listChecks,"Show game name","ShowCounts");
            Check(listChecks,"Show locked / unlocked state","ShowUnlockState"); Check(listChecks,"Show unlock date","ShowUnlockDate");
            Check(listChecks,"Include hidden games","IncludeHidden"); Check(listChecks,"Installed games only","InstalledOnly"); Check(listChecks,"Favorite games only","FavoritesOnly");
            var content = Section("Content and filters"); contentSection = (Expander)content.Parent;
            Number(content,"Maximum games (0 = all)","Limit");
            var contentLimits = Columns(content);
            NumberField(contentLimits,"Minimum achievement progress (%)","MinimumPercent"); NumberField(contentLimits,"Maximum remaining achievements (0 = any)","MaximumRemaining");
            Choice(content,"Sort games",typeof(AchievementGameOrder),"Order");
            var contentChecks = Columns(content);
            Check(contentChecks,"Include hidden games","IncludeHidden"); Check(contentChecks,"Installed only","InstalledOnly");
            Check(contentChecks,"Favorites only","FavoritesOnly"); Check(contentChecks,"Include fully unlocked games (progress/goals)","IncludeCompleted");
            Check(contentChecks,"Show artwork","ShowArtwork"); Check(contentChecks,"Show game title","ShowTitle");
            Check(contentChecks,"Show earned / total","ShowCounts"); Check(contentChecks,"Show percentage","ShowPercent"); Check(contentChecks,"Show remaining count","ShowRemaining");
            var layout = Section("Layout"); layoutSection = (Expander)layout.Parent;
            Choice(layout,"Layout",typeof(AchievementCardLayout),"Layout"); Choice(layout,"Progress graphic",typeof(AchievementProgressStyle),"ProgressStyle");
            progressLabel = (FrameworkElement)layout.Children[2]; progressChoice = (FrameworkElement)layout.Children[3];
            Choice(layout,"Text alignment",typeof(TextAlignment),"Alignment");
            var dimensions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Name = "AppearanceDimensions" }; layout.Children.Add(dimensions);
            foreach (var pair in new[] { new[] { "Card width","CardWidth" },new[] { "Artwork size","ArtworkSize" }, new[] { "Space between cards","Spacing" },
                new[] { "Ring size","RingSize" },new[] { "Bar / ring thickness","BarHeight" },
                new[] { "Card padding","Padding" },new[] { "Corner radius","CornerRadius" },new[] { "Border width","BorderWidth" },new[] { "Card background opacity","BackgroundOpacity" } }) {
                var field = new StackPanel { Margin = new Thickness(0,0,8,4) };
                if (pair[1] == "BackgroundOpacity") {
                    field.Name = "CardOpacityField";
                    Label(field,pair[0]);
                    var opacity = new Grid(); opacity.ColumnDefinitions.Add(new ColumnDefinition()); opacity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                    var slider = new Slider { Name = "CardOpacitySlider", Minimum = 0, Maximum = 1, TickFrequency = .01, SmallChange = .01, LargeChange = .1,
                        IsSnapToTickEnabled = true, AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft, AutoToolTipPrecision = 2, Margin = new Thickness(0,0,6,0) };
                    slider.SetBinding(Slider.ValueProperty,Bind("BackgroundOpacity")); opacity.Children.Add(slider);
                    var number = new TextBox { TextAlignment = TextAlignment.Center }; Grid.SetColumn(number,1);
                    var binding = Bind("BackgroundOpacity"); binding.StringFormat = "0.00"; number.SetBinding(TextBox.TextProperty,binding); opacity.Children.Add(number); field.Children.Add(opacity);
                } else if (pair[1] == "TitleSize") FontField(field,pair[0],"TitleSize","Bold","Italic");
                else if (pair[1] == "DetailSize") FontField(field,"Other text size","DetailSize","DetailBold","DetailItalic");
                else Number(field,pair[0],pair[1]);
                if (pair[1] == "BackgroundOpacity") layout.Children.Add(field); else dimensions.Children.Add(field);
                if (pair[1] == "RingSize" || pair[1] == "BarHeight") graphSizingControls.Add(field);
            }
            var perText = Section("Text formatting",false);
            var textEditor = new TextSettingsSelector(); perText.Children.Add(textEditor);
            foreach (var entry in new[] { new[] { "Title","TitleSize","Bold","Italic","TitleColor" }, new[] { "Shared detail style","DetailSize","DetailBold","DetailItalic","DetailColor" } }) {
                var field = new StackPanel();
                Check(field,"Customize text size and style","CustomizeRecentTypography"); recentTypographyChecks.Add((FrameworkElement)field.Children[0]);
                var font = FontField(field,"Size",entry[1],entry[2],entry[3]);
                font.Children.Add(InlineTextColor(entry[4],"CustomTextColors"));
                textEditor.AddEntry(entry[0],field);
            }
            foreach (AchievementTextRole role in Enum.GetValues(typeof(AchievementTextRole))) {
                var field = new StackPanel { Margin = new Thickness(0,0,8,10) };
                field.SetBinding(DataContextProperty,new Binding("TextStyles["+role+"]"));
                var name = role == AchievementTextRole.GameName ? "Game name" : role == AchievementTextRole.UnlockState ? "Locked / unlocked" : role == AchievementTextRole.UnlockDate ? "Unlock date" : role == AchievementTextRole.GroupTitle ? "Group headers" : role.ToString();
                Check(field,"Use shared style","UseDefaults");
                var custom = new StackPanel(); custom.SetBinding(IsEnabledProperty,new Binding("UseDefaults") { Converter = new InverseBoolConverter() });
                var font = FontField(custom,"Size","Size","Bold","Italic"); font.Children.Add(InlineTextColor("Color","UseCustomColor")); field.Children.Add(custom);
                textStyleFields[role] = textEditor.AddEntry(name,field);
            }
            var colors = Section("Colors",false); colorsSection = (Expander)colors.Parent;
            var palette = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 }; colors.Children.Add(palette);
            foreach (var pair in new[] { new[] { "Progress","ProgressColor" },
                new[] { "Completed / goal reached","CompleteColor" },new[] { "Progress track","TrackColor" },new[] { "Card background","CardColor" },new[] { "Card border","BorderColor" } }) {
                var picker = new PopupColorPicker { Label = pair[0], ShowCustomColorOption = false, Margin = new Thickness(0,4,8,4) };
                picker.SetBinding(PopupColorPicker.SelectedColorProperty,Bind(pair[1])); palette.Children.Add(picker);
            }
            var goals = Section("Goals",false); goalsSection = (Expander)goals.Parent;
            pinnedDescription = Hint(goals,"Used by Achievement goals. Add a game, then set its target percentage.");
            var add = new Button { Content = "Add pinned game", Margin = new Thickness(0,6,0,6) }; add.Click += AddGoal; goals.Children.Add(add);
            var items = new ItemsControl(); items.SetBinding(ItemsControl.ItemsSourceProperty,new Binding("Goals"));
            var template = new DataTemplate(typeof(AchievementGoal));
            var row = new FrameworkElementFactory(typeof(StackPanel));
            var title = new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty,new Binding("GameName")); title.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); row.AppendChild(title);
            var target = new FrameworkElementFactory(typeof(TextBox));
            var targetStyle = new Style(typeof(TextBox));
            var listTrigger = new DataTrigger { Binding = new Binding("WidgetId") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,typeof(AchievementWidgetSettingsView),1) }, Value = "GameAchievementList" };
            listTrigger.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed)); targetStyle.Triggers.Add(listTrigger); target.SetValue(StyleProperty,targetStyle);
            target.SetBinding(TextBox.TextProperty,Bind("TargetPercent")); target.SetValue(FrameworkElement.ToolTipProperty,"Target percentage (1 to 100)"); row.AppendChild(target);
            var remove = new FrameworkElementFactory(typeof(Button)); remove.SetValue(ContentControl.ContentProperty,"Remove goal"); remove.SetValue(FrameworkElement.MarginProperty,new Thickness(0,3,0,10));
            remove.AddHandler(Button.ClickEvent,new RoutedEventHandler((s,e) => { if (DataContext is AchievementWidgetSettings options && (s as FrameworkElement)?.DataContext is AchievementGoal goal) options.Goals.Remove(goal); })); row.AppendChild(remove);
            template.VisualTree = row; items.ItemTemplate = template; goals.Children.Add(items);
            var reset = new Button { Content = "Reset achievement widget settings", Margin = new Thickness(0,10,0,0) };
            reset.Click += (s,e) => {
                DependencyObject parent = this;
                while (parent != null) {
                    if ((parent as FrameworkElement)?.DataContext is ViewModels.Layout.GridNodeViewModel model) {
                        model.GridNode.ViewProperties.AchievementSettings = AchievementWidgetSettings.Create(model.GridNode.ViewProperties.ViewId); return;
                    }
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            };
            body.Children.Add(reset); UpdateSourceVisibility();
        }
        private StackPanel Section(string title,bool expanded = true) {
            var panel = new StackPanel { Margin = new Thickness(8,6,0,10) };
            var expander = new Expander { Header = title, IsExpanded = expanded, Content = panel, Margin = new Thickness(0,0,0,8) };
            expander.SetResourceReference(Control.ForegroundProperty,"TextBrush"); body.Children.Add(expander); return panel;
        }
        private sealed class InverseBoolConverter : IValueConverter
        {
            public object Convert(object value,Type type,object parameter,System.Globalization.CultureInfo culture) => !(value is bool flag && flag);
            public object ConvertBack(object value,Type type,object parameter,System.Globalization.CultureInfo culture) => !(value is bool flag && flag);
        }
        private static WrapPanel FontField(Panel panel,string title,string size,string bold,string italic)
        {
            Label(panel,title); var row = new WrapPanel(); panel.Children.Add(row);
            var number = new TextBox { Width = 48, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
            number.SetBinding(TextBox.TextProperty,Bind(size)); row.Children.Add(number);
            var b = FormattingToggle("B","Bold"); b.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,Bind(bold)); row.Children.Add(b);
            var i = FormattingToggle("I","Italic"); i.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,Bind(italic)); row.Children.Add(i);
            return row;
        }
        private static PopupColorPicker InlineTextColor(string property,string flag)
        {
            var picker = new PopupColorPicker { Margin = new Thickness(8,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            picker.SetBinding(PopupColorPicker.SelectedColorProperty,Bind(property)); picker.SetBinding(PopupColorPicker.UseCustomColorProperty,Bind(flag)); return picker;
        }
        private static System.Windows.Controls.Primitives.ToggleButton FormattingToggle(string symbol,string name)
        {
            var label = new TextBlock { Text = symbol, FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 16,
                FontWeight = symbol == "B" ? FontWeights.Bold : FontWeights.Normal, FontStyle = symbol == "I" ? FontStyles.Italic : FontStyles.Normal,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            var button = new System.Windows.Controls.Primitives.ToggleButton { Content = label, Width = 32, Height = 28,
                Margin = new Thickness(0,4,5,4), ToolTip = name };
            System.Windows.Automation.AutomationProperties.SetName(button,name); return button;
        }
        private static Binding Bind(string path) => new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, ValidatesOnExceptions = true };
        private static void Label(Panel panel,string text) { var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,2) }; label.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); panel.Children.Add(label); }
        private static void Number(Panel panel,string title,string property) { Label(panel,title); var box = new TextBox(); box.SetBinding(TextBox.TextProperty,Bind(property)); panel.Children.Add(box); }
        private static void Choice(Panel panel,string title,Type type,string property) { Label(panel,title); var box = new ComboBox { ItemsSource = Enum.GetValues(type) }; box.SetBinding(ComboBox.SelectedItemProperty,Bind(property)); panel.Children.Add(box); }
        private static void Check(Panel panel,string title,string property) { var box = new CheckBox { Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0,4,0,0) }; box.SetResourceReference(Control.ForegroundProperty,"TextBrush"); box.SetBinding(CheckBox.IsCheckedProperty,Bind(property)); panel.Children.Add(box); }
        // Shared layout kit (Controls/SettingsLayout.cs): related fields in responsive columns, labeled fields and hints.
        private static SettingsColumns Columns(Panel panel) { var columns = new SettingsColumns(); panel.Children.Add(columns); return columns; }
        private static void NumberField(Panel panel,string title,string property) { var field = new SettingsField { Label = title }; var box = new TextBox(); box.SetBinding(TextBox.TextProperty,Bind(property)); field.Children.Add(box); panel.Children.Add(field); }
        private static void TextField(Panel panel,string title,string property) => NumberField(panel,title,property);
        private static void ChoiceField(Panel panel,string title,Type type,string property) { var field = new SettingsField { Label = title }; var box = new ComboBox { ItemsSource = Enum.GetValues(type) }; box.SetBinding(ComboBox.SelectedItemProperty,Bind(property)); field.Children.Add(box); panel.Children.Add(field); }
        private static SettingsHint Hint(Panel panel,string text) { var hint = new SettingsHint { Text = text, Margin = new Thickness(0,3,0,4) }; panel.Children.Add(hint); return hint; }
        private sealed class GameChoice : GenericItemOption { public Guid GameId { get; set; } }
        private void AddGoal(object sender,RoutedEventArgs e) {
            if (!(DataContext is AchievementWidgetSettings options)) return;
            var api = LandingPageExtension.Instance.PlayniteApi;
            Func<string,List<GenericItemOption>> search = text => api.Database.Games.Where(game => !options.Goals.Any(goal => goal.GameId == game.Id)
                && (string.IsNullOrWhiteSpace(text) || (game.Name ?? "").IndexOf(text,StringComparison.CurrentCultureIgnoreCase) >= 0))
                .OrderBy(game => game.Name).Take(100).Select(game => (GenericItemOption)new GameChoice { GameId = game.Id, Name = game.Name }).ToList();
            var choice = api.Dialogs.ChooseItemWithSearch(search(""),search,"Add achievement goal","Search your library") as GameChoice;
            if (choice != null) options.Goals.Add(new AchievementGoal { GameId = choice.GameId, GameName = choice.Name });
        }
    }
}
