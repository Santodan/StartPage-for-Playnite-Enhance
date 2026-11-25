using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LandingPage.Models;
using LandingPage.Settings;

namespace LandingPage.Views.Settings
{
    // Editor for one PageTransitionSettings (DataContext): the global transition (extension settings, page manager)
    // or a page's own transition. Saved custom presets (PresetLibrary) are shared by all of them.
    public partial class TransitionSettingsView : UserControl
    {
        public static readonly DependencyProperty PresetLibraryProperty = DependencyProperty.Register(nameof(PresetLibrary),
            typeof(ObservableCollection<DashboardTransitionPreset>),typeof(TransitionSettingsView),new PropertyMetadata(null));
        public ObservableCollection<DashboardTransitionPreset> PresetLibrary
        {
            get => (ObservableCollection<DashboardTransitionPreset>)GetValue(PresetLibraryProperty);
            set => SetValue(PresetLibraryProperty,value);
        }

        // The extension settings window has no dashboard behind it: Preview plays in a box with two sample pages instead.
        // In the page manager (false) Preview raises PreviewRequested and the dashboard plays it on the current page.
        public static readonly DependencyProperty PreviewInBoxProperty = DependencyProperty.Register(nameof(PreviewInBox),typeof(bool),typeof(TransitionSettingsView),
            new PropertyMetadata(false,(sender,args) => ((TransitionSettingsView)sender).UpdatePreviewMode()));
        public bool PreviewInBox { get => (bool)GetValue(PreviewInBoxProperty); set => SetValue(PreviewInBoxProperty,value); }

        // Raised for Preview transition when PreviewInBox is off.
        public event EventHandler PreviewRequested;
        // Raised after the saved preset library changed (save, import, delete) so the host can persist it.
        public event EventHandler PresetsChanged;

        public TransitionSettingsView()
        {
            InitializeComponent(); UpdatePreviewMode();
            Unloaded += (sender,args) => PreviewBox.Stop();
        }

        private void UpdatePreviewMode()
        {
            var box = PreviewInBox ? Visibility.Visible : Visibility.Collapsed;
            PreviewArea.Visibility = box; StopPreviewButton.Visibility = box;
        }

        public PageTransitionSettings Transition => DataContext as PageTransitionSettings;
        public string Status { get => TransitionPresetStatus.Text; set => TransitionPresetStatus.Text = value; }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { Status = ex.Message; LandingPageExtension.logger?.Warn(ex,"Transition preset operation failed."); }
        }

        internal async void PreviewTransition_Click(object sender,RoutedEventArgs e)
        {
            if (!PreviewInBox) { PreviewRequested?.Invoke(this,EventArgs.Empty); return; }
            await PreviewInBoxAsync();
        }

        // Plays the edited transition in the preview box (a copy, so editing during the preview is safe).
        public async System.Threading.Tasks.Task PreviewInBoxAsync()
        {
            var transition = Transition; if (transition == null || PreviewBox.IsPlaying) return;
            try
            {
                if (transition.Transition == DashboardTransition.Custom) TransitionPresets.Validate(transition.Custom);
                if (transition.Transition == DashboardTransition.None && (!transition.Media.Enabled || transition.Media.ResolveMedia() == null))
                { Status = "Choose a transition (or a GIF or video) to preview."; return; }
                Status = transition.Media.Enabled && transition.Media.RepeatsAfterEnd && !transition.Media.Media.LoopMedia
                    ? "Playing preview: the whole clip, then its " + (transition.Media.AfterEnd == MediaEndBehavior.RepeatStart ? "first" : "last") + " " + transition.Media.AfterEndRepeatMs + " ms 3 times..."
                    : "Playing preview...";
                await PreviewBox.PlayAsync(transition.Copy());
                Status = "Preview complete. Press Preview again to switch back.";
            }
            catch (Exception ex) { Status = ex.Message; }
        }

        private void StopPreview_Click(object sender,RoutedEventArgs e) => PreviewBox.Stop();

        internal void SaveTransitionPreset_Click(object sender,RoutedEventArgs e) => Run(() =>
        {
            var library = PresetLibrary ?? throw new InvalidOperationException("Saved presets are not available here.");
            var copy = TransitionPresets.Clone(Transition.Custom);
            var name = copy.Name;
            for (var suffix = 2; library.Any(item => item.Name == copy.Name); ++suffix) copy.Name = name + " (" + suffix + ")";
            library.Add(copy); TransitionPresetPicker.SelectedItem = copy;
            Status = "Saved an independent preset: " + copy.Name;
            PresetsChanged?.Invoke(this,EventArgs.Empty);
        });

        internal void LoadTransitionPreset_Click(object sender,RoutedEventArgs e) => Run(() =>
        {
            if (!(TransitionPresetPicker.SelectedItem is DashboardTransitionPreset selected)) throw new ArgumentException("Select a saved preset.");
            Transition.Custom = TransitionPresets.Clone(selected); Transition.Transition = DashboardTransition.Custom;
            Status = "Loaded " + selected.Name;
        });

        internal void DeleteTransitionPreset_Click(object sender,RoutedEventArgs e) => Run(() =>
        {
            if (TransitionPresetPicker.SelectedItem is DashboardTransitionPreset selected && PresetLibrary != null)
            {
                PresetLibrary.Remove(selected);
                Status = "Deleted saved preset. Current transition is unchanged.";
                PresetsChanged?.Invoke(this,EventArgs.Empty);
            }
        });

        private void ExportTransitionPreset_Click(object sender,RoutedEventArgs e) => Run(() =>
        {
            var json = TransitionPresets.Export(Transition.Custom);
            var path = LandingPageExtension.Instance.PlayniteApi.Dialogs.SaveFile("Transition preset|*.json");
            if (!string.IsNullOrWhiteSpace(path)) { System.IO.File.WriteAllText(path,json); Status = "Exported " + path; }
        });

        private void ImportTransitionPreset_Click(object sender,RoutedEventArgs e) => Run(() =>
        {
            var path = LandingPageExtension.Instance.PlayniteApi.Dialogs.SelectFile("Transition preset|*.json");
            if (string.IsNullOrWhiteSpace(path)) return;
            if (new System.IO.FileInfo(path).Length > 65536) throw new ArgumentException("Choose a JSON file smaller than 64 KiB.");
            var preset = TransitionPresets.Import(System.IO.File.ReadAllText(path));
            var library = PresetLibrary ?? throw new InvalidOperationException("Saved presets are not available here.");
            var name = preset.Name;
            for (var suffix = 2; library.Any(item => string.Equals(item.Name,preset.Name,StringComparison.OrdinalIgnoreCase)); ++suffix)
                preset.Name = name + " (" + suffix + ")";
            library.Add(preset);
            Transition.Custom = TransitionPresets.Clone(preset);
            Transition.Transition = DashboardTransition.Custom;
            TransitionPresetPicker.SelectedItem = preset;
            Status = "Imported and saved preset: " + preset.Name;
            PresetsChanged?.Invoke(this,EventArgs.Empty);
        });
    }
}
