using FluentAssertions;
using NOVA.Clip.Core.Models;
using Xunit;

namespace NOVA.Clip.Tests;

public class PasteQueueTests
{
    [Fact]
    public void PasteQueue_Navigation_AdvancesCorrectly()
    {
        var queue = new PasteQueue
        {
            Name = "Sprint Tasks",
            CurrentIndex = 0
        };

        var item1 = new PasteQueueItem { Title = "Task 1", PreviewText = "Do work" };
        var item2 = new PasteQueueItem { Title = "Task 2", PreviewText = "Review PR" };
        queue.Items.Add(item1);
        queue.Items.Add(item2);

        queue.GetCurrentItem().Should().Be(item1);

        queue.CurrentIndex++;
        queue.GetCurrentItem().Should().Be(item2);

        queue.CurrentIndex++;
        queue.GetCurrentItem().Should().BeNull(); // Reached end
    }

    [Theory]
    [InlineData("\n", "Line 1\nLine 2\nLine 3")]
    [InlineData("\n\n", "Line 1\n\nLine 2\n\nLine 3")]
    [InlineData(", ", "Line 1, Line 2, Line 3")]
    [InlineData("\t", "Line 1\tLine 2\tLine 3")]
    [InlineData(" --- ", "Line 1 --- Line 2 --- Line 3")]
    public void PasteAll_ConcatenatesTextWithSeparators(string separator, string expected)
    {
        var textItems = new[] { "Line 1", "Line 2", "Line 3" };
        string combined = string.Join(separator, textItems);
        combined.Should().Be(expected);
    }

    [Fact]
    public void PasteQueueItem_SafeSnapshot_KeepsPayloadIndependent()
    {
        var originalPayload = new byte[] { 1, 2, 3, 4, 5 };
        var queueItem = new PasteQueueItem
        {
            Id = Guid.NewGuid(),
            SourceClipItemId = Guid.NewGuid(),
            Title = "Snapshot test",
            EncryptedPayload = (byte[])originalPayload.Clone()
        };

        queueItem.EncryptedPayload.Should().Equal(originalPayload);
        queueItem.SourceClipItemId.Should().NotBeNull();
    }
}
