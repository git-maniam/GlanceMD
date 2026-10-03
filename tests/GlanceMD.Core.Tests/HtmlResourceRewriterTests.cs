using GlanceMD.Core;

namespace GlanceMD.Core.Tests;

public sealed class HtmlResourceRewriterTests
{
    [Fact]
    public void RewriteImageSources_MapsGeneratedImagesToControlledHost()
    {
        var result = HtmlResourceRewriter.RewriteImageSources("<p><img src=\"images/my file.png\" alt=\"sample\"></p>");
        Assert.Contains("https://resource.glancemd.local/image?src=images%2Fmy%20file.png", result);
        Assert.Contains("alt=\"sample\"", result);
    }

    [Fact]
    public void RewriteImageSources_DoesNotChangeLinks()
    {
        const string html = "<a href=\"image.png\">link</a>";
        Assert.Equal(html, HtmlResourceRewriter.RewriteImageSources(html));
    }
}
