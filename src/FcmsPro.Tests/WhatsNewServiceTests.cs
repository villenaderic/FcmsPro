using FcmsPro.Avalonia.Services;
using Xunit;

namespace FcmsPro.Tests;

public class WhatsNewServiceTests
{
    [Fact]
    public void GetReleaseNotesForVersion_ExistingVersion_ReturnsContent()
    {
        var notes = WhatsNewService.GetReleaseNotesForVersion("1.6.9");
        Assert.NotNull(notes);
        Assert.NotEmpty(notes);
        Assert.Contains("Fixed", notes);
    }

    [Fact]
    public void GetReleaseNotesForVersion_UnknownVersion_ReturnsEmpty()
    {
        var notes = WhatsNewService.GetReleaseNotesForVersion("999.999.999");
        Assert.Equal(string.Empty, notes);
    }
}
