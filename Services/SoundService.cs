namespace TimeSnap.Services;

// Plays a short, subtle "camera click" cue on screenshot capture, using a
// built-in Windows system sound — no external audio file ships with the app.
internal static class SoundService
{
    private static readonly string ClickSoundPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "Media", "Windows Foreground.wav");

    public static void PlayCameraClick()
    {
        try
        {
            if (!File.Exists(ClickSoundPath))
                return;

            using var player = new System.Media.SoundPlayer(ClickSoundPath);
            player.Play(); // plays asynchronously — never blocks the caller
        }
        catch
        {
            // A missing or locked sound file should never break screenshot capture.
        }
    }
}
