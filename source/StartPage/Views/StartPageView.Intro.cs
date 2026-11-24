using System;
using System.ComponentModel;
using System.Threading.Tasks;
using LandingPage.Models;
using LandingPage.Settings;
using LandingPage.Utils;

namespace LandingPage.Views
{
    // Startup intros (roadmap step 22): the global intro when StartPage opens (the plugin plays a whole-window
    // intro at Playnite start), and per-page intros when switching dashboard pages.
    public partial class StartPageView
    {
        private ViewModels.StartPageViewModel introModel;
        private Guid introPageId;

        private void HookStartupIntro()
        {
            Loaded += (sender,args) => { WatchIntroModel(); BeginIntroOnOpen(); };
            Unloaded += (sender,args) => { if (introModel != null) introModel.PropertyChanged -= IntroModelChanged; introModel = null; };
            DataContextChanged += (sender,args) => { if (IsLoaded) WatchIntroModel(); };
        }

        private void WatchIntroModel()
        {
            if (introModel != null) introModel.PropertyChanged -= IntroModelChanged;
            introModel = DataContext as ViewModels.StartPageViewModel;
            if (introModel != null) { introModel.PropertyChanged += IntroModelChanged; introPageId = introModel.SelectedPage?.Id ?? Guid.Empty; }
        }

        private bool IntroDashboardReady() => introModel == null || (!introModel.IsLoading && introModel.IsSelectedPageInitialized);

        private void BeginIntroOnOpen()
        {
            var model = introModel; if (model == null || IntroHost.Current != null || LaunchIntro.IsRunning) return;
            var settings = model.Settings.Settings;
            var global = settings.StartupIntro;
            if (global.Enabled && (global.Repeat == IntroRepeat.EveryTime || !IntroHost.GlobalPlayed))
            {
                IntroHost.GlobalPlayed = true;
                _ = PlayIntro(global,null);
                return;
            }
            var page = model.SelectedPage;
            if (page != null && page.Intro.Enabled && page.Intro.PlayWhenStartPageOpens && PageIntroDue(page)) _ = PlayIntro(page.Intro,page);
        }

        private void IntroModelChanged(object sender,PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(ViewModels.StartPageViewModel.SelectedPage) || !IsLoaded) return;
            var page = introModel?.SelectedPage; var id = page?.Id ?? Guid.Empty;
            if (id == introPageId) return;
            introPageId = id;
            if (page != null && page.Intro.Enabled && PageIntroDue(page) && IntroHost.Current == null && !LaunchIntro.IsRunning) _ = PlayIntro(page.Intro,page);
        }

        private static bool PageIntroDue(DashboardPage page) => page.Intro.Repeat == IntroRepeat.EveryTime || !IntroHost.PagesPlayed.Contains(page.Id);

        internal async Task<bool> PlayIntro(StartupIntroSettings intro,DashboardPage page)
        {
            if (page != null) IntroHost.PagesPlayed.Add(page.Id);
            var settings = introModel?.Settings.Settings;
            if (settings != null && settings.DashboardRespectWindowsAnimations && !DashboardAnimationsEnabled() && intro.Exit != IntroExit.Cut)
            {
                // Reduced motion: still show the intro, but without fades or exit motion.
                var still = Models.DashboardProfiles.Clone(intro); still.FadeInMs = 0; still.Exit = IntroExit.Cut;
                return await IntroHost.PlayAsync(IntroHost.Target(still.Coverage,this),still,IntroDashboardReady);
            }
            return await IntroHost.PlayAsync(IntroHost.Target(intro.Coverage,this),intro,IntroDashboardReady);
        }
    }
}
