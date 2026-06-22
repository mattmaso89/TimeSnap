using Microsoft.UI.Xaml;

namespace TimeSnap;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        RootFrame.Navigate(typeof(MainPage));

        // Hide instead of close so the app keeps running in the tray.
        Closed += (_, args) =>
        {
            args.Handled = true;
            AppWindow.Hide();
        };
    }
}
