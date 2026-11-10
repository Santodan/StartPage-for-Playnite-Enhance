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
using LandingPage.Models.Layout;
using LandingPage.ViewModels.Layout;
using Playnite.SDK;

namespace LandingPage.ViewModels
{
    public partial class StartPageViewModel
    {
        public ObservableCollection<DashboardProfile> Profiles => Settings.Settings.DashboardProfiles;
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
            SaveDashboardProfileCommand = new RelayCommand(() => ProfileAction(() => SaveDashboardProfile(NewProfileName)), () => CanChangePages);
            ApplyDashboardProfileCommand = new RelayCommand(async () => { if (SelectedProfile != null) await ApplyDashboardProfileAsync(SelectedProfile); }, () => CanChangePages && SelectedProfile != null);
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
            RequestDeleteProfileCommand = new RelayCommand(() => ConfirmDeleteProfile = true, () => CanChangePages && SelectedProfile != null);
            CancelDeleteProfileCommand = new RelayCommand(() => ConfirmDeleteProfile = false);
            DeleteDashboardProfileCommand = new RelayCommand(() => ProfileAction(() =>
            {
                if (SelectedProfile == null || !ConfirmDeleteProfile) return;
                Profiles.Remove(SelectedProfile); SelectedProfile = Profiles.FirstOrDefault();
                SaveCurrentPage(); ProfileStatus = "Deleted saved profile. Current dashboard unchanged.";
            }), () => CanChangePages && SelectedProfile != null && ConfirmDeleteProfile);
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
            ApplyPageBackground(selectedPage);
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

        public async Task<bool> ApplyDashboardProfileAsync(DashboardProfile profile)
        {
            if (profile == null || !CanChangePages) return false;
            DashboardSnapshot prepared;
            try { prepared = DashboardProfiles.Prepare(profile, plugin.Id); }
            catch (Exception ex) { ProfileStatus = "Cannot apply profile: " + ex.Message; return false; }
            // Persist a full backup before releasing controls or replacing dashboard data.
            var backup = SaveDashboardProfile("Before applying " + profile.Name);
            SelectedProfile = profile;
            IsSwitchingPage = true; IsLoading = true;
            try
            {
                await ReplaceDashboardAsync(prepared);
                var unavailable = prepared.Pages.SelectMany(page => page.Layout.Enumerate()).Count(node => (node.ViewProperties?.view as FrameworkElement)?.Tag as string == "StartPage.MissingWidget");
                var missingAssets = MissingProfileAssets(prepared);
                ProfileStatus = "Applied " + profile.Name + ". Previous dashboard saved as " + backup.Name + "."
                    + (unavailable > 0 ? " " + unavailable + " widget(s) need their extensions enabled." : "")
                    + (missingAssets > 0 ? " " + missingAssets + " local image/folder reference(s) are unavailable." : "");
                return true;
            }
            catch (Exception ex)
            {
                LandingPageExtension.logger?.Error(ex, "Unable to apply dashboard profile; restoring backup.");
                try { await ReplaceDashboardAsync(DashboardProfiles.Prepare(backup, plugin.Id)); ProfileStatus = "Could not apply profile. Previous dashboard restored."; }
                catch (Exception restoreError) { LandingPageExtension.logger?.Error(restoreError, "Dashboard backup restore failed."); ProfileStatus = "Apply failed. The saved backup profile remains available: " + backup.Name; }
                return false;
            }
            finally { IsLoading = false; IsSwitchingPage = false; }
        }
    }
}
