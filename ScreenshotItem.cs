using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace TimeSnap;

public sealed class ScreenshotItem
{
    public string      FilePath          { get; }
    public string      TimeText          { get; }   // "14:30:45"
    public string      MonitorLabel      { get; }   // "M2" if part of a multi-monitor capture; empty otherwise
    public string      TimeAndMonitorText { get; }  // "14:30:45" or "14:30:45 · M2" — for the thumbnail grid
    public ImageSource Thumbnail         { get; }
    public string      WindowTitle       { get; }   // from sidecar .title file; empty if not present

    public ScreenshotItem(string filePath)
    {
        FilePath = filePath;

        // Multi-monitor captures share one timestamp but get a "_M<n>" filename
        // suffix to stay unique — e.g. "14-30-45_M2". Split it off for display.
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('_', 2);
        TimeText     = parts[0].Replace('-', ':');
        MonitorLabel = parts.Length > 1 ? parts[1] : string.Empty;
        TimeAndMonitorText = string.IsNullOrEmpty(MonitorLabel) ? TimeText : $"{TimeText} · {MonitorLabel}";

        Thumbnail = new BitmapImage(new Uri(filePath)) { DecodePixelWidth = 120 };

        var titleFile = Path.ChangeExtension(filePath, ".title");
        WindowTitle = File.Exists(titleFile) ? File.ReadAllText(titleFile, System.Text.Encoding.UTF8).Trim() : string.Empty;
    }
}
