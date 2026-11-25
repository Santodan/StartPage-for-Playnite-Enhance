using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LandingPage.Settings;
using LandingPage.Utils;

namespace LandingPage.Views
{
    // Manages the custom Playnite window sizes shown by the toolbar's window size button.
    public partial class WindowSizeDialog : UserControl
    {
        private readonly ObservableCollection<WindowSizePreset> sizes;
        private readonly Func<Window> target;
        private readonly Action changed;

        // `sizes` is the saved list, `target` returns the window to measure/resize, `changed` saves the settings.
        public WindowSizeDialog(ObservableCollection<WindowSizePreset> sizes,Func<Window> target,Action changed)
        {
            InitializeComponent();
            this.sizes = sizes ?? new ObservableCollection<WindowSizePreset>(); this.target = target; this.changed = changed;
            SizeList.ItemsSource = this.sizes;
            this.sizes.CollectionChanged += (sender,args) => UpdateEmpty();
            UpdateEmpty();
            var current = WindowSizer.CurrentPixels(target?.Invoke());
            if (!current.IsEmpty && current.Width > 0) { WidthInput.Value = (int)current.Width; HeightInput.Value = (int)current.Height; }
        }

        private void UpdateEmpty() => EmptyText.Visibility = sizes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Adds the size from the form; returns it, or null when it duplicates an existing size.
        public WindowSizePreset AddFromForm()
        {
            var width = WidthInput.Value ?? 0; var height = HeightInput.Value ?? 0;
            if (width < WindowSizePreset.MinWidth || height < WindowSizePreset.MinHeight) { Status.Text = $"Enter at least {WindowSizePreset.MinWidth} × {WindowSizePreset.MinHeight} pixels."; return null; }
            var preset = WindowSizePreset.Create(NameInput.Text,width,height);
            var duplicate = sizes.Concat(WindowSizePreset.Defaults).FirstOrDefault(item => item.Width == preset.Width && item.Height == preset.Height && string.Equals(item.Name,preset.Name,StringComparison.CurrentCultureIgnoreCase));
            if (duplicate != null) { Status.Text = "That size is already in the list."; return null; }
            sizes.Add(preset); changed?.Invoke();
            NameInput.Text = string.Empty; Status.Text = "Added " + preset.Label + ".";
            return preset;
        }

        public void Apply(WindowSizePreset preset)
        {
            if (preset == null) return;
            WindowSizer.Apply(target?.Invoke(),preset.Width,preset.Height,out var shrunk);
            Status.Text = shrunk ? $"{preset.Label} is larger than the screen, so the window was reduced to fit." : "Applied " + preset.Label + ".";
        }

        private void Add_Click(object sender,RoutedEventArgs e) => AddFromForm();
        private void AddApply_Click(object sender,RoutedEventArgs e) => Apply(AddFromForm());
        private void UseCurrent_Click(object sender,RoutedEventArgs e)
        {
            var current = WindowSizer.CurrentPixels(target?.Invoke());
            if (current.IsEmpty || current.Width <= 0) { Status.Text = "Playnite's window size is not available."; return; }
            WidthInput.Value = (int)current.Width; HeightInput.Value = (int)current.Height;
            if (string.IsNullOrWhiteSpace(NameInput.Text)) NameInput.Text = "Current size";
            Status.Text = $"Using the current size, {current.Width} × {current.Height}.";
        }
        private void ApplySize_Click(object sender,RoutedEventArgs e) => Apply((sender as FrameworkElement)?.DataContext as WindowSizePreset);
        private void RemoveSize_Click(object sender,RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is WindowSizePreset preset && sizes.Remove(preset)) { changed?.Invoke(); Status.Text = "Removed " + preset.Label + "."; }
        }
    }
}
