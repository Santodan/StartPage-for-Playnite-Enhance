using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
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
        public static string Html(string source, int length, bool images)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;
            source = Regex.Replace(source,@"<script\b[^>]*>.*?</script\s*>",string.Empty,RegexOptions.IgnoreCase|RegexOptions.Singleline,TimeSpan.FromMilliseconds(100));
            source = Regex.Replace(source,@"<img\b[^>]*>",match => {
                if (!images) return string.Empty;
                var image = Parse(match.Value,0,true).Find(part => part.ImageUri != null);
                if (image == null) return match.Value;
                var tag = Regex.Replace(match.Value,"(?<![\\w-])src\\s*=\\s*(?:\"[^\"]*\"|'[^']*'|[^\\s>]+)",string.Empty,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
                return tag.Insert(4," src=\""+WebUtility.HtmlEncode(image.ImageUri.AbsoluteUri)+"\"");
            },RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(200));
            if (length <= 0) return source;
            var result = new StringBuilder(); var open = new Stack<string>(); var remaining = length;
            var voidTags = new HashSet<string>(new[] { "area","base","br","col","embed","hr","img","input","link","meta","param","source","track","wbr" },StringComparer.OrdinalIgnoreCase);
            foreach (Match token in Regex.Matches(source,"<(?:\"[^\"]*\"|'[^']*'|[^'\">])*>|[^<]+|<",RegexOptions.Singleline,TimeSpan.FromMilliseconds(200)))
            {
                if (token.Value.StartsWith("<",StringComparison.Ordinal))
                {
                    var name = Regex.Match(token.Value,@"^<\s*(/?)\s*([a-zA-Z][\w:-]*)");
                    result.Append(token.Value);
                    if (!name.Success) continue;
                    var tag = name.Groups[2].Value.ToLowerInvariant();
                    if (name.Groups[1].Value == "/")
                    {
                        if (open.Contains(tag)) { while (open.Count > 0 && open.Pop() != tag) { } }
                    }
                    else if (!voidTags.Contains(tag) && !token.Value.TrimEnd().EndsWith("/>",StringComparison.Ordinal)) open.Push(tag);
                    continue;
                }
                if (open.Count > 0 && open.Peek() == "style") { result.Append(token.Value); continue; }
                var text = WebUtility.HtmlDecode(token.Value);
                if (text.Length <= remaining) { result.Append(token.Value); remaining -= text.Length; }
                else
                {
                    result.Append(WebUtility.HtmlEncode(text.Substring(0,remaining))).Append("\u2026");
                    break;
                }
            }
            while (open.Count > 0) result.Append("</").Append(open.Pop()).Append('>');
            return result.ToString();
        }
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
                // Lazy descriptions often have an empty/placeholder src before the
                // real data-src. Try each source independently rather than stopping
                // at the first matching attribute.
                Uri image = null;
                foreach (var name in new[] { "data-src", "data-original", "data-lazy-src", "src", "srcset", "data-srcset" })
                {
                    var attribute = Regex.Match(match.Value,"(?<![\\w-])"+name+"\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
                    if (!attribute.Success) continue;
                    var source = WebUtility.HtmlDecode(attribute.Groups[1].Success ? attribute.Groups[1].Value : attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Value).Trim();
                    if (name.EndsWith("srcset",StringComparison.Ordinal)) source = Regex.Split(source,@"\s|,")[0];
                    if (source.StartsWith("//",StringComparison.Ordinal)) source = "https:"+source;
                    if (Uri.TryCreate(source,UriKind.Absolute,out var uri) && (uri.Scheme == "https" || uri.Scheme == "http" || uri.IsFile)) { image = uri; break; }
                }
                if (image != null) parts.Add(new FeaturedDescriptionPart { ImageUri = image });
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
