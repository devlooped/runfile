using System.Net;

namespace Devlooped.Tests;

public class DownloadTests
{
    [Theory]
    [InlineData("https://gist.github.com/kzu/0ac826dc7de666546aaedd38e5965381")]
    [InlineData("https://github.com/kzu/runfile/blob/main/run.cs")]
    [InlineData("https://gitlab.com/kzu/runcs/-/blob/main/program.cs?ref_type=heads")]
    public async Task DownloadPublicUnchanged(string value)
    {
        Assert.True(RemoteRef.TryParse(value, out var location));

        var provider = DownloadProvider.Create(location);
        var contents = await provider.GetAsync(location);

        Assert.True(contents.IsSuccessStatusCode);

        var etag = contents.Headers.ETag?.ToString();

        location = location with
        {
            ETag = etag,
            ResolvedUri = contents.OriginalUri
        };

        var refresh = await provider.GetAsync(location);

        Assert.Equal(HttpStatusCode.NotModified, refresh.StatusCode);
    }

    [LocalTheory]
    // Requires being authenticated with private kzu's GH repo
    [InlineData("github.com/kzu/runfile")]
    [InlineData("github.com/kzu/runfile@v0.1.0")]
    [InlineData("github.com/kzu/runfile@dev")]
    [InlineData("github.com/kzu/runfile@211de7614553152d848ef53dd9587d1a52c76582")]
    [InlineData("github.com/kzu/runfile@211de7614")]
    [InlineData("github.com/kzu/runfile@211de761455")]
    // Requires running the CLI app once against this private repo and saving a PAT
    [InlineData("https://gitlab.com/kzu/runfile/-/blob/main/program.cs?ref_type=heads")]
    [InlineData("gitlab.com/kzu/runfile")]
    [InlineData("gitlab.com/kzu/runfile@v0.1.0")]
    [InlineData("gitlab.com/kzu/runfile@dev")]
    [InlineData("gitlab.com/kzu/runfile@533ecac61d4cf62dac0c72567e73753acd235ac2")]
    [InlineData("gitlab.com/kzu/runfile@533ecac61")]
    [InlineData("gitlab.com/kzu/runfile@533ecac61d4")]
    // Also private auth
    //[InlineData("bitbucket.org/kzu/runfile")]
    //[InlineData("bitbucket.org/kzu/runfile@v0.1.0")]
    //[InlineData("bitbucket.org/kzu/runfile@dev")]
    public async Task DownloadPrivateUnchanged(string value)
    {
        Assert.True(RemoteRef.TryParse(value, out var location));

        var provider = DownloadProvider.Create(location);
        var contents = await provider.GetAsync(location);

        Assert.True(contents.IsSuccessStatusCode);

        var etag = contents.Headers.ETag?.ToString();

        location = location with
        {
            ETag = etag,
            ResolvedUri = contents.OriginalUri
        };

        var refresh = await provider.GetAsync(location);

        Assert.Equal(HttpStatusCode.NotModified, refresh.StatusCode);
    }
}
