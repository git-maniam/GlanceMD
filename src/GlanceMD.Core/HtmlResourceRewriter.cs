using System.Text.RegularExpressions;

namespace GlanceMD.Core;

public static partial class HtmlResourceRewriter
{
    public static string RewriteImageSources(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return ImageSourcePattern().Replace(html, match =>
        {
            var source = match.Groups[2].Value;
            var encoded = Uri.EscapeDataString(source);
            return $"{match.Groups[1].Value}https://resource.glancemd.local/image?src={encoded}{match.Groups[3].Value}";
        });
    }

    [GeneratedRegex("(<img\\b[^>]*?\\bsrc=\\\")([^\"]+)(\\\")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageSourcePattern();
}
