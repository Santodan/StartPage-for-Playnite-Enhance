using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LandingPage.Models;
using LandingPage.Settings;
using LandingPage.Models.Layout;
using LandingPage.ViewModels.Layout;
using Playnite.SDK;

namespace LandingPage.ViewModels
{
    public partial class StartPageViewModel
    {
        public ObservableCollection<DashboardProfile> Profiles => Settings.Settings.DashboardProfiles;
        public IReadOnlyList<DashboardStarterTemplate> StarterProfiles { get; private set; }
        public ICommand AddStarterProfileCommand { get; private set; }
        public ICommand ApplyStarterProfileCommand { get; private set; }
        private bool appendProfilePages = true;
        public bool AppendProfilePages { get => appendProfilePages; set => SetValue(ref appendProfilePages, value); }
        public ObservableCollection<DashboardProfile> SelectedProfiles { get; } = new ObservableCollection<DashboardProfile>();
        public void SetSelectedProfiles(IEnumerable<DashboardProfile> profiles)
        {
            SelectedProfiles.Clear();
            foreach (var profile in profiles) SelectedProfiles.Add(profile);
            ConfirmDeleteProfile = false;
            CommandManager.InvalidateRequerySuggested();
        }
        private DashboardProfile selectedProfile;
        public DashboardProfile SelectedProfile { get => selectedProfile; set { SetValue(ref selectedProfile, value); ConfirmDeleteProfile = false; } }
        private string newProfileName = "My dashboard";
        public string NewProfileName { get => newProfileName; set => SetValue(ref newProfileName, value); }
        private string profileStatus;
        public string ProfileStatus { get => profileStatus; private set => SetValue(ref profileStatus, value); }
        private bool confirmDeleteProfile;
        public bool ConfirmDeleteProfile { get => confirmDeleteProfile; set => SetValue(ref confirmDeleteProfile, value); }
        public ICommand SaveDashboardProfileCommand { get; private set; }
        public ICommand ApplyDashboardProfileCommand { get; private set; }
        public ICommand ImportDashboardProfileCommand { get; private set; }
        public ICommand ExportDashboardProfileCommand { get; private set; }
        public ICommand RequestDeleteProfileCommand { get; private set; }
        public ICommand DeleteDashboardProfileCommand { get; private set; }
        public ICommand CancelDeleteProfileCommand { get; private set; }
        private readonly Dictionary<string, FrameworkElement> profileExternalViews = new Dictionary<string, FrameworkElement>();

        private string UniqueProfileName(string requested)
        {
            var name = string.IsNullOrWhiteSpace(requested) ? "Dashboard" : requested.Trim();
            var result = name;
            for (var suffix = 2; Profiles.Any(profile => string.Equals(profile.Name, result, StringComparison.OrdinalIgnoreCase)); ++suffix) result = name + " (" + suffix + ")";
            return result;
        }
        private void InitializeProfiles()
        {
            SelectedProfile = Profiles.FirstOrDefault();
            RefreshStarterProfiles();
            AddStarterProfileCommand = new RelayCommand<DashboardStarterTemplate>(template => ProfileAction(() => AddStarterProfile(template)), template => CanChangePages && template != null);
            ApplyStarterProfileCommand = new RelayCommand<DashboardStarterTemplate>(async template =>
            {
                if (template == null || !CanChangePages) return;
                try { await ApplyDashboardProfileAsync(AddStarterProfile(template), AppendProfilePages); }
                catch (Exception ex) { LandingPageExtension.logger?.Warn(ex, "Unable to apply starter profile."); ProfileStatus = "Could not apply starter profile: " + ex.Message; }
            }, template => CanChangePages && template != null);
            SaveDashboardProfileCommand = new RelayCommand(() => ProfileAction(() => SaveDashboardProfile(NewProfileName)), () => CanChangePages);
            ApplyDashboardProfileCommand = new RelayCommand(async () => { if (SelectedProfile != null) await ApplyDashboardProfileAsync(SelectedProfile, AppendProfilePages); }, () => CanChangePages && SelectedProfile != null);
            ImportDashboardProfileCommand = new RelayCommand(() =>
            {
                var path = playniteAPI.Dialogs.SelectFile("Dashboard profile|*.json");
                if (!string.IsNullOrWhiteSpace(path)) ProfileAction(() => ImportDashboardProfile(File.ReadAllText(path)));
            }, () => CanChangePages);
            ExportDashboardProfileCommand = new RelayCommand(() =>
            {
                var profile = SelectedProfile;
                if (profile == null) return;
                var path = playniteAPI.Dialogs.SaveFile("Dashboard profile|*.json");
                if (!string.IsNullOrWhiteSpace(path)) ProfileAction(() => { File.WriteAllText(path, DashboardProfiles.Export(profile)); ProfileStatus = "Exported " + profile.Name + "."; });
            }, () => CanChangePages && SelectedProfile != null);
            RequestDeleteProfileCommand = new RelayCommand(() => ConfirmDeleteProfile = true, () => CanChangePages && SelectedProfiles.Count > 0);
            CancelDeleteProfileCommand = new RelayCommand(() => ConfirmDeleteProfile = false);
            DeleteDashboardProfileCommand = new RelayCommand(() => ProfileAction(() =>
            {
                if (SelectedProfiles.Count == 0 || !ConfirmDeleteProfile) return;
                var removed = SelectedProfiles.ToArray();
                foreach (var profile in removed) Profiles.Remove(profile);
                SelectedProfiles.Clear(); SelectedProfile = Profiles.FirstOrDefault();
                ConfirmDeleteProfile = false;
                SaveCurrentPage(); ProfileStatus = "Deleted " + removed.Length + " saved profile(s). Current dashboard unchanged.";
            }), () => CanChangePages && SelectedProfiles.Count > 0 && ConfirmDeleteProfile);
        }
        public void RefreshStarterProfiles()
        {
            StarterProfiles = DashboardStarterTemplates.Create(Settings.Settings, plugin.Id,
                plugin.AllAvailableViews.Values.SelectMany(views => views));
            OnPropertyChanged(nameof(StarterProfiles));
        }
        public DashboardProfile AddStarterProfile(DashboardStarterTemplate template)
        {
            if (template?.Profile == null) throw new ArgumentException("Choose a starter profile.");
            var created = new DashboardProfile
            {
                Name = UniqueProfileName(template.Name), Snapshot = DashboardProfiles.Prepare(template.Profile, plugin.Id)
            };
            Profiles.Add(created); SelectedProfile = created;
            SaveCurrentPage();
            ProfileStatus = "Added " + created.Name + " to saved profiles. Your current dashboard is unchanged until Apply.";
            return created;
        }

