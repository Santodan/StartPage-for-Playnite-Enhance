using System;
using System.Collections.ObjectModel;
using System.Linq;
using LandingPage.Models.Layout;
using Playnite.SDK;

namespace LandingPage.Models
{
    public class DashboardPage : ObservableObjectExt
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        private string name = "Page";
        public string Name { get => name; set => SetValue(ref name, string.IsNullOrWhiteSpace(value) ? "Page" : value.Trim()); }
        public GridNode Layout { get; set; } = new GridNode();
        public DashboardBackground Background { get; set; } = new DashboardBackground();
    }

    // The existing settings remain the active-page projection, so existing controls,
    // settings dialogs, background integrations, and legacy saves keep working.
    public class DashboardBackground
    {
        public string ImagePath { get; set; }
        public Uri ImageUri { get; set; }
        public BackgroundImageSource Source { get; set; }
        public Guid? LastRandomId { get; set; }
        public double Blur { get; set; }
        public double Overlay { get; set; }
        public double Noise { get; set; }
        public double RenderScale { get; set; }
        public double AnimationDuration { get; set; }
        public int RefreshInterval { get; set; }
        public double GameInfoOpacity { get; set; }
        public bool ShowRunningGame { get; set; }
        public bool DisableRunningGameBlur { get; set; }

        public static DashboardBackground Capture(LandingPageSettings settings) => new DashboardBackground
        {
            ImagePath = settings.BackgroundImagePath,
            ImageUri = settings.BackgroundImageUri,
            Source = settings.BackgroundImageSource,
            LastRandomId = settings.LastRandomBackgroundId,
            Blur = settings.BlurAmount,
            Overlay = settings.OverlayOpacity,
            Noise = settings.NoiseOpacity,
            RenderScale = settings.RenderScale,
            AnimationDuration = settings.AnimationDuration,
            RefreshInterval = settings.BackgroundRefreshInterval,
            GameInfoOpacity = settings.BackgroundGameInfoOpacity,
            ShowRunningGame = settings.ShowCurrentlyPlayedBackground,
            DisableRunningGameBlur = settings.DisableBlurForCurrentlyPlayed,
        };

        public DashboardBackground Copy() => (DashboardBackground)MemberwiseClone();

        public void Apply(LandingPageSettings settings)
        {
            settings.BackgroundImagePath = ImagePath;
            settings.BackgroundImageUri = ImageUri;
            settings.BackgroundImageSource = Source;
            settings.LastRandomBackgroundId = LastRandomId;
            settings.BlurAmount = Blur;
            settings.OverlayOpacity = Overlay;
            settings.NoiseOpacity = Noise;
            settings.RenderScale = RenderScale;
            settings.AnimationDuration = AnimationDuration;
            settings.BackgroundRefreshInterval = RefreshInterval;
            settings.BackgroundGameInfoOpacity = GameInfoOpacity;
            settings.ShowCurrentlyPlayedBackground = ShowRunningGame;
            settings.DisableBlurForCurrentlyPlayed = DisableRunningGameBlur;
        }
    }

    public static class DashboardPages
    {
        public static DashboardPage Ensure(LandingPageSettings settings)
        {
            if (settings.DashboardPages == null) settings.DashboardPages = new ObservableCollection<DashboardPage>();
            var pages = settings.DashboardPages;
            foreach (var page in pages.Where(page => page == null).ToArray()) pages.Remove(page);
            if (pages.Count == 0)
                pages.Add(new DashboardPage { Name = "Main", Layout = settings.GridLayout ?? new GridNode(), Background = DashboardBackground.Capture(settings) });
            var ids = new System.Collections.Generic.HashSet<Guid>();
            foreach (var page in pages)
            {
                if (page.Id == Guid.Empty || !ids.Add(page.Id)) { page.Id = Guid.NewGuid(); ids.Add(page.Id); }
                if (page.Layout == null) page.Layout = new GridNode();
                if (page.Background == null) page.Background = DashboardBackground.Capture(settings);
            }
            var active = pages.FirstOrDefault(page => page.Id == settings.ActiveDashboardPageId);
            if (active != null)
            {
                // GridLayout/global background settings include edits saved by existing
                // settings dialogs. They are authoritative for the active page on load.
                active.Layout = settings.GridLayout ?? active.Layout;
                active.Background = DashboardBackground.Capture(settings);
            }
            else { active = pages[0]; active.Background.Apply(settings); }
            settings.ActiveDashboardPageId = active.Id;
            settings.GridLayout = active.Layout;
            return active;
        }

        public static DashboardPage Add(LandingPageSettings settings, string requestedName)
        {
            var name = string.IsNullOrWhiteSpace(requestedName) ? "New page" : requestedName.Trim();
            var unique = name;
            for (int suffix = 2; settings.DashboardPages.Any(page => string.Equals(page.Name, unique, StringComparison.OrdinalIgnoreCase)); ++suffix)
                unique = name + " (" + suffix + ")";
            var layout = new GridNode { Orientation = System.Windows.Controls.Orientation.Vertical };
            var toolbar = new GridNode { Size = System.Windows.GridLength.Auto, ViewProperties = new ViewProperties { ViewId = "ToolbarWidget", PluginId = Guid.Parse("a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5") } };
            toolbar.ViewProperties.ToolbarSettings.ShowLabels = false;
            layout.Children.Add(toolbar); layout.Children.Add(new GridNode());
            var created = new DashboardPage { Name = unique, Layout = layout, Background = DashboardBackground.Capture(settings) };
            settings.DashboardPages.Add(created);
            return created;
        }
    }
}
