using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using LandingPage.Models.Layout;
using LandingPage.Settings;

namespace LandingPage.Models
{
    public class DashboardProfile : ObservableObjectExt
    {
        public string Format { get; set; } = "StartPage.DashboardProfile";
        public int Version { get; set; } = 1;
        public Guid Id { get; set; } = Guid.NewGuid();
        private string name = "Dashboard";
        public string Name { get => name; set => SetValue(ref name, string.IsNullOrWhiteSpace(value) ? "Dashboard" : value.Trim()); }
        public DashboardSnapshot Snapshot { get; set; }
    }

    public class DashboardSnapshot
    {
        public ObservableCollection<DashboardPage> Pages { get; set; } = new ObservableCollection<DashboardPage>();
        public Guid ActivePageId { get; set; }
        public Dictionary<Guid, ShelvesSettings> Shelves { get; set; } = new Dictionary<Guid, ShelvesSettings>();
        public Dictionary<string, JToken> SharedSettings { get; set; } = new Dictionary<string, JToken>();
    }

    public static class DashboardProfiles
    {
        private class ProfileResolver : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type type) => base.GetSerializableMembers(type)
                .Where(member => !Attribute.IsDefined(member, typeof(Playnite.SDK.Data.DontSerializeAttribute)))
                .Where(member => !(member is PropertyInfo property) || property.GetIndexParameters().Length == 0).ToList();
        }
        private static JsonSerializerSettings JsonSettings() => new JsonSerializerSettings
        {
            ContractResolver = new ProfileResolver(), TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore, MaxDepth = 64, CheckAdditionalContent = true,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };
        public static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value, JsonSettings()), JsonSettings());
        private static readonly string[] sharedNames =
        {
            "Padding", "FixGridSize", "FixedGridHeight", "LayoutSettings", "CoverAspectRatio", "MaxCoverWidth", "HideScrollbarInShelves",
            "DownScaleCovers", "EnableNotifications", "EnableGlobalProgressBar", "EnableGlobalBackground", "GlobalBackgroundOpacity",
            "MostPlayedOptions", "SkipGamesInPreviousMostPlayed", "MaxNumberRecentAchievements", "MaxNumberRecentAchievementsPerGame",
            "SelectedAchievementsProvider", "ShowDetails", "ShowTitleOnCover", "NumberOfGames", "HorizontalShelveLabels",
            "SkipGamesInPreviousShelves", "IgnoreTagId", "IgnoreMostPlayedTagId"
        };

        public static DashboardProfile Capture(LandingPageSettings settings, string name)
        {
            var pages = settings.DashboardPages;
            if (pages == null || pages.Count == 0) { DashboardPages.Ensure(settings); pages = settings.DashboardPages; }
            var snapshot = new DashboardSnapshot { Pages = pages, ActivePageId = settings.ActiveDashboardPageId };
            foreach (var id in pages.SelectMany(page => page.Layout.Enumerate()).Where(node => node.ViewProperties != null).Select(node => node.ViewProperties.InstanceId).Distinct())
            {
                if (settings.ShelveInstanceSettings?.TryGetValue(id, out var shelves) == true) snapshot.Shelves[id] = shelves;
                else if (settings.ShelveInstances?.TryGetValue(id, out var legacy) == true)
                    snapshot.Shelves[id] = new ShelvesSettings(settings) { ShelveProperties = legacy };
            }
            var serializer = JsonSerializer.Create(JsonSettings());
            foreach (var propertyName in sharedNames)
            {
                var property = typeof(LandingPageSettings).GetProperty(propertyName);
                if (property != null) snapshot.SharedSettings[propertyName] = JToken.FromObject(property.GetValue(settings) ?? JValue.CreateNull(), serializer);
            }
            var profile = new DashboardProfile { Name = name, Snapshot = Clone(snapshot) };
            Validate(profile);
            return profile;
        }

        public static string Export(DashboardProfile profile)
        {
            Validate(profile);
            return JsonConvert.SerializeObject(profile, Formatting.Indented, JsonSettings());
        }
        public static DashboardProfile Import(string json)
        {
            using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { MaxDepth = 64, DateParseHandling = DateParseHandling.None })
            {
                var header = JObject.Load(reader);
                if ((string)header["Format"] != "StartPage.DashboardProfile" || (int?)header["Version"] != 1)
                    throw new ArgumentException("This is not a supported StartPage dashboard profile (format version 1).");
            }
            var profile = JsonConvert.DeserializeObject<DashboardProfile>(json, JsonSettings());
            Validate(profile);
            profile.Id = Guid.NewGuid();
            return profile;
        }

        public static void Validate(DashboardProfile profile)
        {
            if (profile == null || profile.Format != "StartPage.DashboardProfile" || profile.Version != 1 || profile.Snapshot?.Pages == null || profile.Snapshot.Pages.Count == 0)
                throw new ArgumentException("The profile must contain at least one dashboard page.");
            var pages = new HashSet<Guid>();
            var instances = new HashSet<string>();
            foreach (var page in profile.Snapshot.Pages)
            {
                if (page == null || page.Layout == null || page.Background == null || page.Id == Guid.Empty || !pages.Add(page.Id))
                    throw new ArgumentException("The profile contains an invalid or duplicated page.");
                var background = page.Background;
                if (!Finite(background.Blur) || background.Blur < 0 || !Unit(background.Overlay) || !Unit(background.Noise)
                    || !Finite(background.RenderScale) || background.RenderScale <= 0 || !Finite(background.AnimationDuration)
                    || background.AnimationDuration < 0 || background.RefreshInterval < 0 || !Unit(background.GameInfoOpacity))
                    throw new ArgumentException("The profile contains invalid background settings.");
                foreach (var node in page.Layout.Enumerate())
                {
                    if (!Finite(node.Padding) || node.Padding < 0) throw new ArgumentException("The profile contains invalid panel padding.");
                    if (node.ViewProperties is ViewProperties view && (view.InstanceId == Guid.Empty || !instances.Add(view.PluginId + ":" + view.InstanceId)))
                        throw new ArgumentException("The profile contains invalid or duplicated widget instance IDs.");
                }
            }
            if (!pages.Contains(profile.Snapshot.ActivePageId)) throw new ArgumentException("The profile's selected page does not exist.");
            if (profile.Snapshot.SharedSettings == null) profile.Snapshot.SharedSettings = new Dictionary<string, JToken>();
            if (profile.Snapshot.Shelves == null) profile.Snapshot.Shelves = new Dictionary<Guid, ShelvesSettings>();
            var serializer = JsonSerializer.Create(JsonSettings());
            foreach (var pair in profile.Snapshot.SharedSettings.Where(pair => sharedNames.Contains(pair.Key)))
            {
                var property = typeof(LandingPageSettings).GetProperty(pair.Key);
                var value = pair.Value.ToObject(property.PropertyType, serializer);
                if (value is double number && (!Finite(number) || number < 0
                    || ((pair.Key == "FixedGridHeight" || pair.Key == "CoverAspectRatio" || pair.Key == "MaxCoverWidth") && number == 0)
                    || (pair.Key == "GlobalBackgroundOpacity" && number > 1))) throw new ArgumentException("Invalid profile setting: " + pair.Key);
            }
            if (profile.Snapshot.Shelves.Values.Any(value => value == null || value.ShelveProperties == null))
                throw new ArgumentException("The profile contains invalid shelf settings.");
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Unit(double value) => Finite(value) && value >= 0 && value <= 1;

        public static DashboardSnapshot Prepare(DashboardProfile profile, Guid builtinPluginId)
        {
            Validate(profile);
            var copy = Clone(profile.Snapshot);
            var ids = new Dictionary<Guid, Guid>();
            var referenced = new HashSet<Guid>(copy.Pages.SelectMany(page => page.Layout.Enumerate()).Where(node => node.ViewProperties != null).Select(node => node.ViewProperties.InstanceId));
            foreach (var page in copy.Pages)
            {
                var oldPage = page.Id; page.Id = Guid.NewGuid();
                if (copy.ActivePageId == oldPage) copy.ActivePageId = page.Id;
                foreach (var node in page.Layout.Enumerate())
                    if (node.ViewProperties is ViewProperties view && view.PluginId == builtinPluginId)
                    {
                        if (!ids.TryGetValue(view.InstanceId, out var replacement)) ids[view.InstanceId] = replacement = Guid.NewGuid();
                        view.InstanceId = replacement;
                    }
            }
            copy.Shelves = copy.Shelves.Where(pair => referenced.Contains(pair.Key)).ToDictionary(pair => ids.TryGetValue(pair.Key, out var replacement) ? replacement : pair.Key, pair => pair.Value);
            return copy;
        }

        public static void ApplyData(LandingPageSettings settings, DashboardSnapshot snapshot)
        {
            var serializer = JsonSerializer.Create(JsonSettings());
            foreach (var pair in snapshot.SharedSettings.Where(pair => sharedNames.Contains(pair.Key)))
            {
                var property = typeof(LandingPageSettings).GetProperty(pair.Key);
                property.SetValue(settings, pair.Value.ToObject(property.PropertyType, serializer));
            }
            settings.DashboardPages = snapshot.Pages;
            settings.ActiveDashboardPageId = snapshot.ActivePageId;
            if (settings.ShelveInstanceSettings == null) settings.ShelveInstanceSettings = new Dictionary<Guid, ShelvesSettings>();
            foreach (var pair in snapshot.Shelves) settings.ShelveInstanceSettings[pair.Key] = pair.Value;
            var active = snapshot.Pages.First(page => page.Id == snapshot.ActivePageId);
            settings.GridLayout = active.Layout;
            active.Background.Apply(settings);
        }
    }
}
