using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace LandingPage.Models
{
    public sealed class FeaturedDescriptionPart
    {
        public string Text { get; set; }
        public Uri ImageUri { get; set; }
    }
    public static class FeaturedDescription
    {
        public static List<FeaturedDescriptionPart> Parse(string html, int length, bool images)
        {
            var parts = new List<FeaturedDescriptionPart>();
            if (string.IsNullOrWhiteSpace(html)) return parts;
            html = Regex.Replace(html,@"<!--.*?-->",string.Empty,RegexOptions.Singleline,TimeSpan.FromMilliseconds(100));
            html = Regex.Replace(html,@"<(script|style)\b[^>]*>.*?</\1\s*>",string.Empty,RegexOptions.IgnoreCase|RegexOptions.Singleline,TimeSpan.FromMilliseconds(100));
            if (!images) { parts.Add(new FeaturedDescriptionPart { Text = FeaturedGameSelection.Description(html,length) }); return parts; }
            var remaining = length <= 0 ? int.MaxValue : length;
            var offset = 0;
            foreach (Match match in Regex.Matches(html,@"<img\b[^>]*>",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100)))
            {
                if (!Append(parts,html.Substring(offset,match.Index-offset),ref remaining)) return parts;
                var attribute = Regex.Match(match.Value,"\\b(?:src|data-src)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
                var source = WebUtility.HtmlDecode(attribute.Groups[1].Success ? attribute.Groups[1].Value : attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Value).Trim();
                if (source.StartsWith("//",StringComparison.Ordinal)) source = "https:"+source;
                if (Uri.TryCreate(source,UriKind.Absolute,out var uri) && (uri.Scheme == "https" || uri.Scheme == "http" || uri.IsFile))
                    parts.Add(new FeaturedDescriptionPart { ImageUri = uri });
                offset = match.Index+match.Length;
            }
            Append(parts,html.Substring(offset),ref remaining);
            return parts;
        }
        private static bool Append(List<FeaturedDescriptionPart> parts, string html, ref int remaining)
        {
            var text = FeaturedGameSelection.Description(html,0);
            if (text.Length == 0) return true;
            if (remaining <= 0) return false;
            var truncated = text.Length > remaining;
            parts.Add(new FeaturedDescriptionPart { Text = truncated ? text.Substring(0,remaining).TrimEnd()+"\u2026" : text });
            remaining -= Math.Min(remaining,text.Length);
            return !truncated && remaining > 0;
        }
    }
}
