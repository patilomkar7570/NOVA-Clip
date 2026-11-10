namespace NOVA.Clip.Core.Interfaces;

public interface IPrivacyFilter
{
    bool IsSensitiveText(string text, out string? reason);
    bool IsProcessExcluded(string? processName, IEnumerable<string> excludedList);
}
