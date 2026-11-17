using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using LandingPage.Settings;
namespace LandingPage.Views.Settings
{
    public sealed class StatisticColorLookup : IMultiValueConverter
    {
        public object Convert(object[] values,Type targetType,object parameter,System.Globalization.CultureInfo culture)
            {
            if(values.Length < 2 || !(values[0] is StatisticsCounter counter) || !(values[1] is IEnumerable<CounterPlacement> positions)) return null;
            var placement = positions.FirstOrDefault(item => item?.Counter == counter);
            return parameter as string == "Position" ? (object)placement : placement?.Colors;
        }
        public object[] ConvertBack(object value,Type[] targetTypes,object parameter,System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
    public sealed class InverseGlobalColorConverter : IValueConverter
    {
        public object Convert(object value,Type type,object parameter,System.Globalization.CultureInfo culture) => !(value is bool flag && flag);
        public object ConvertBack(object value,Type type,object parameter,System.Globalization.CultureInfo culture) => !(value is bool flag && flag);
    }
    public partial class LibraryStatisticsSettingsView : UserControl
    {
        public static readonly DependencyProperty ChartOnlyProperty = DependencyProperty.Register(nameof(ChartOnly),typeof(bool),typeof(LibraryStatisticsSettingsView),new PropertyMetadata(false,(sender,args) => ((LibraryStatisticsSettingsView)sender).UpdateMode()));
        public bool ChartOnly { get => (bool)GetValue(ChartOnlyProperty); set => SetValue(ChartOnlyProperty,value); }
        public static readonly DependencyProperty ChildChartProperty = DependencyProperty.Register(nameof(ChildChart),typeof(bool),typeof(LibraryStatisticsSettingsView),new PropertyMetadata(false,(sender,args) => ((LibraryStatisticsSettingsView)sender).UpdateMode()));
        public bool ChildChart { get => (bool)GetValue(ChildChartProperty); set => SetValue(ChildChartProperty,value); }
        private ObservableCollection<LibraryStatisticsSettings> observedCharts;
        private bool rebuilding;
        private bool Manager => ChartOnly && !ChildChart;
        private void UpdateMode()
        {
            if(CounterOptions == null) return;
            foreach(var element in new FrameworkElement[] {CounterOptions,CounterGridOptions,PeriodOptions,AdditionalCounterOptions,StatisticGroupOptions,RowsOption,ValueTextOptions,CounterLabelStrikeToggle,CounterLabelTextOptions,CounterRenderingOptions,ValueSizeLabel,ValueSizeInput,TileWidthLabel,TileWidthInput}) element.Visibility = ChartOnly ? Visibility.Collapsed : Visibility.Visible;
            ChartOptions.Visibility = ChartPaletteOptions.Visibility = ChartOnly && ChildChart ? Visibility.Visible : Visibility.Collapsed;
            ChartColors.Header = ChartOnly ? "Colors" : "Global colors";
            GlobalTextColorToggle.CustomColorLabel = ChartOnly ? "Use custom label color" : "Use custom global text color";
            ChartColors.Visibility = ScopeOptions.Visibility = LayoutTextOptions.Visibility = Manager ? Visibility.Collapsed : Visibility.Visible;
            MultiChartOptions.Visibility = Manager ? Visibility.Visible : Visibility.Collapsed;
            ShowChartOption.Visibility = Visibility.Collapsed;
            WatchCharts();
        }
        public LibraryStatisticsSettingsView()
        {
            InitializeComponent(); UpdateMode();
            DataContextChanged += (s,e) => { WatchCharts(); };
            Loaded += (s,e) => { WatchCharts(); }; Unloaded += (s,e) => { if(observedCharts != null) observedCharts.CollectionChanged -= ChartsChanged; observedCharts = null; };
        }
        private void WatchCharts()
        {
            if(!ChartOnly && DataContext is LibraryStatisticsSettings statisticSettings)
            {
                var api = LandingPageExtension.Instance?.PlayniteApi;
                foreach(var option in statisticSettings.StatisticGroups.Where(item => item != null && item.SupportsSelection))
                {
                    var choices = new List<StatisticSelectionChoice> { new StatisticSelectionChoice { Id = Guid.Empty,Name = option.Group == StatisticGroup.Sources ? "Playnite / no source" : "No platform" } };
                    if(option.Group == StatisticGroup.Sources && api?.Database.Sources != null) choices.AddRange(api.Database.Sources.Select(item => new StatisticSelectionChoice { Id = item.Id,Name = item.Name }).OrderBy(item => item.Name));
                    if(option.Group == StatisticGroup.Platforms && api?.Database.Platforms != null) choices.AddRange(api.Database.Platforms.Select(item => new StatisticSelectionChoice { Id = item.Id,Name = item.Name }).OrderBy(item => item.Name));
                    if(!choices.Any(item => item.Id == option.SelectedId)) choices.Add(new StatisticSelectionChoice { Id = option.SelectedId,Name = "Unavailable saved selection" });
                    if(!option.Choices.Select(item => item.Id+":"+item.Name).SequenceEqual(choices.Select(item => item.Id+":"+item.Name))) { option.Choices.Clear(); foreach(var choice in choices) option.Choices.Add(choice); }
                }
            }
            if(observedCharts != null) observedCharts.CollectionChanged -= ChartsChanged;
            observedCharts = Manager ? (DataContext as LibraryStatisticsSettings)?.ExtraCharts : null;
            if(observedCharts != null) observedCharts.CollectionChanged += ChartsChanged;
            RebuildCharts();
        }
        private void ChartsChanged(object sender,NotifyCollectionChangedEventArgs args) => RebuildCharts();
        private void RebuildCharts()
        {
            if(ExtraChartList == null || !Manager) return;
            var selected = ExtraChartList.SelectedItem;
            var charts = new List<LibraryStatisticsSettings>();
            if(DataContext is LibraryStatisticsSettings settings) { charts.Add(settings); charts.AddRange(settings.ExtraCharts.Where(chart => chart != null)); }
            rebuilding = true; ExtraChartList.ItemsSource = charts;
            ExtraChartList.SelectedItem = charts.Contains(selected) ? selected : charts.FirstOrDefault(); rebuilding = false;
            ShowSelectedChart();
        }
        private void UpdateButtons()
        {
            if(ExtraChartList == null) return;
            var index = ExtraChartList.SelectedIndex; var count = ExtraChartList.Items.Count;
            RemoveChartButton.IsEnabled = count > 1 && index >= 0;
            MoveChartUpButton.IsEnabled = index > 0; MoveChartDownButton.IsEnabled = index >= 0 && index < count-1;
        }
        private void EditCharts_Click(object sender,RoutedEventArgs args)
        {
            if(!(DataContext is LibraryStatisticsSettings settings)) return;
            DependencyObject parent = this;
            while(parent != null && !(parent is StartPageView)) parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            if(parent is StartPageView page) { settings.EditCharts = true; page.BeginChartEditing(); }
        }
        private void ResetLabelOffsets_Click(object sender,RoutedEventArgs args)
        { if(DataContext is LibraryStatisticsSettings settings) { settings.LabelOffsetX = settings.LabelOffsetY = 0; foreach(var label in settings.LabelOffsets) label.X = label.Y = 0; } }
        private void StatisticPosition_Expanded(object sender,RoutedEventArgs args)
        {
            if(ReferenceEquals(sender,args.OriginalSource) && sender is Expander expander) expander.ContentTemplate = (DataTemplate)expander.FindResource("StatisticPositionFields");
        }
        private void GlobalTextColor_Editing(object sender,RoutedEventArgs args)
        {
            if(DataContext is LibraryStatisticsSettings settings && sender is LandingPage.Controls.PopupColorPicker picker)
            {
                settings.CustomLabelColor = picker.UseCustomColor;
                if(!ChartOnly) settings.ValueColorMode = settings.CustomLabelColor ? StatisticsValueColor.MatchLabels : StatisticsValueColor.Theme;
            }
        }
        private void StatisticColors_Expanded(object sender,RoutedEventArgs args)
        {
            if(ReferenceEquals(sender,args.OriginalSource) && sender is Expander expander) expander.ContentTemplate = (DataTemplate)expander.FindResource("StatisticColorFields");
        }
        private void ThemeValueFont_Click(object sender,RoutedEventArgs args) { if(DataContext is LibraryStatisticsSettings settings) settings.ValueFontFamily = string.Empty; }
        private void ThemeFont_Click(object sender,RoutedEventArgs args) { if(DataContext is LibraryStatisticsSettings settings) settings.LabelFontFamily = string.Empty; }
        private void ResetCounterPositions_Click(object sender,RoutedEventArgs args) { if(DataContext is LibraryStatisticsSettings settings) settings.ResetCounterPositions(); }
        private void AddChart_Click(object sender,RoutedEventArgs args)
        {
            if(!(DataContext is LibraryStatisticsSettings settings)) return;
            var chart = LibraryStatisticsSettings.CreateChartSettings(); chart.ChartName = "Chart "+(settings.ExtraCharts.Count+2);
            settings.ExtraCharts.Add(chart); ExtraChartList.SelectedItem = chart;
        }
        private void DuplicateChart_Click(object sender,RoutedEventArgs args)
        {
            if(!(DataContext is LibraryStatisticsSettings settings)) return;
            var chart = Models.DashboardProfiles.Clone(ExtraChartList.SelectedItem as LibraryStatisticsSettings ?? settings);
            chart.ExtraCharts.Clear(); chart.ChartName += " copy";
            settings.ExtraCharts.Add(chart); ExtraChartList.SelectedItem = chart;
        }
        private static void CopyChart(LibraryStatisticsSettings from,LibraryStatisticsSettings to)
        {
            var copy = Models.DashboardProfiles.Clone(from);
            foreach(var property in typeof(LibraryStatisticsSettings).GetProperties().Where(property => property.DeclaringType == typeof(LibraryStatisticsSettings) && property.CanRead && property.CanWrite && property.Name != nameof(LibraryStatisticsSettings.ExtraCharts) && property.Name != nameof(LibraryStatisticsSettings.ChartColumns))) property.SetValue(to,property.GetValue(copy));
        }
        private void RemoveChart_Click(object sender,RoutedEventArgs args)
        {
            if(!(DataContext is LibraryStatisticsSettings settings) || settings.ExtraCharts.Count == 0 || !(ExtraChartList.SelectedItem is LibraryStatisticsSettings chart)) return;
            if(ReferenceEquals(chart,settings)) { CopyChart(settings.ExtraCharts[0],settings); settings.ExtraCharts.RemoveAt(0); ExtraChartList.SelectedItem = settings; }
            else settings.ExtraCharts.Remove(chart);
        }
        private void MoveChartUp_Click(object sender,RoutedEventArgs args) => MoveChart(-1);
        private void MoveChartDown_Click(object sender,RoutedEventArgs args) => MoveChart(1);
        private void MoveChart(int offset)
        {
            if(!(DataContext is LibraryStatisticsSettings settings) || !(ExtraChartList.SelectedItem is LibraryStatisticsSettings chart)) return;
            var index = ExtraChartList.SelectedIndex; var target = index+offset;
            if(target < 0 || target >= ExtraChartList.Items.Count) return;
            if(index == 0 || target == 0)
            {
                var oldFirst = Models.DashboardProfiles.Clone(settings); oldFirst.ExtraCharts.Clear();
                CopyChart(settings.ExtraCharts[0],settings); settings.ExtraCharts[0] = oldFirst;
                ExtraChartList.SelectedItem = index == 0 ? oldFirst : settings;
            }
            else { settings.ExtraCharts.Move(index-1,target-1); ExtraChartList.SelectedItem = chart; }
            UpdateButtons();
        }
        private void ExtraChart_SelectionChanged(object sender,SelectionChangedEventArgs args) { if(!rebuilding) ShowSelectedChart(); }
        private void ShowSelectedChart()
        {
            if(ExtraChartEditor == null) return;
            ExtraChartEditor.Content = ExtraChartList.SelectedItem is LibraryStatisticsSettings chart
                ? new LibraryStatisticsSettingsView { ChartOnly = true,ChildChart = true,DataContext = chart } : null;
            UpdateButtons();
        }
    }
}
