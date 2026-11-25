using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using LandingPage.Settings;

namespace LandingPage.Controls
{
    // A small stand-in dashboard with two sample pages. PlayAsync runs a transition between them with the same player
    // the dashboard uses, so the extension settings (which have no dashboard behind them) can preview transitions,
    // including the GIF/video and its size option. Each play switches to the other sample page.
    public sealed class TransitionPreviewBox : Grid
    {
        private readonly Border first, second;
        // Previews show the whole clip and, when it repeats a part after its end, that part three times.
        private readonly TransitionMediaLayer media = new TransitionMediaLayer { PreviewRepeats = 3 };
        private TaskCompletionSource<bool> stop;

        public TransitionPreviewBox()
        {
            ClipToBounds = true; Height = 190;
            first = SamplePage("Page 1", Color.FromRgb(0x2B,0x4C,0x7E), Color.FromRgb(0x14,0x1E,0x30));
            second = SamplePage("Page 2", Color.FromRgb(0x7A,0x3E,0x6B), Color.FromRgb(0x24,0x14,0x22));
            second.Visibility = Visibility.Collapsed;
            Children.Add(first); Children.Add(second); Children.Add(media);
        }

        public bool IsPlaying { get; private set; }
        public bool ShowingFirst { get; private set; } = true;
        public TransitionMediaLayer MediaLayer => media;
        public TransitionPlayer LastPlayer { get; private set; }

        // Plays `transition` from the shown sample page to the other one. Returns false when nothing was played.
        public async Task<bool> PlayAsync(PageTransitionSettings transition)
        {
            if (IsPlaying || transition == null) return false;
            var outgoing = ShowingFirst ? first : second; var incoming = ShowingFirst ? second : first;
            var mediaFile = transition.Media.Enabled ? transition.Media.ResolveMedia() : null;
            IsPlaying = true; stop = new TaskCompletionSource<bool>();
            var player = new TransitionPlayer(outgoing, incoming, media); LastPlayer = player;
            try
            {
                Panel.SetZIndex(outgoing, TransitionPlayer.OutgoingZ); Panel.SetZIndex(incoming, 0);
                outgoing.Visibility = Visibility.Visible; incoming.Visibility = Visibility.Visible;
                UpdateLayout();
                if (transition.Transition != DashboardTransition.None || mediaFile != null)
                {
                    player.Configure(transition, ShowingFirst, mediaFile);
                    await player.PlayAsync(stop.Task, null);
                }
                return true;
            }
            finally
            {
                player.Reset();
                outgoing.Visibility = Visibility.Collapsed; Panel.SetZIndex(outgoing, 0);
                ShowingFirst = !ShowingFirst; IsPlaying = false;
            }
        }

        public void Stop() => stop?.TrySetResult(true);

        private static Border SamplePage(string title, Color top, Color bottom)
        {
            var background = new LinearGradientBrush(top, bottom, 90); background.Freeze();
            var page = new Border { Background = background, CornerRadius = new CornerRadius(4) };
            var layout = new Grid { Margin = new Thickness(10) };
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            layout.RowDefinitions.Add(new RowDefinition());
            var bar = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(0x55,0xFF,0xFF,0xFF)), RadiusX = 3, RadiusY = 3 };
            layout.Children.Add(bar);
            var label = new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            layout.Children.Add(label);
            var tiles = new UniformGrid { Columns = 4, Rows = 2, Margin = new Thickness(0,8,0,0) };
            for (var index = 0; index < 8; index++)
                tiles.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromArgb((byte)(0x30 + index * 6),0xFF,0xFF,0xFF)), RadiusX = 3, RadiusY = 3, Margin = new Thickness(3) });
            Grid.SetRow(tiles, 1); layout.Children.Add(tiles);
            page.Child = layout;
            return page;
        }
    }
}
