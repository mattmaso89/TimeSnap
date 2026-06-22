using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace TimeSnap;

public sealed class ScreenshotItem
{
    public string      FilePath    { get; }
    public string      TimeText    { get; }   // "14:30:45"
    public ImageSource Thumbnail   { get; }
    public string      WindowTitle { get; }   // from sidecar .title file; empty if not present

    public ScreenshotItem(string filePath)
    {
        FilePath  = filePath;
        TimeText  = Path.GetFileNameWithoutExtension(filePath).Replace('-', ':');
        Thumbnail = new BitmapImage(new Uri(filePath)) { DecodePixelWidth = 120 };

        var titleFile = Path.ChangeExtension(filePath, ".title");
        WindowTitle = File.Exists(titleFile) ? File.ReadAllText(titleFile, System.Text.Encoding.UTF8).Trim() : string.Empty;
    }
}
