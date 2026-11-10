using FluentAssertions;
using NOVA.Clip.Core.Models;
using Xunit;

namespace NOVA.Clip.Tests;

public class ClipboardServiceLogicTests
{
    [Fact]
    public void PayloadData_SerializationRoundtrip_PreservesAllFields()
    {
        var payload = new PayloadData
        {
            PlainText = "Hello world!",
            HtmlText = "<b>Hello world!</b>",
            RtfText = @"{\rtf1\ansi Hello world!}",
            FilePaths = [@"C:\temp\doc1.txt", @"C:\temp\image2.png"],
            ImageData = [10, 20, 30, 40],
            ImageWidth = 1920,
            ImageHeight = 1080
        };

        byte[] serialized = PayloadData.Serialize(payload);
        serialized.Should().NotBeNullOrEmpty();

        var deserialized = PayloadData.Deserialize(serialized);
        deserialized.Should().NotBeNull();
        deserialized!.PlainText.Should().Be("Hello world!");
        deserialized.HtmlText.Should().Be("<b>Hello world!</b>");
        deserialized.RtfText.Should().Be(@"{\rtf1\ansi Hello world!}");
        deserialized.FilePaths.Should().Equal(@"C:\temp\doc1.txt", @"C:\temp\image2.png");
        deserialized.ImageData.Should().Equal(10, 20, 30, 40);
        deserialized.ImageWidth.Should().Be(1920);
        deserialized.ImageHeight.Should().Be(1080);
    }

    [Fact]
    public void StorageUsageInfo_Formatting_DisplaysHumanReadable()
    {
        var usage = new StorageUsageInfo();
        usage.FormatBytes(500).Should().Be("500 B");
        usage.FormatBytes(1024 * 150).Should().Be("150.0 KB");
        usage.FormatBytes(1024 * 1024 * 42).Should().Be("42.0 MB");
        usage.FormatBytes(1024L * 1024L * 1024L * 3L).Should().Be("3.00 GB");
    }

    [Fact]
    public void ClipItem_Clone_CreatesDeepIndependentCopy()
    {
        var item = new ClipItem
        {
            Id = Guid.NewGuid(),
            Type = ClipContentType.Image,
            Title = "Clone test",
            EncryptedPayload = [1, 2, 3],
            IsFavorite = true
        };

        var clone = item.Clone();
        clone.Id.Should().Be(item.Id);
        clone.Title.Should().Be(item.Title);
        clone.IsFavorite.Should().BeTrue();
        clone.EncryptedPayload.Should().Equal(item.EncryptedPayload);

        clone.EncryptedPayload![0] = 99;
        item.EncryptedPayload[0].Should().Be(1); // Original unchanged
    }
}
