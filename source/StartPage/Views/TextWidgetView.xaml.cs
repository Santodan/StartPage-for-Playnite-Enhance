using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LandingPage.Settings;

namespace LandingPage.Views
{
    public partial class TextWidgetView : UserControl
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
            nameof(Settings), typeof(TextWidgetSettings), typeof(TextWidgetView), new PropertyMetadata(null));
        public TextWidgetSettings Settings
        {
            get => (TextWidgetSettings)GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }

        public TextWidgetView()
        {
            Settings = new TextWidgetSettings();
            InitializeComponent();
            SetBinding(SettingsProperty, new Binding("DataContext.GridNode.ViewProperties.TextSettings")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(GridNodeView), 1),
                FallbackValue = Settings, TargetNullValue = Settings
            });
        }
    }
}
