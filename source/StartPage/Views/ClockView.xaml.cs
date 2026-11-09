using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LandingPage.Models;
using LandingPage.Settings;

namespace LandingPage.Views
{
    public partial class ClockView : UserControl
    {
        public Clock Clock { get; } = new Clock(false);
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
            nameof(Settings), typeof(ClockSettings), typeof(ClockView),
            new PropertyMetadata(null, (sender, args) => ((ClockView)sender).Clock.Settings = args.NewValue as ClockSettings));
        public ClockSettings Settings
        {
            get => (ClockSettings)GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }

        public ClockView()
        {
            Settings = new ClockSettings();
            InitializeComponent();
            SetBinding(SettingsProperty, new Binding("DataContext.GridNode.ViewProperties.ClockSettings")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(GridNodeView), 1),
                FallbackValue = Settings, TargetNullValue = Settings
            });
            Loaded += (sender, args) => Clock.Start();
            Unloaded += (sender, args) => Clock.Stop();
        }
    }
}
