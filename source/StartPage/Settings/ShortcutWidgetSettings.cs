using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;
using Playnite.SDK.Data;

namespace LandingPage.Settings
{
    public enum ShortcutKind { Game, Application, Folder, Website }
    public enum ShortcutLayout { Wrap, Horizontal, Vertical }
    public sealed class ShortcutEntry : ObservableObjectExt
    {
        private Guid id = Guid.NewGuid();
        public Guid Id { get => id; set => SetValue(ref id,value); }
        private ShortcutKind kind;
        public ShortcutKind Kind { get => kind; set { SetValue(ref kind,value); NotifyPropertyChanged(nameof(DisplayName),null,DisplayName); } }
        private string label = "New shortcut";
        public string Label { get => label; set { SetValue(ref label,value ?? ""); NotifyPropertyChanged(nameof(DisplayName),null,DisplayName); } }
        private string target = "";
        public string Target { get => target; set { SetValue(ref target,value ?? ""); NotifyPropertyChanged(nameof(DisplayName),null,DisplayName); } }
        private Guid gameId;
        public Guid GameId { get => gameId; set => SetValue(ref gameId,value); }
        private string arguments = "";
        public string Arguments { get => arguments; set => SetValue(ref arguments,value ?? ""); }
        private string workingDirectory = "";
        public string WorkingDirectory { get => workingDirectory; set => SetValue(ref workingDirectory,value ?? ""); }
        private string iconPath = "";
        public string IconPath { get => iconPath; set => SetValue(ref iconPath,value ?? ""); }
        private bool enabled = true;
        public bool Enabled { get => enabled; set => SetValue(ref enabled,value); }
        [DontSerialize,JsonIgnore] public string DisplayName => string.IsNullOrWhiteSpace(Label) ? (string.IsNullOrWhiteSpace(Target) ? Kind.ToString() : Target) : Label;
    }
    public sealed class ShortcutWidgetSettings : ObservableObjectExt
    {
        private ObservableCollection<ShortcutEntry> items = new ObservableCollection<ShortcutEntry>();
        public ObservableCollection<ShortcutEntry> Items { get => items; set => SetValue(ref items,value ?? new ObservableCollection<ShortcutEntry>()); }
        private ShortcutLayout layout;
        public ShortcutLayout Layout { get => layout; set => SetValue(ref layout,value); }
        private HorizontalAlignment alignment = HorizontalAlignment.Left;
        public HorizontalAlignment Alignment { get => alignment; set => SetValue(ref alignment,value); }
        private bool showIcons = true, showLabels = true, iconAbove, bold, italic;
        public bool ShowIcons { get => showIcons; set => SetValue(ref showIcons,value); }
        public bool ShowLabels { get => showLabels; set => SetValue(ref showLabels,value); }
        public bool IconAbove { get => iconAbove; set => SetValue(ref iconAbove,value); }
        public bool Bold { get => bold; set => SetValue(ref bold,value); }
        public bool Italic { get => italic; set => SetValue(ref italic,value); }
        private double iconSize = 32, fontSize = 14, spacing = 8, padding = 10, buttonWidth, cornerRadius = 6, borderWidth = 1;
        private static double Limit(double value,double fallback,double min,double max) => double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(min,Math.Min(max,value));
        public double IconSize { get => iconSize; set => SetValue(ref iconSize,Limit(value,32,12,256)); }
        public double FontSize { get => fontSize; set => SetValue(ref fontSize,Limit(value,14,8,100)); }
        public double Spacing { get => spacing; set => SetValue(ref spacing,Limit(value,8,0,100)); }
        public double Padding { get => padding; set => SetValue(ref padding,Limit(value,10,0,100)); }
        public double ButtonWidth { get => buttonWidth; set => SetValue(ref buttonWidth,Limit(value,0,0,1000)); }
        public double CornerRadius { get => cornerRadius; set => SetValue(ref cornerRadius,Limit(value,6,0,100)); }
        public double BorderWidth { get => borderWidth; set => SetValue(ref borderWidth,Limit(value,1,0,20)); }
        private bool customTextColor, customBackground, customBorder;
        public bool CustomTextColor { get => customTextColor; set => SetValue(ref customTextColor,value); }
        public bool CustomBackground { get => customBackground; set => SetValue(ref customBackground,value); }
        public bool CustomBorder { get => customBorder; set => SetValue(ref customBorder,value); }
        private string textColor = "#FFFFFF", background = "#40202020", borderColor = "#808080";
        private static string Color(string value) { if (!(ColorConverter.ConvertFromString(value ?? "") is System.Windows.Media.Color)) throw new ArgumentException("Enter a valid color."); return value; }
        public string TextColor { get => textColor; set => SetValue(ref textColor,Color(value)); }
        public string Background { get => background; set => SetValue(ref background,Color(value)); }
        public string BorderColor { get => borderColor; set => SetValue(ref borderColor,Color(value)); }
    }
}
