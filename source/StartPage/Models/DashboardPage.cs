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
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public GridNode Layout { get; set; } = DashboardPages.CreateDefaultLayout();
        public DashboardBackground Background { get; set; } = new DashboardBackground();
        public bool UseGlobalBackground { get; set; } = true;
        // Optional intro shown when switching to this page; saved with the page, so profiles carry it.
        private Settings.StartupIntroSettings intro;
        public Settings.StartupIntroSettings Intro { get => intro ?? (intro = new Settings.StartupIntroSettings()); set => SetValue(ref intro, value ?? new Settings.StartupIntroSettings()); }
        // Optional own transition used when switching to this page; otherwise the global transition applies.
        // Transition stays null until the page first gets its own (it then starts as a copy of the global one).
        // Saved with the page, so profiles carry it.
        private bool useOwnTransition;
        public bool UseOwnTransition { get => useOwnTransition; set => SetValue(ref useOwnTransition, value); }
        private Settings.PageTransitionSettings transition;
        public Settings.PageTransitionSettings Transition { get => transition; set => SetValue(ref transition, value); }
    }

    // The existing settings remain the active-page projection, so existing controls,
    // settings dialogs, background integrations, and legacy saves keep working.
    public class DashboardBackground
    {
        private Settings.BackgroundAppearanceSettings appearance = new Settings.BackgroundAppearanceSettings();
        public Settings.BackgroundAppearanceSettings Appearance { get => appearance; set => appearance = value ?? new Settings.BackgroundAppearanceSettings(); }
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
            Appearance = settings.BackgroundAppearance.Copy(),
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

        public DashboardBackground Copy() { var copy = (DashboardBackground)MemberwiseClone(); copy.Appearance = Appearance.Copy(); return copy; }

        public void Apply(LandingPageSettings settings)
        {
            settings.BackgroundAppearance = Appearance.Copy();
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
        // The transition played when switching to `page`: its own when it has one turned on, else the global transition.
        public static Settings.PageTransitionSettings EffectiveTransition(LandingPageSettings settings, DashboardPage page)
            => page != null && page.UseOwnTransition && page.Transition != null ? page.Transition : settings.GlobalTransition;

        // Turns on a page's own transition; the first time it starts as a copy of the global transition.
        public static Settings.PageTransitionSettings UseOwnTransition(LandingPageSettings settings, DashboardPage page)
        {
            if (page.Transition == null) page.Transition = settings.GlobalTransition.Copy();
            page.UseOwnTransition = true;
            return page.Transition;
        }

        // Makes `source` the global transition and every page use it.
        public static void UseOnAllPages(LandingPageSettings settings, Settings.PageTransitionSettings source)
        {
            settings.GlobalTransition.CopyFrom(source);
            foreach (var page in settings.DashboardPages ?? Enumerable.Empty<DashboardPage>()) page.UseOwnTransition = false;
        }

        public static bool UsesGlobalBackground(LandingPageSettings settings, DashboardPage page = null)
            => settings.EnableGlobalBackground && (page ?? settings.DashboardPages?.FirstOrDefault(item => item.Id == settings.ActiveDashboardPageId))?.UseGlobalBackground != false;
        public static DashboardPage Ensure(LandingPageSettings settings)
        {
            if (settings.DashboardPages == null) settings.DashboardPages = new ObservableCollection<DashboardPage>();
            var pages = settings.DashboardPages;
            foreach (var page in pages.Where(page => page == null).ToArray()) pages.Remove(page);
            if (pages.Count == 0)
                pages.Add(new DashboardPage { Name = "Main", Layout = settings.GridLayout ?? CreateDefaultLayout(), Background = DashboardBackground.Capture(settings) });
            var ids = new System.Collections.Generic.HashSet<Guid>();
            foreach (var page in pages)
            {
                if (page.Id == Guid.Empty || !ids.Add(page.Id)) { page.Id = Guid.NewGuid(); ids.Add(page.Id); }
                if (page.Layout == null) page.Layout = CreateDefaultLayout();
                if (page.Background == null) page.Background = DashboardBackground.Capture(settings);
            }
            var active = pages.FirstOrDefault(page => page.Id == settings.ActiveDashboardPageId);
            if (active != null)
            {
                // GridLayout/global background settings include edits saved by existing
                // settings dialogs. They are authoritative for the active page on load.
                active.Layout = settings.GridLayout ?? active.Layout;
                if (!UsesGlobalBackground(settings,active)) active.Background = DashboardBackground.Capture(settings);
            }
            else { active = pages[0]; if (!UsesGlobalBackground(settings,active)) active.Background.Apply(settings); }
            // Configs saved before shared backgrounds existed have no shared copy yet: seed it from the
            // active projection so switching pages does not adopt the destination page's own background.
            if (settings.GlobalDashboardBackground == null && UsesGlobalBackground(settings,active)) settings.GlobalDashboardBackground = DashboardBackground.Capture(settings);
            settings.ActiveDashboardPageId = active.Id;
            settings.GridLayout = active.Layout;
            return active;
        }

        public static GridNode CreateDefaultLayout()
        {
            var layout = new GridNode { Orientation = System.Windows.Controls.Orientation.Vertical };
            var toolbar = new GridNode { Size = System.Windows.GridLength.Auto, ViewProperties = new ViewProperties
                { ViewId = "ToolbarWidget", PluginId = Guid.Parse("a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5") } };
            toolbar.ViewProperties.ToolbarSettings.ShowLabels = false;
            layout.Children.Add(toolbar);
            layout.Children.Add(new GridNode());
            return layout;
        }

        public static DashboardPage Add(LandingPageSettings settings, string requestedName)
        {
            var name = string.IsNullOrWhiteSpace(requestedName) ? "New page" : requestedName.Trim();
            var unique = name;
            for (int suffix = 2; settings.DashboardPages.Any(page => string.Equals(page.Name, unique, StringComparison.OrdinalIgnoreCase)); ++suffix)
                unique = name + " (" + suffix + ")";
            var layout = CreateDefaultLayout();
            var created = new DashboardPage { Name = unique, Layout = layout, Background = DashboardBackground.Capture(settings) };
            settings.DashboardPages.Add(created);
            return created;
        }
    }
}