        private void ProfileAction(Action action)
        {
            try { action(); }
            catch (Exception ex) { LandingPageExtension.logger?.Warn(ex, "Dashboard profile operation failed."); ProfileStatus = "Profile operation failed: " + ex.Message; }
        }
        public DashboardProfile SaveDashboardProfile(string name)
        {
            CaptureCurrentPage();
            var profile = DashboardProfiles.Capture(Settings.Settings, UniqueProfileName(name));
            Profiles.Add(profile); SelectedProfile = profile;
            SaveCurrentPage(); ProfileStatus = "Saved an independent snapshot: " + profile.Name + ".";
            return profile;
        }
        public DashboardProfile ImportDashboardProfile(string json)
        {
            var profile = DashboardProfiles.Import(json); // Validate completely before touching the list.
            profile.Name = UniqueProfileName(profile.Name);
            Profiles.Add(profile); SelectedProfile = profile;
            SaveCurrentPage(); ProfileStatus = "Imported " + profile.Name + ". Select Apply to use it.";
            return profile;
        }
        private static string ExternalViewKey(ViewProperties view) => view.PluginId + ":" + view.ViewId + ":" + view.InstanceId;

        private async Task ReplaceDashboardAsync(DashboardSnapshot snapshot)
        {
            await HidePageAsync();
            var nodes = pageRoots.Values.SelectMany(root => root.AllNodes).Distinct().ToArray();
            foreach (var node in nodes)
            {
                var properties = node.GridNode.ViewProperties;
                if (properties?.view is FrameworkElement control)
                {
                    if (properties.PluginId != plugin.Id && !(control.Tag as string == "StartPage.MissingWidget"))
                        profileExternalViews[ExternalViewKey(properties)] = control;
                    else if (properties.PluginId == plugin.Id)
                    {
                        if (properties.ViewId != "GameShelves" || plugin.shelvesViewModels.ContainsKey(properties.InstanceId))
                            plugin.OnViewRemoved(properties.ViewId, properties.InstanceId);
                    }
                }
                node.View.Children.Clear();
            }
            pageRoots.Clear(); initializedPages.Clear();
            BackgroundViewModel.IsApplyingPage = true;
            try { DashboardProfiles.ApplyData(Settings.Settings, snapshot); }
            finally { BackgroundViewModel.IsApplyingPage = false; }
            foreach (var page in snapshot.Pages)
                foreach (var node in page.Layout.Enumerate())
                    if (node.ViewProperties is ViewProperties properties && properties.PluginId != plugin.Id
                        && profileExternalViews.TryGetValue(ExternalViewKey(properties), out var cached)) properties.view = cached;
            selectedPage = snapshot.Pages.First(page => page.Id == snapshot.ActivePageId);
            RootNodeViewModel = new GridNodeViewModel(selectedPage.Layout);
            pageRoots[selectedPage.Id] = RootNodeViewModel;
            OnPropertyChanged(nameof(Pages)); OnPropertyChanged(nameof(SelectedPage));
            ApplyPageBackground(selectedPage, true);
            plugin.RefreshDashboardPresentation();
            await RootNodeViewModel.InititalizeGrid();
            await InitializeAndOpenViews();
            initializedPages.Add(selectedPage.Id);
            if (!isPageVisible) await HidePageAsync();
            SaveCurrentPage();
        }

