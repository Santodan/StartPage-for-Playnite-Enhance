using System;
using System.Windows.Media;
namespace LandingPage.Settings
{
    public enum BackgroundFit { Fill, Fit, Stretch, OriginalSize }
    public enum BackgroundFade { None, Top, Bottom, Left, Right }
    public sealed class BackgroundAppearanceSettings : ObservableObjectExt
    {
        private BackgroundFit fit;
        public BackgroundFit Fit { get => fit; set => SetValue(ref fit,Enum.IsDefined(typeof(BackgroundFit),value) ? value : BackgroundFit.Fill); }
        private double positionX = .5,positionY = .5,zoom = 1,imageOpacity = 1,tintOpacity = .25,fadeOpacity = .7,vignette;
        private static double Number(double value,double fallback,double min,double max) => double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(min,Math.Min(max,value));
        public double PositionX { get => positionX; set => SetValue(ref positionX,Number(value,.5,0,1)); }
        public double PositionY { get => positionY; set => SetValue(ref positionY,Number(value,.5,0,1)); }
        public double Zoom { get => zoom; set => SetValue(ref zoom,Number(value,1,1,4)); }
        public double ImageOpacity { get => imageOpacity; set => SetValue(ref imageOpacity,Number(value,1,0,1)); }
        public double TintOpacity { get => tintOpacity; set => SetValue(ref tintOpacity,Number(value,.25,0,1)); }
        public double FadeOpacity { get => fadeOpacity; set => SetValue(ref fadeOpacity,Number(value,.7,0,1)); }
        public double Vignette { get => vignette; set => SetValue(ref vignette,Number(value,0,0,1)); }
        private bool excludeSidebar;
        public bool ExcludeSidebar { get => excludeSidebar; set => SetValue(ref excludeSidebar,value); }
        private bool showSidebarLine,showTopBarLine;
        public bool ShowSidebarLine { get => showSidebarLine; set => SetValue(ref showSidebarLine,value); }
        public bool ShowTopBarLine { get => showTopBarLine; set => SetValue(ref showTopBarLine,value); }
        private double sidebarLineThickness = 1,topBarLineThickness = 1,lineOpacity = 1;
        public double SidebarLineThickness { get => sidebarLineThickness; set => SetValue(ref sidebarLineThickness,Number(value,1,.5,12)); }
        public double TopBarLineThickness { get => topBarLineThickness; set => SetValue(ref topBarLineThickness,Number(value,1,.5,12)); }
        public double LineOpacity { get => lineOpacity; set => SetValue(ref lineOpacity,Number(value,1,0,1)); }
        private string sidebarLineColor = "#808080",topBarLineColor = "#808080";
        public string SidebarLineColor { get => sidebarLineColor; set => SetValue(ref sidebarLineColor,Color(value)); }
        public string TopBarLineColor { get => topBarLineColor; set => SetValue(ref topBarLineColor,Color(value)); }
        private bool flipHorizontal,flipVertical,useBaseColor,tintEnabled;
        public bool FlipHorizontal { get => flipHorizontal; set => SetValue(ref flipHorizontal,value); }
        public bool FlipVertical { get => flipVertical; set => SetValue(ref flipVertical,value); }
        public bool UseBaseColor { get => useBaseColor; set => SetValue(ref useBaseColor,value); }
        public bool TintEnabled { get => tintEnabled; set => SetValue(ref tintEnabled,value); }
        private BackgroundFade fade;
        public BackgroundFade Fade { get => fade; set => SetValue(ref fade,Enum.IsDefined(typeof(BackgroundFade),value) ? value : BackgroundFade.None); }
        private static string Color(string value) { if(!(ColorConverter.ConvertFromString(value ?? "") is System.Windows.Media.Color)) throw new ArgumentException("Enter a valid color."); return value; }
        private string baseColor = "#202020",tintColor = "#000000",fadeColor = "#000000",vignetteColor = "#000000";
        public string BaseColor { get => baseColor; set => SetValue(ref baseColor,Color(value)); }
        public string TintColor { get => tintColor; set => SetValue(ref tintColor,Color(value)); }
        public string FadeColor { get => fadeColor; set => SetValue(ref fadeColor,Color(value)); }
        public string VignetteColor { get => vignetteColor; set => SetValue(ref vignetteColor,Color(value)); }
        private bool playMedia = true,loopMedia = true,pauseMediaWhenInactive = true,videoMuted = true;
        public bool PlayMedia { get => playMedia; set => SetValue(ref playMedia,value); }
        public bool LoopMedia { get => loopMedia; set => SetValue(ref loopMedia,value); }
        public bool PauseMediaWhenInactive { get => pauseMediaWhenInactive; set => SetValue(ref pauseMediaWhenInactive,value); }
        public bool VideoMuted { get => videoMuted; set => SetValue(ref videoMuted,value); }
        private double videoVolume = .5,playbackSpeed = 1;
        public double VideoVolume { get => videoVolume; set => SetValue(ref videoVolume,Number(value,.5,0,1)); }
        public double PlaybackSpeed { get => playbackSpeed; set => SetValue(ref playbackSpeed,Number(value,1,.25,2)); }
        private int gifFrameRate = 24,gifMaxDimension = 1920;
        public int GifFrameRate { get => gifFrameRate; set => SetValue(ref gifFrameRate,Math.Max(1,Math.Min(60,value))); }
        public int GifMaxDimension { get => gifMaxDimension; set => SetValue(ref gifMaxDimension,Math.Max(0,Math.Min(3840,value))); }
        public void RestartMedia() => NotifyPropertyChanged("RestartMedia",null,null);
        public BackgroundAppearanceSettings Copy() => new BackgroundAppearanceSettings { Fit = Fit,PositionX = PositionX,PositionY = PositionY,Zoom = Zoom,ImageOpacity = ImageOpacity,
            ShowSidebarLine = ShowSidebarLine,ShowTopBarLine = ShowTopBarLine,SidebarLineThickness = SidebarLineThickness,TopBarLineThickness = TopBarLineThickness,LineOpacity = LineOpacity,SidebarLineColor = SidebarLineColor,TopBarLineColor = TopBarLineColor,
            ExcludeSidebar = ExcludeSidebar,TintOpacity = TintOpacity,FadeOpacity = FadeOpacity,Vignette = Vignette,FlipHorizontal = FlipHorizontal,FlipVertical = FlipVertical,UseBaseColor = UseBaseColor,TintEnabled = TintEnabled,
            PlayMedia = PlayMedia,LoopMedia = LoopMedia,PauseMediaWhenInactive = PauseMediaWhenInactive,VideoMuted = VideoMuted,VideoVolume = VideoVolume,PlaybackSpeed = PlaybackSpeed,GifFrameRate = GifFrameRate,GifMaxDimension = GifMaxDimension,
            Fade = Fade,BaseColor = BaseColor,TintColor = TintColor,FadeColor = FadeColor,VignetteColor = VignetteColor };
    }
}
