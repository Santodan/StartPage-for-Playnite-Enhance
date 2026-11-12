using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace LandingPage.Controls
{
    internal static class LibraryDescriptionRenderer
    {
        internal static string LastError { get; private set; }
        // Resolve from the running host, without packaging a private Playnite/Cef copy.
        internal static FrameworkElement Create(string html, double size, FontFamily family, Brush color)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == "Playnite")?.GetType("Playnite.Controls.HtmlTextView");
            LastError = null;
            if (type == null) { LastError = "Playnite HtmlTextView type is not loaded."; return null; }
            try
            {
                var native = (FrameworkElement)Activator.CreateInstance(type);
                var wrapper = new ContentControl { Content = native, FontSize = size, FontFamily = family, Margin = new Thickness(0,0,0,10), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top, VerticalAlignment = VerticalAlignment.Top };
                wrapper.SetResourceReference(Control.ForegroundProperty,"TextBrush");
                if (color != null) wrapper.Foreground = color;
                BindingOperations.SetBinding(native,(DependencyProperty)type.GetField("HtmlFontSizeProperty").GetValue(null),new Binding("FontSize") { Source = wrapper });
                BindingOperations.SetBinding(native,(DependencyProperty)type.GetField("HtmlFontFamilyProperty").GetValue(null),new Binding("FontFamily") { Source = wrapper });
                BindingOperations.SetBinding(native,(DependencyProperty)type.GetField("HtmlForegroundProperty").GetValue(null),new Binding("Foreground.Color") { Source = wrapper, FallbackValue = Colors.White });
                var link = (DependencyProperty)type.GetField("LinkForegroundProperty").GetValue(null);
                if (wrapper.TryFindResource("GlyphColor") is Color linkColor) native.SetValue(link,linkColor);
                else BindingOperations.SetBinding(native,link,new Binding("Foreground.Color") { Source = wrapper, FallbackValue = Colors.White });
                type.GetProperty("PartialLoadEnabled").SetValue(native,false);
                type.GetProperty("HtmlText").SetValue(native,html);
                ScrollViewer.SetCanContentScroll(native,false);
                return wrapper;
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
                LandingPageExtension.logger.Warn(ex,"Library HTML description renderer is unavailable; using the native text/image fallback.");
                return null;
            }
        }
    }
}
