using Microsoft.Extensions.Logging.Abstractions;
using NOVA.Clip.Infrastructure.Network;
using Xunit;

namespace NOVA.Clip.Tests;

public class PhoneSyncTwoWayTests
{
    [Fact]
    public void PushTextFromPc_StoresItemAndUpdatesLatest()
    {
        var server = new PhoneSyncServer(NullLogger<PhoneSyncServer>.Instance);

        server.PushTextFromPc("https://github.com/patilomkar7570/NOVA-Clip", "Link", "Chrome");

        Assert.Equal("https://github.com/patilomkar7570/NOVA-Clip", server.GetLatestPcText());
        var items = server.GetRecentPcItems();
        Assert.Single(items);
        Assert.Equal("Link", items[0].Type);
        Assert.Equal("Chrome", items[0].SourceApp);
    }

    [Fact]
    public void PushTextFromPc_IgnoresEmptyOrWhitespace()
    {
        var server = new PhoneSyncServer(NullLogger<PhoneSyncServer>.Instance);

        server.PushTextFromPc("   ");
        server.PushTextFromPc(string.Empty);

        Assert.Null(server.GetLatestPcText());
        Assert.Empty(server.GetRecentPcItems());
    }

    [Fact]
    public void PushTextFromPc_ConsecutiveDuplicates_DoNotDuplicateEntry()
    {
        var server = new PhoneSyncServer(NullLogger<PhoneSyncServer>.Instance);

        server.PushTextFromPc("Same Text", "Text", "Notepad");
        server.PushTextFromPc("Same Text", "Text", "Notepad");

        var items = server.GetRecentPcItems();
        Assert.Single(items);
        Assert.Equal("Same Text", server.GetLatestPcText());
    }

    [Fact]
    public void PushTextFromPc_MaintainsFifoWithMaxCapacity()
    {
        var server = new PhoneSyncServer(NullLogger<PhoneSyncServer>.Instance);

        for (int i = 1; i <= 25; i++)
        {
            server.PushTextFromPc($"Message {i}");
        }

        var items = server.GetRecentPcItems();
        Assert.Equal(20, items.Count); // Max capacity is 20
        Assert.Equal("Message 25", server.GetLatestPcText());
        Assert.Equal("Message 6", items[^1].Text);
    }
}
