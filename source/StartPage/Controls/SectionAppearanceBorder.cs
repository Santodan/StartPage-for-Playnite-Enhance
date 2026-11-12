using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LandingPage.Models.Layout;
using LandingPage.Settings;

namespace LandingPage.Controls
{
    internal class SectionAppearanceBorder : Border
    {
        private readonly FeaturedSectionLayout layout;
        private readonly FeaturedGameSettings settings;
        private readonly FrameworkElement content;
        private readonly bool customLayout;
        private PanelAppearance observed;
        private bool subscribed;
        internal SectionAppearanceBorder(FeaturedSectionLayout layout, FeaturedGameSettings settings, FrameworkElement content, bool customLayout = false)
        {
            this.layout = layout; this.settings = settings; this.content = content; this.customLayout = customLayout;
            Loaded += (sender,args) => {
                if (!subscribed) { layout.PropertyChanged += ModelChanged; settings.PropertyChanged += ModelChanged; subscribed = true; }
                Rebind();
            };
            Unloaded += (sender,args) => {
                if (subscribed) { layout.PropertyChanged -= ModelChanged; settings.PropertyChanged -= ModelChanged; subscribed = false; }
                if (observed != null) observed.PropertyChanged -= AppearanceChanged;
            };
            Rebind();
        }
        private void ModelChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(FeaturedSectionLayout.OverrideAppearance) || args.PropertyName == nameof(FeaturedSectionLayout.Appearance)
                || args.PropertyName == nameof(FeaturedGameSettings.SectionAppearance)) Rebind();
        }
        private void Rebind()
        {
            if (observed != null) observed.PropertyChanged -= AppearanceChanged;
            observed = layout.OverrideAppearance ? layout.Appearance : settings.SectionAppearance;
            if (subscribed && observed != null) observed.PropertyChanged += AppearanceChanged;
            Apply();
        }
        private void AppearanceChanged(object sender, PropertyChangedEventArgs args) => Apply();
        private void Apply()
        {
            var enabled = observed?.Enabled == true;
            Background = enabled ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(observed.Background)) { Opacity = observed.BackgroundOpacity } : Brushes.Transparent;
            BorderBrush = enabled ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(observed.BorderColor)) : Brushes.Transparent;
            BorderThickness = new Thickness(enabled ? observed.BorderWidth : 0);
            CornerRadius = new CornerRadius(enabled ? observed.CornerRadius : 0);
            Padding = new Thickness(enabled ? observed.Padding : 0);
            Margin = new Thickness(enabled && !customLayout ? observed.Margin : 0);
            Effect = enabled && observed.Shadow ? new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.35 } : null;
            // The section owns its chosen text/icon palette; outer widget colors must not overwrite it.
            WidgetPalette.SetPreserveTextColor(this,enabled && observed.CustomizeText);
            WidgetPalette.SetAppearance(content,observed);
        }
    }
}
