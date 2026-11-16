using System;
using System.Globalization;
using System.Collections.Generic;
using LandingPage.Settings;
using LandingPage.Models;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LandingPage.Models.Layout;

namespace LandingPage.Controls
{
    // A schematic of the actual template tree, not an image of someone else's library.
    public class DashboardTemplatePreview : FrameworkElement
    {
        public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(nameof(Layout), typeof(GridNode), typeof(DashboardTemplatePreview),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public GridNode Layout { get => (GridNode)GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
        public static readonly DependencyProperty ShelvesProperty = DependencyProperty.Register(nameof(Shelves), typeof(Dictionary<Guid, ShelvesSettings>), typeof(DashboardTemplatePreview),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public Dictionary<Guid, ShelvesSettings> Shelves { get => (Dictionary<Guid, ShelvesSettings>)GetValue(ShelvesProperty); set => SetValue(ShelvesProperty, value); }
        public DashboardTemplatePreview() { Height = 160; MinWidth = 260; ClipToBounds = true; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);
            var bounds = new Rect(0, 0, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight));
            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(20,23,29)), null, bounds, 8, 8);
            if (Layout != null && bounds.Width > 8 && bounds.Height > 8) DrawNode(drawing, Layout, new Rect(4,4,bounds.Width-8,bounds.Height-8));
        }
        private void DrawNode(DrawingContext drawing, GridNode node, Rect bounds)
        {
            if (bounds.Width < 5 || bounds.Height < 5) return;
            if (node.Children.Count > 0)
            {
                var horizontal = node.Orientation == Orientation.Horizontal;
                var available = Math.Max(0, (horizontal ? bounds.Width : bounds.Height) - 4 * (node.Children.Count - 1));
                var scale = horizontal ? ActualWidth / 1200 : ActualHeight / 720;
                var fixedSize = node.Children.Where(child => child.Size.IsAbsolute || child.Size.IsAuto).Sum(child => child.Size.IsAuto ? 20 : Math.Max(24, child.Size.Value * scale));
                var stars = node.Children.Where(child => child.Size.IsStar).Sum(child => child.Size.IsStar ? child.Size.Value : 1);
                var offset = 0.0;
                foreach (var child in node.Children)
                {
                    var length = child.Size.IsAuto ? 20 : child.Size.IsAbsolute ? Math.Max(24, child.Size.Value * scale) : Math.Max(0, available-fixedSize) * (child.Size.IsStar ? child.Size.Value : 1) / Math.Max(1,stars);
                    var rectangle = horizontal ? new Rect(bounds.X+offset,bounds.Y,length,bounds.Height) : new Rect(bounds.X,bounds.Y+offset,bounds.Width,length);
                    DrawNode(drawing, child, rectangle); offset += length+4;
                }
                return;
            }
            var view = node.ViewProperties;
            if (view == null) return;
            var fill = new SolidColorBrush(Color.FromRgb(47,53,65));
            var line = new Pen(new SolidColorBrush(Color.FromRgb(83,94,113)), 1);
            drawing.DrawRoundedRectangle(fill, line, bounds, 4, 4);
            var label = view.Appearance.Title;
            if (string.IsNullOrWhiteSpace(label))
                switch (view.ViewId)
                {
                    case "DigitalClock": label = "12:34"; break;
                    case "ToolbarWidget": label = "Page navigation / profiles"; break;
                    case "TextWidget": label = view.TextSettings.Text.Split('\n')[0]; break;
                    case "RecentAchivements": label = "Recent unlocks"; break;
                    case "LibraryStatistics": label = "Library overview"; break;
                    case "LibraryChart": label = "Library charts"; break;
                    case "FeaturedGame": label = "Game spotlight"; break;
                    default: label = GameDetailWidgets.Find(view.ViewId)?.Name ?? "Game shelves"; break;
                }
            if (view.ViewId == "LibraryStatistics" && bounds.Height > 36 && bounds.Width > 40)
            {
                for (var index = 0; index < 6; index++)
                {
                    var x = bounds.X + 8 + index % 3 * (bounds.Width-16)/3;
                    var y = bounds.Y + 23 + index/3 * (bounds.Height-30)/2;
                    drawing.DrawRoundedRectangle(Brushes.LightSkyBlue,null,new Rect(x,y,Math.Max(2,(bounds.Width-30)/4),4),1,1);
                    drawing.DrawRoundedRectangle(Brushes.SlateGray,null,new Rect(x,y+7,Math.Max(2,(bounds.Width-30)/4),2),1,1);
                }
            }
            if (view.ViewId == "LibraryChart" && bounds.Height > 45 && bounds.Width > 40)
            {
                var count = 1+view.LibraryStatisticsSettings.ExtraCharts.Count;
                var height = (bounds.Height-27)/count;
                for(var index = 0; index < count; index++)
                    for(var bar = 0; bar < 3; bar++)
                        drawing.DrawRoundedRectangle(bar == 0 ? Brushes.CornflowerBlue : bar == 1 ? Brushes.MediumSeaGreen : Brushes.Orange,
                            null,new Rect(bounds.X+12,bounds.Y+23+index*height+bar*height/4,(bounds.Width-26)*(0.85-bar*0.2),Math.Max(1,height/6)),1,1);
            }
            if ((view.ViewId == "FeaturedGame" || view.ViewId == "GameDetailArtwork") && bounds.Height > 40 && bounds.Width > 36)
                drawing.DrawRoundedRectangle(Brushes.SteelBlue,null,new Rect(bounds.X+8,bounds.Y+23,bounds.Width-16,bounds.Height-30),3,3);
            if (view.ViewId == "GameShelves" && bounds.Height > 48)
            {
                var rules = Shelves != null && Shelves.TryGetValue(view.InstanceId, out var settings) ? settings.ShelveProperties.ToArray() : new ShelveProperties[0];
                var rows = Math.Max(1, rules.Length);
                var rowHeight = (bounds.Height - 23) / rows;
                for (var row = 0; row < rows; ++row)
                {
                    var rule = row < rules.Length ? rules[row] : null;
                    var top = bounds.Y + 22 + row * rowHeight;
                    if (rule != null && rowHeight > 22)
                    {
                        var title = new FormattedText(rule.Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 8, Brushes.LightGray, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                        { MaxTextWidth = Math.Max(1,bounds.Width-16), Trimming = TextTrimming.CharacterEllipsis };
                        drawing.DrawText(title,new Point(bounds.X+8,top)); top += 12;
                    }
                    var height = Math.Max(3,bounds.Y+22+(row+1)*rowHeight-top-5);
                    if (rule?.DisplayStyle == ShelfDisplayStyle.CompactList)
                    {
                        for (var item = 0; item < Math.Min(6,(int)(height/9)); ++item)
                        {
                            var y = top + item*9;
                            drawing.DrawRoundedRectangle(Brushes.SlateGray,null,new Rect(bounds.X+8,y,6,6),1,1);
                            drawing.DrawRoundedRectangle(Brushes.SlateGray,null,new Rect(bounds.X+18,y+1,Math.Max(4,bounds.Width-28),3),1,1);
                        }
                    }
                    else
                    {
                        var count = Math.Max(2, Math.Min(6, (int)(bounds.Width / (rule?.DisplayStyle == ShelfDisplayStyle.WideCards ? 76 : 38))));
                        for (var item = 0; item < count; ++item)
                        {
                            var width = Math.Max(4,(bounds.Width-16-4*(count-1))/count);
                            var rect = new Rect(bounds.X+8+item*(width+4),top,width,height);
                            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb((byte)(65+item*5),(byte)(76+item*4),(byte)(94+item*3))),null,rect,3,3);
                            if (rule?.ShowAchievementProgress == true && height > 12)
                                drawing.DrawRectangle(Brushes.SteelBlue,null,new Rect(rect.X+2,rect.Bottom-5,Math.Max(1,width*0.65),2));
                        }
                    }
                }
            }
            if (bounds.Width > 16 && bounds.Height > 12)
            {
                var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"),
                    view.ViewId == "DigitalClock" ? Math.Min(28,bounds.Height/2) : 10, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                    { MaxTextWidth = Math.Max(1,bounds.Width-12), MaxTextHeight = Math.Max(1,bounds.Height-6), Trimming = TextTrimming.CharacterEllipsis };
                drawing.DrawText(text,new Point(bounds.X+6,bounds.Y+3));
            }
        }
    }
}
