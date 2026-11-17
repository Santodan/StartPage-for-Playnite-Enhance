using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
namespace LandingPage.Utils
{
    // Keep one composited native frame and one reusable pixel buffer, not all decoded frames.
    public sealed class BackgroundGifDecoder : IDisposable
    {
        private const int MaxBytes = 64*1024*1024;
        private readonly MemoryStream stream;
        private readonly Image image;
        private readonly Bitmap bitmap;
        private readonly Graphics graphics;
        private readonly long[] frameEnds;
        public int Width { get; }
        public int Height { get; }
        public int NaturalWidth { get; }
        public int NaturalHeight { get; }
        public int FrameCount => frameEnds.Length;
        public long Duration => frameEnds[frameEnds.Length-1];
        public byte[] Pixels { get; }
        private int current = -1;
        public BackgroundGifDecoder(byte[] bytes,int maxWidth)
        {
            if(bytes == null || bytes.Length>MaxBytes) throw new ArgumentException("GIF files must be smaller than 64 MiB.");
            try {
                stream = new MemoryStream(bytes,false); image = Image.FromStream(stream,true,true);
                NaturalWidth = image.Width; NaturalHeight = image.Height;
                if(NaturalWidth>8192 || NaturalHeight>8192 || (long)NaturalWidth*NaturalHeight>16000000) throw new ArgumentException("GIF dimensions are too large; use a video background instead.");
                var count = image.GetFrameCount(FrameDimension.Time); if(count<1 || count>5000) throw new ArgumentException("Unsupported GIF frame count.");
                frameEnds = new long[count]; byte[] delays = null;
                try { delays = image.GetPropertyItem(0x5100).Value; } catch(ArgumentException) { }
                long duration = 0;
                for(var index=0;index<count;index++) { var delay = delays != null && delays.Length>=(index+1)*4 ? BitConverter.ToUInt32(delays,index*4)*10L : 100; duration += delay == 0 ? 100 : Math.Max(20,Math.Min(600000,delay)); frameEnds[index] = duration; }
                var scale = maxWidth>0 ? Math.Min(1,(double)maxWidth/Math.Max(NaturalWidth,NaturalHeight)) : 1;
                Width = Math.Max(1,(int)Math.Round(NaturalWidth*scale)); Height = Math.Max(1,(int)Math.Round(NaturalHeight*scale));
                bitmap = new Bitmap(Width,Height,PixelFormat.Format32bppPArgb); graphics = Graphics.FromImage(bitmap);
                graphics.CompositingMode = CompositingMode.SourceCopy; graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                Pixels = new byte[Width*Height*4]; Render(0);
            } catch { Dispose(); throw; }
        }
        public int FrameAt(double milliseconds,bool loop)
        {
            var time = Math.Max(0,(long)milliseconds); time = loop ? time%Duration : Math.Min(Duration-1,time);
            var index = Array.BinarySearch(frameEnds,time); return index>=0 ? Math.Min(FrameCount-1,index+1) : Math.Min(FrameCount-1,~index);
        }
        public bool Render(int frame)
        {
            if(frame == current) return false;
            image.SelectActiveFrame(FrameDimension.Time,Math.Max(0,Math.Min(FrameCount-1,frame)));
            graphics.Clear(Color.Transparent); graphics.DrawImage(image,new Rectangle(0,0,Width,Height));
            var data = bitmap.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            try { for(var row=0;row<Height;row++) Marshal.Copy(IntPtr.Add(data.Scan0,row*data.Stride),Pixels,row*Width*4,Width*4); }
            finally { bitmap.UnlockBits(data); }
            current = frame; return true;
        }
        public static async Task<BackgroundGifDecoder> OpenAsync(Uri uri,int width,CancellationToken cancellation)
        {
            byte[] bytes;
            if(uri.IsFile) {
                var info = new FileInfo(uri.LocalPath); if(info.Length>MaxBytes) throw new ArgumentException("GIF files must be smaller than 64 MiB.");
                bytes = await Task.Run(() => File.ReadAllBytes(uri.LocalPath),cancellation).ConfigureAwait(false);
            } else {
                using(var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                using(var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation)) {
                    timeout.CancelAfter(TimeSpan.FromSeconds(15)); client.DefaultRequestHeaders.UserAgent.ParseAdd("StartPage-Enhanced");
                    using(var response = await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false)) {
                        response.EnsureSuccessStatusCode(); if(response.Content.Headers.ContentLength>MaxBytes) throw new ArgumentException("GIF files must be smaller than 64 MiB.");
                        using(var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using(var output = new MemoryStream()) {
                            var buffer = new byte[8192]; int count;
                            while((count = await input.ReadAsync(buffer,0,buffer.Length,timeout.Token).ConfigureAwait(false))>0) { if(output.Length+count>MaxBytes) throw new ArgumentException("GIF is too large."); output.Write(buffer,0,count); }
                            bytes = output.ToArray();
                        }
                    }
                }
            }
            cancellation.ThrowIfCancellationRequested(); return await Task.Run(() => new BackgroundGifDecoder(bytes,width),cancellation).ConfigureAwait(false);
        }
        public void Dispose() { graphics?.Dispose(); bitmap?.Dispose(); image?.Dispose(); stream?.Dispose(); }
    }
}
