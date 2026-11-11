using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LandingPage.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TransitionEasing { Linear, Quadratic, Cubic, Sine }

    public class TransitionMotion : ObservableObjectExt
    {
        private double startOpacity = 1, endOpacity = 1, startScale = 1, endScale = 1;
        private double startX, endX, startY, endY;
        private int delayMs;
        public double StartOpacity { get => startOpacity; set => SetValue(ref startOpacity, value); }
        public double EndOpacity { get => endOpacity; set => SetValue(ref endOpacity, value); }
        public double StartScale { get => startScale; set => SetValue(ref startScale, value); }
        public double EndScale { get => endScale; set => SetValue(ref endScale, value); }
        public double StartX { get => startX; set => SetValue(ref startX, value); }
        public double EndX { get => endX; set => SetValue(ref endX, value); }
        public double StartY { get => startY; set => SetValue(ref startY, value); }
        public double EndY { get => endY; set => SetValue(ref endY, value); }
        public int DelayMs { get => delayMs; set => SetValue(ref delayMs, value); }
    }

    public class DashboardTransitionPreset : ObservableObjectExt
    {
        private string name = "My transition";
        private int durationMs = 200;
        private TransitionEasing easing = TransitionEasing.Quadratic;
        private bool reverseOnPrevious = true;
        public string Name { get => name; set => SetValue(ref name, value); }
        public int DurationMs { get => durationMs; set => SetValue(ref durationMs, value); }
        public TransitionEasing Easing { get => easing; set => SetValue(ref easing, value); }
        public bool ReverseOnPrevious { get => reverseOnPrevious; set => SetValue(ref reverseOnPrevious, value); }
        public TransitionMotion Incoming { get; set; } = new TransitionMotion { StartOpacity = 0, StartScale = 0.96, StartX = 10 };
        public TransitionMotion Outgoing { get; set; } = new TransitionMotion { EndOpacity = 0, EndX = -10 };
    }

    public static class TransitionPresets
    {
        private class Document
        {
            public string Format { get; set; } = "StartPage.TransitionPreset";
            public int Version { get; set; } = 1;
            public DashboardTransitionPreset Preset { get; set; }
        }
        private static JsonSerializerSettings Options() => new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error,
            CheckAdditionalContent = true, MaxDepth = 16, ObjectCreationHandling = ObjectCreationHandling.Replace
        };
        public static void Validate(DashboardTransitionPreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.Name) || preset.Name.Length > 100
                || preset.DurationMs < 50 || preset.DurationMs > 1000 || !Enum.IsDefined(typeof(TransitionEasing), preset.Easing))
                throw new ArgumentException("Use a name (1-100 characters), duration 50-1000 ms and a supported easing.");
            foreach (var motion in new[] { preset.Incoming, preset.Outgoing })
            {
                if (motion == null || motion.DelayMs < 0 || motion.DelayMs > 500
                    || !Range(motion.StartOpacity, 0, 1) || !Range(motion.EndOpacity, 0, 1)
                    || !Range(motion.StartScale, 0.5, 1.5) || !Range(motion.EndScale, 0.5, 1.5)
                    || !new[] { motion.StartX, motion.EndX, motion.StartY, motion.EndY }.All(value => Range(value, -100, 100)))
                    throw new ArgumentException("Opacity must be 0-1, scale 0.5-1.5, movement -100 to 100 percent and delay 0-500 ms.");
            }
        }
        private static bool Range(double value, double min, double max) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;
        public static string Export(DashboardTransitionPreset preset)
        {
            Validate(preset);
            return JsonConvert.SerializeObject(new Document { Preset = preset }, Formatting.Indented, Options());
        }
        public static DashboardTransitionPreset Import(string json)
        {
            if (json == null || json.Length > 65536) throw new ArgumentException("Choose a transition JSON file smaller than 64 KiB.");
            var header = Newtonsoft.Json.Linq.JObject.Parse(json);
            if ((string)header["Format"] != "StartPage.TransitionPreset" || (int?)header["Version"] != 1)
                throw new ArgumentException("Unsupported transition preset format/version.");
            var document = JsonConvert.DeserializeObject<Document>(json, Options());
            if (document?.Format != "StartPage.TransitionPreset" || document.Version != 1) throw new ArgumentException("Unsupported transition preset format/version.");
            Validate(document.Preset);
            return document.Preset;
        }
        public static DashboardTransitionPreset Clone(DashboardTransitionPreset preset) => Import(Export(preset));
    }
}
