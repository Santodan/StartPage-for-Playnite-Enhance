using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LandingPage.Settings
{
    // A Playnite window size in physical pixels (what Windows reports as the resolution of the window).
    public sealed class WindowSizePreset : ObservableObjectExt
    {
        public const int MinWidth = 320, MinHeight = 240, MaxSide = 16384;
        private string name = "Custom";
        public string Name { get => name; set => SetValue(ref name, string.IsNullOrWhiteSpace(value) ? Width + " × " + Height : value.Trim(), nameof(Name), nameof(Label)); }
        private int width = 1920;
        public int Width { get => width; set => SetValue(ref width, Math.Max(MinWidth, Math.Min(MaxSide, value)), nameof(Width), nameof(Label), nameof(SizeText)); }
        private int height = 1080;
        public int Height { get => height; set => SetValue(ref height, Math.Max(MinHeight, Math.Min(MaxSide, value)), nameof(Height), nameof(Label), nameof(SizeText)); }
        [JsonIgnore] public string SizeText => Width + " × " + Height;
        [JsonIgnore] public string Label => Name + "  (" + SizeText + ")";

        public static WindowSizePreset Create(string name, int width, int height) { var preset = new WindowSizePreset { Width = width, Height = height }; preset.Name = name; return preset; }

        // Built-in sizes shown before the user's custom sizes.
        public static IReadOnlyList<WindowSizePreset> Defaults { get; } = new[]
        {
            Create("4K", 3840, 2160), Create("2K", 2560, 1440), Create("1080p", 1920, 1080), Create("720p", 1280, 720)
        };
    }
}
