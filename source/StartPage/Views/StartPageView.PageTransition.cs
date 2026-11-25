using System;
using System.ComponentModel;
using System.Windows;
using LandingPage.Models;
using LandingPage.Settings;

namespace LandingPage.Views
{
    // Manage dashboard pages > Dashboard transitions: the selected page uses the global transition or its own.
    // The editor shows whichever applies; "Copy from global", "Copy to global" and "Use on all dashboards" move
    // settings between them. The global transition is also edited in the extension settings (Transitions tab).
    public partial class StartPageView
    {
        private ViewModels.StartPageViewModel transitionModel;
        private DashboardPage transitionPage;

        private void WatchPageTransitions(ViewModels.StartPageViewModel model)
        {
            if (transitionModel != null) transitionModel.PropertyChanged -= TransitionModelChanged;
            transitionModel = model;
            if (transitionModel != null) transitionModel.PropertyChanged += TransitionModelChanged;
            UpdateTransitionEditor();
        }

        private void TransitionModelChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ViewModels.StartPageViewModel.SelectedPage)) UpdateTransitionEditor();
        }

        private void TransitionPageChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(DashboardPage.UseOwnTransition) || args.PropertyName == nameof(DashboardPage.Transition)) UpdateTransitionEditor();
        }

        private LandingPageSettings TransitionSettings => (DataContext as ViewModels.StartPageViewModel)?.Settings?.Settings;

        // The transition the page manager edits: the selected page's own when it uses one, else the global transition.
        internal PageTransitionSettings EditedTransition
        {
            get
            {
                var settings = TransitionSettings; if (settings == null) return null;
                return DashboardPages.EffectiveTransition(settings, (DataContext as ViewModels.StartPageViewModel)?.SelectedPage);
            }
        }

        internal void UpdateTransitionEditor()
        {
            var model = DataContext as ViewModels.StartPageViewModel; var settings = TransitionSettings;
            var page = model?.SelectedPage;
            if (!ReferenceEquals(page, transitionPage))
            {
                if (transitionPage != null) transitionPage.PropertyChanged -= TransitionPageChanged;
                transitionPage = page;
                if (transitionPage != null) transitionPage.PropertyChanged += TransitionPageChanged;
            }
            if (settings == null) { PageTransitionEditor.DataContext = null; return; }
            var own = page != null && page.UseOwnTransition && page.Transition != null;
            PageTransitionEditor.PresetLibrary = settings.DashboardTransitionPresets;
            PageTransitionEditor.DataContext = own ? page.Transition : settings.GlobalTransition;
            PageOwnTransitionToggle.IsChecked = own;
            PageOwnTransitionToggle.IsEnabled = page != null;
            TransitionScopeText.Text = own
                ? "Editing this page's own transition. It plays when you switch to \"" + page.Name + "\"."
                : "Editing the global transition, used by every page without its own.";
            CopyTransitionFromGlobalButton.IsEnabled = own;
            CopyTransitionToGlobalButton.IsEnabled = own;
        }

        private void SaveTransitionChange() => (DataContext as ViewModels.StartPageViewModel)?.SaveCurrentPage();

        private void PageOwnTransition_Click(object sender, RoutedEventArgs e)
        {
            var settings = TransitionSettings; var page = (DataContext as ViewModels.StartPageViewModel)?.SelectedPage;
            if (settings == null || page == null) return;
            if (PageOwnTransitionToggle.IsChecked == true) DashboardPages.UseOwnTransition(settings, page);
            else page.UseOwnTransition = false;
            PageTransitionEditor.Status = page.UseOwnTransition ? "This page now has its own transition (its last own settings, or a copy of the global one)." : "This page now uses the global transition. Its own settings are kept if you turn it back on.";
            UpdateTransitionEditor(); SaveTransitionChange();
        }

        private void CopyTransitionFromGlobal_Click(object sender, RoutedEventArgs e)
        {
            var settings = TransitionSettings; var page = (DataContext as ViewModels.StartPageViewModel)?.SelectedPage;
            if (settings == null || page?.Transition == null || !page.UseOwnTransition) return;
            page.Transition.CopyFrom(settings.GlobalTransition);
            PageTransitionEditor.Status = "Copied the global transition to this page.";
            SaveTransitionChange();
        }

        private void CopyTransitionToGlobal_Click(object sender, RoutedEventArgs e)
        {
            var settings = TransitionSettings; var page = (DataContext as ViewModels.StartPageViewModel)?.SelectedPage;
            if (settings == null || page?.Transition == null || !page.UseOwnTransition) return;
            settings.GlobalTransition.CopyFrom(page.Transition);
            PageTransitionEditor.Status = "This page's transition is now the global transition, used by every page without its own.";
            SaveTransitionChange();
        }

        private void UseTransitionOnAll_Click(object sender, RoutedEventArgs e)
        {
            var settings = TransitionSettings; var edited = EditedTransition;
            if (settings == null || edited == null) return;
            DashboardPages.UseOnAllPages(settings, edited);
            PageTransitionEditor.Status = "Every dashboard now uses this transition. Pages keep their own settings in case you turn them back on.";
            UpdateTransitionEditor(); SaveTransitionChange();
        }

        private void PageTransitionEditor_PresetsChanged(object sender, EventArgs e) => SaveTransitionChange();

        private async void PageTransitionEditor_PreviewRequested(object sender, EventArgs e)
        {
            var editor = PageTransitionEditor; var transition = editor.Transition;
            if (!(DataContext is ViewModels.StartPageViewModel model) || !model.CanChangePages || transitionCompletion != null || transition == null) return;
            try
            {
                if (transition.Transition == DashboardTransition.Custom) TransitionPresets.Validate(transition.Custom);
                // Like the settings preview box: the whole clip, then a repeated part three times.
                DashboardTransitionMedia.PreviewRepeats = 3;
                BeginDashboardTransition(this, new ViewModels.DashboardTransitionEventArgs { Forward = true, Transition = transition });
                if (DashboardTransitionImage.Source == null) { editor.Status = "Choose a transition (or a GIF or video) and enable animations to preview."; return; }
                await CompleteDashboardTransition();
                editor.Status = "Preview complete. Switch pages to test with different content.";
            }
            catch (Exception ex) { ResetDashboardTransition(); editor.Status = ex.Message; }
            finally { DashboardTransitionMedia.PreviewRepeats = 0; }
        }
    }
}
