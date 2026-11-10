using System.Text;
using System.Text.Json;

namespace NOVA.Clip.Core.Models;

public class PayloadData
{
    public string? PlainText { get; set; }
    public string? HtmlText { get; set; }
    public string? RtfText { get; set; }
    public string[]? FilePaths { get; set; }
    public byte[]? ImageData { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }

    public static byte[] Serialize(PayloadData payload)
    {
        string json = JsonSerializer.Serialize(payload);
        return Encoding.UTF8.GetBytes(json);
    }

    public static PayloadData? Deserialize(byte[] data)
    {
        if (data == null || data.Length == 0) return null;
        string json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<PayloadData>(json);
    }
}
