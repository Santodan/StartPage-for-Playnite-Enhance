using System;
using System.Windows.Media;

namespace LandingPage.Settings
{
    public class ImageWidgetSettings : ObservableObjectExt
    {
        private string imagePath = string.Empty;
        public string ImagePath { get => imagePath; set => SetValue(ref imagePath, value ?? string.Empty); }
        private Stretch stretch = Stretch.Uniform;
        public Stretch Stretch { get => stretch; set => SetValue(ref stretch, value); }
        private double opacity = 1;
        public double Opacity { get => opacity; set => SetValue(ref opacity,
            double.IsNaN(value) || double.IsInfinity(value) ? 1 : Math.Max(0, Math.Min(1, value))); }
        private string link = string.Empty;
        public string Link
        {
            get => link;
            set
            {
                var candidate = (value ?? string.Empty).Trim();
                if (candidate.Length > 0 && (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
                    throw new ArgumentException("Enter a website address starting with https:// or http://.");
                SetValue(ref link, candidate);
            }
        }
    }
}
