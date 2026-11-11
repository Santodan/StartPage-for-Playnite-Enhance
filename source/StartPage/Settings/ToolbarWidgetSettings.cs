using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows;

namespace LandingPage.Settings
{
    public enum ExtensionOpenMode { Automatic, ExtensionPage }

    public class ToolbarEntry : ObservableObjectExt
    {
        private string id;
        public string Id { get => id; set => SetValue(ref id, value, nameof(Id), nameof(IsExtension), nameof(CanChooseOpenMode)); }
        [Playnite.SDK.Data.DontSerialize, Newtonsoft.Json.JsonIgnore]
        public bool IsExtension => Id?.StartsWith("extension.") == true || Id?.StartsWith("plugin.") == true || Id?.StartsWith("sidebar.") == true;
        [Playnite.SDK.Data.DontSerialize, Newtonsoft.Json.JsonIgnore]
        public bool CanChooseOpenMode => IsExtension && Id?.StartsWith("sidebar.") != true;
        private ExtensionOpenMode openMode;
        public ExtensionOpenMode OpenMode { get => openMode; set => SetValue(ref openMode, value); }
        private string groupId = "main";
        public string GroupId { get => groupId; set => SetValue(ref groupId, value ?? "main"); }
        private string label = string.Empty;
        public string Label { get => label; set => SetValue(ref label, value ?? string.Empty); }
    }

    public enum ToolbarGroupSizing { Fill, Content, Fixed }

    public class ToolbarGroup : ObservableObjectExt
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        private string name = "Group";
        public string Name { get => name; set => SetValue(ref name, string.IsNullOrWhiteSpace(value) ? "Group" : value); }
        private ToolbarGroupSizing widthSizing = ToolbarGroupSizing.Fill;
        public ToolbarGroupSizing WidthSizing { get => widthSizing; set => SetValue(ref widthSizing, value); }
        private ToolbarGroupSizing heightSizing = ToolbarGroupSizing.Content;
        public ToolbarGroupSizing HeightSizing { get => heightSizing; set => SetValue(ref heightSizing, value); }
        private double width = 240;
        public double Width { get => width; set => SetValue(ref width, Clamp(value, 24, 4000)); }
        private double height = 64;
        public double Height { get => height; set => SetValue(ref height, Clamp(value, 24, 4000)); }
        private double weight = 1;
        public double Weight { get => weight; set => SetValue(ref weight, Clamp(value, 0.1, 100)); }
        private double padding = 0;
        public double Padding { get => padding; set => SetValue(ref padding, Clamp(value, 0, 100)); }
        private double margin = 0;
        public double Margin { get => margin; set => SetValue(ref margin, Clamp(value, 0, 100)); }
        private double borderWidth = 0;
        public double BorderWidth { get => borderWidth; set => SetValue(ref borderWidth, Clamp(value, 0, 20)); }
        private double cornerRadius = 8;
        public double CornerRadius { get => cornerRadius; set => SetValue(ref cornerRadius, Clamp(value, 0, 100)); }
        private double backgroundOpacity = 0.6;
        public double BackgroundOpacity { get => backgroundOpacity; set => SetValue(ref backgroundOpacity, Clamp(value, 0, 1)); }
        private bool showBackground;
        public bool ShowBackground { get => showBackground; set => SetValue(ref showBackground, value); }
        private string background = "#202020";
        public string Background { get => background; set { ValidateColor(value); SetValue(ref background, value); } }
        private string borderColor = "#808080";
        public string BorderColor { get => borderColor; set { ValidateColor(value); SetValue(ref borderColor, value); } }
        private static double Clamp(double value, double min, double max) => double.IsNaN(value) || double.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));
        private static void ValidateColor(string value)
        {
            try { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); }
            catch (Exception) { throw new ArgumentException("Enter a color name or #RRGGBB / #AARRGGBB."); }
        }
        public void ResetAppearance()
        {
            ShowBackground = false; Background = "#202020"; BackgroundOpacity = 0.6; BorderColor = "#808080";
            BorderWidth = 0; CornerRadius = 8; Padding = Margin = 0;
        }
        private HorizontalAlignment alignment = HorizontalAlignment.Left;
        public HorizontalAlignment Alignment { get => alignment; set => SetValue(ref alignment, value); }
        private VerticalAlignment verticalAlignment = VerticalAlignment.Center;
        public VerticalAlignment VerticalAlignment { get => verticalAlignment; set => SetValue(ref verticalAlignment, value); }
    }

    public enum NotificationBadgeStyle { Count, Dot }

    public class ToolbarWidgetSettings : ObservableObjectExt
    {
        public event EventHandler NativeRefreshRequested;
        public void RequestNativeRefresh() => NativeRefreshRequested?.Invoke(this, EventArgs.Empty);
        private ObservableCollection<ToolbarGroup> groups = new ObservableCollection<ToolbarGroup> { new ToolbarGroup { Id = "main", Name = "Main" } };
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public ObservableCollection<ToolbarGroup> Groups { get => groups; set => SetValue(ref groups, value ?? new ObservableCollection<ToolbarGroup>()); }
        private ObservableCollection<ToolbarEntry> items = Defaults();
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public ObservableCollection<ToolbarEntry> Items { get => items; set => SetValue(ref items, value ?? Defaults()); }
        private Orientation orientation = Orientation.Horizontal;
        public Orientation Orientation { get => orientation; set => SetValue(ref orientation, value); }
        private NotificationBadgeStyle notificationBadgeStyle = NotificationBadgeStyle.Dot;
        public NotificationBadgeStyle NotificationBadgeStyle { get => notificationBadgeStyle; set => SetValue(ref notificationBadgeStyle, value); }
        private bool showLabels = true;
        public bool ShowLabels { get => showLabels; set => SetValue(ref showLabels, value); }
        private bool showIcons = true;
        public bool ShowIcons { get => showIcons; set => SetValue(ref showIcons, value); }
        private double spacing = 4;
        public double Spacing { get => spacing; set => SetValue(ref spacing, Math.Max(0, Math.Min(40, double.IsNaN(value) ? 4 : value))); }
        private double fontSize = 14;
        public double FontSize { get => fontSize; set => SetValue(ref fontSize, Math.Max(8, Math.Min(48, double.IsNaN(value) ? 14 : value))); }
        public static ObservableCollection<ToolbarEntry> Defaults() => new ObservableCollection<ToolbarEntry>
        {
            new ToolbarEntry { Id = "page.previous" }, new ToolbarEntry { Id = "page.select" },
            new ToolbarEntry { Id = "page.next" }, new ToolbarEntry { Id = "page.manage" }
        };
    }
}
