using Microsoft.UI.Xaml;

namespace TimeSnap;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(560, 700));

        string title = Loc.Get("SettingsWindowTitle");
        Title = title;
        AppTitleBar.Title = title;

        RootFrame.Navigate(typeof(SettingsPage), this);
    }
}