        private static int MissingProfileAssets(DashboardSnapshot snapshot)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var page in snapshot.Pages)
            {
                var background = page.Background;
                if (background.ImageUri?.IsAbsoluteUri == true && background.ImageUri.IsFile) paths.Add(background.ImageUri.LocalPath);
                else if (background.ImageUri == null && !string.IsNullOrWhiteSpace(background.ImagePath)) paths.Add(background.ImagePath);
                foreach (var node in page.Layout.Enumerate())
                    if (node.ViewProperties?.ViewId == "ImageWidget" && !string.IsNullOrWhiteSpace(node.ViewProperties.ImageSettings.ImagePath)) paths.Add(node.ViewProperties.ImageSettings.ImagePath);
            }
            return paths.Count(path => !File.Exists(path) && !Directory.Exists(path));
        }

        public async Task<bool> ApplyDashboardProfileAsync(DashboardProfile profile, bool appendPages = false)
        {
            if (profile == null || !CanChangePages) return false;
            DashboardSnapshot prepared;
            try { prepared = DashboardProfiles.Prepare(profile, plugin.Id); }
            catch (Exception ex) { ProfileStatus = "Cannot apply profile: " + ex.Message; return false; }
            // Persist a full backup before releasing controls or replacing dashboard data.
            var backup = SaveDashboardProfile("Before applying " + profile.Name);
            SelectedProfile = profile;
            var previousShelves = Settings.Settings.ShelveInstanceSettings == null ? null
                : new Dictionary<Guid, ShelvesSettings>(Settings.Settings.ShelveInstanceSettings);
            IsSwitchingPage = true; IsLoading = true;
            try
            {
                if (appendPages)
                {
                    // Existing controls, shared settings, active page and backgrounds stay intact.
                    var names = new HashSet<string>(Pages.Select(page => page.Name), StringComparer.OrdinalIgnoreCase);
                    foreach (var page in prepared.Pages)
                    {
                        var name = page.Name;
                        for (var suffix = 2; !names.Add(page.Name); ++suffix) page.Name = name + " (" + suffix + ")";
                        // Appended external widgets also need independent identities.
                        foreach (var node in page.Layout.Enumerate())
                            if (node.ViewProperties is ViewProperties view && view.PluginId != plugin.Id) view.InstanceId = Guid.NewGuid();
                    }
                    if (Settings.Settings.ShelveInstanceSettings == null) Settings.Settings.ShelveInstanceSettings = new Dictionary<Guid, ShelvesSettings>();
                    foreach (var pair in prepared.Shelves) Settings.Settings.ShelveInstanceSettings[pair.Key] = pair.Value;
                    foreach (var page in prepared.Pages) Pages.Add(page);
                    SaveCurrentPage();
                }
                else await ReplaceDashboardAsync(prepared);
                var unavailable = prepared.Pages.SelectMany(page => page.Layout.Enumerate()).Count(node => (node.ViewProperties?.view as FrameworkElement)?.Tag as string == "StartPage.MissingWidget");
                var missingAssets = MissingProfileAssets(prepared);
                ProfileStatus = (appendPages ? "Added pages from " : "Applied ") + profile.Name + ". Previous dashboard saved as " + backup.Name + "."
                    + (unavailable > 0 ? " " + unavailable + " widget(s) need their extensions enabled." : "")
                    + (missingAssets > 0 ? " " + missingAssets + " local image/folder reference(s) are unavailable." : "");
                return true;
            }
            catch (Exception ex)
            {
                LandingPageExtension.logger?.Error(ex, "Unable to apply dashboard profile; restoring backup.");
                try
                {
                    if (appendPages)
                    {
                        foreach (var page in prepared.Pages) Pages.Remove(page);
                        Settings.Settings.ShelveInstanceSettings = previousShelves;
                        SaveCurrentPage();
                    }
                    else await ReplaceDashboardAsync(DashboardProfiles.Prepare(backup, plugin.Id));
                    ProfileStatus = "Could not apply profile. Previous dashboard restored.";
                }
                catch (Exception restoreError) { LandingPageExtension.logger?.Error(restoreError, "Dashboard backup restore failed."); ProfileStatus = "Apply failed. The saved backup profile remains available: " + backup.Name; }
                return false;
            }
            finally { IsLoading = false; IsSwitchingPage = false; }
        }
    }
}
