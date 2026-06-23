using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TimeSnap.Services;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace TimeSnap;

public sealed partial class SettingsPage : Page
{
    private Window? _hostWindow;

    private bool _isRecordingHotkey;
    private uint _capturedHotkeyModifiers;
    private uint _pendingHotkeyModifiers;
    private uint _pendingHotkeyVirtualKey;

    private readonly List<CheckBox> _monitorCheckBoxes = [];

    public SettingsPage()
    {
        InitializeComponent();
        ApplyStaticStrings();
        LoadSettingsIntoUi();
        PopulateMonitorList();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _hostWindow = e.Parameter as Window;
    }

    // Sets all localizable strings that don't change after startup.
    private void ApplyStaticStrings()
    {
        PageHeaderText.Text = Loc.Get("SettingsWindowTitle");
        ScaffoldNoticeBar.Message = Loc.Get("SettingsScaffoldNotice");

        GeneralHeaderText.Text = Loc.Get("SettingsGeneralHeader");

        HotkeyLabelText.Text = Loc.Get("SettingsHotkeyLabel");
        HotkeyHintText.Text  = Loc.Get("SettingsHotkeyHint");
        RecordHotkeyButton.Content = Loc.Get("SettingsHotkeyRecordButton");
        ResetHotkeyButton.Content  = Loc.Get("SettingsHotkeyResetButton");

        SoundLabelText.Text = Loc.Get("SettingsSoundLabel");

        ShowWeekendLabelText.Text = Loc.Get("SettingsShowWeekendLabel");

        LanguageLabelText.Text = Loc.Get("SettingsLanguageLabel");
        LangSystemItem.Content = Loc.Get("SettingsLanguageSystem");
        LangDeItem.Content     = Loc.Get("SettingsLanguageGerman");
        LangEnItem.Content     = Loc.Get("SettingsLanguageEnglish");

        ThemeLabelText.Text     = Loc.Get("SettingsThemeLabel");
        ThemeSystemItem.Content = Loc.Get("SettingsThemeSystem");
        ThemeLightItem.Content  = Loc.Get("SettingsThemeLight");
        ThemeDarkItem.Content   = Loc.Get("SettingsThemeDark");

        FolderLabelText.Text       = Loc.Get("SettingsFolderLabel");
        BrowseFolderButton.Content = Loc.Get("SettingsBrowseButton");

        MonitorsHeaderText.Text = Loc.Get("SettingsMonitorsHeader");
        MonitorsHintText.Text   = Loc.Get("SettingsMonitorsHint");

        SaveButton.Content = Loc.Get("SettingsSaveButton");

        LanguageRestartInfoBar.Message = Loc.Get("SettingsLanguageRestartNotice");
        RestartNowButton.Content = Loc.Get("SettingsRestartNowButton");

        DangerZoneHeaderText.Text = Loc.Get("SettingsDangerZoneHeader");
        DangerZoneHintText.Text   = Loc.Get("SettingsDangerZoneHint");
        DeleteAllButton.Content   = Loc.Get("SettingsDeleteAllButton");
    }

    private void LoadSettingsIntoUi()
    {
        var s = SettingsService.Current;
        _pendingHotkeyModifiers = s.HotkeyModifiers;
        _pendingHotkeyVirtualKey = s.HotkeyVirtualKey;
        HotkeyDisplayTextBox.Text = HotkeyFormat.Format(_pendingHotkeyModifiers, _pendingHotkeyVirtualKey);
        SoundToggle.IsOn = s.SoundEnabled;
        ShowWeekendToggle.IsOn = s.ShowWeekend;
        FolderPathTextBox.Text = SettingsService.EffectiveScreenshotFolder;
        SelectComboBoxByTag(LanguageComboBox, s.Language);
        SelectComboBoxByTag(ThemeComboBox, s.Theme);
    }

    // ── Monitor selection ────────────────────────────────────────────────

    // Re-enumerates connected monitors and rebuilds the checkbox list. The
    // settings window is recreated from scratch each time it's reopened (it
    // doesn't minimize to tray like MainWindow), so this naturally re-runs
    // whenever the user opens Settings again — covering monitor configuration
    // changes made while it was closed.
    private void PopulateMonitorList()
    {
        MonitorListPanel.Children.Clear();
        _monitorCheckBoxes.Clear();

        var monitors = MonitorService.GetMonitors();
        var selectedIds = SettingsService.Current.SelectedMonitorDeviceIds;
        bool notConfiguredYet = selectedIds.Count == 0;

        for (int i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            string label = string.Format(Loc.Get("SettingsMonitorLabel"), i + 1, monitor.Width, monitor.Height);
            if (monitor.IsPrimary)
                label += $" {Loc.Get("SettingsMonitorPrimarySuffix")}";

            var checkBox = new CheckBox
            {
                Content = label,
                Tag = monitor.DeviceId,
                IsChecked = notConfiguredYet ? monitor.IsPrimary : selectedIds.Contains(monitor.DeviceId)
            };

            MonitorListPanel.Children.Add(checkBox);
            _monitorCheckBoxes.Add(checkBox);
        }
    }

    // ── Hotkey recorder ──────────────────────────────────────────────────

    private void RecordHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingHotkey)
        {
            StopHotkeyRecording();
            return;
        }

        _isRecordingHotkey = true;
        _capturedHotkeyModifiers = 0;
        HotkeyStatusText.Visibility = Visibility.Collapsed;
        SaveButton.IsEnabled = false;
        RecordHotkeyButton.Content = Loc.Get("SettingsHotkeyRecordingPrompt");
        HotkeyDisplayTextBox.Text = Loc.Get("SettingsHotkeyRecordingPrompt");
        RecordHotkeyButton.Focus(FocusState.Programmatic);
    }

    private void ResetHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingHotkey)
            StopHotkeyRecording();

        _pendingHotkeyModifiers = AppSettings.DefaultHotkeyModifiers;
        _pendingHotkeyVirtualKey = AppSettings.DefaultHotkeyVirtualKey;
        HotkeyDisplayTextBox.Text = HotkeyFormat.Format(_pendingHotkeyModifiers, _pendingHotkeyVirtualKey);
        HotkeyStatusText.Visibility = Visibility.Collapsed;
    }

    private void SettingsPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isRecordingHotkey)
            return;

        e.Handled = true;

        switch (e.Key)
        {
            case VirtualKey.Control:
            case VirtualKey.LeftControl:
            case VirtualKey.RightControl:
                _capturedHotkeyModifiers |= NativeMethods.MOD_CONTROL;
                return;
            case VirtualKey.Menu:
            case VirtualKey.LeftMenu:
            case VirtualKey.RightMenu:
                _capturedHotkeyModifiers |= NativeMethods.MOD_ALT;
                return;
            case VirtualKey.Shift:
            case VirtualKey.LeftShift:
            case VirtualKey.RightShift:
                _capturedHotkeyModifiers |= NativeMethods.MOD_SHIFT;
                return;
            case VirtualKey.LeftWindows:
            case VirtualKey.RightWindows:
                _capturedHotkeyModifiers |= NativeMethods.MOD_WIN;
                return;
            case VirtualKey.Escape:
                StopHotkeyRecording();
                return;
        }

        // Any other key finalizes the combo, but only together with a modifier —
        // a bare letter/function key would fire on every keystroke system-wide.
        if (_capturedHotkeyModifiers == 0)
        {
            StopHotkeyRecording();
            ShowHotkeyStatus(Loc.Get("SettingsHotkeyNeedsModifierError"));
            return;
        }

        _pendingHotkeyModifiers = _capturedHotkeyModifiers;
        _pendingHotkeyVirtualKey = (uint)e.Key;
        StopHotkeyRecording(); // also refreshes the display text from the new pending values
    }

    private void StopHotkeyRecording()
    {
        _isRecordingHotkey = false;
        SaveButton.IsEnabled = true;
        RecordHotkeyButton.Content = Loc.Get("SettingsHotkeyRecordButton");
        HotkeyDisplayTextBox.Text = HotkeyFormat.Format(_pendingHotkeyModifiers, _pendingHotkeyVirtualKey);
    }

    private void ShowHotkeyStatus(string message)
    {
        HotkeyStatusText.Text = message;
        HotkeyStatusText.Visibility = Visibility.Visible;
    }

    private static void SelectComboBoxByTag(ComboBox box, string tag)
    {
        foreach (var obj in box.Items)
        {
            if (obj is ComboBoxItem item && (item.Tag as string) == tag)
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }

    // ── Save ──────────────────────────────────────────────────────────────

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedMonitorIds = _monitorCheckBoxes
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (string)cb.Tag)
            .ToList();

        if (selectedMonitorIds.Count == 0)
        {
            MonitorsStatusText.Text = Loc.Get("SettingsMonitorsNoneSelectedError");
            MonitorsStatusText.Visibility = Visibility.Visible;
            return;
        }
        MonitorsStatusText.Visibility = Visibility.Collapsed;

        string newFolder = string.IsNullOrWhiteSpace(FolderPathTextBox.Text)
            ? AppSettings.DefaultScreenshotFolder
            : FolderPathTextBox.Text;
        string previousFolder = SettingsService.EffectiveScreenshotFolder;
        bool folderChanged = !FolderPathsEqual(newFolder, previousFolder);
        bool moved = false;

        string previousLanguage = SettingsService.Current.Language;
        bool previousShowWeekend = SettingsService.Current.ShowWeekend;

        bool hotkeyChanged = _pendingHotkeyModifiers != SettingsService.Current.HotkeyModifiers
            || _pendingHotkeyVirtualKey != SettingsService.Current.HotkeyVirtualKey;

        // Validated first, before the folder move below — if the hotkey is
        // unusable, nothing else should be touched either.
        if (hotkeyChanged && !HotkeyService.IsAvailable(_pendingHotkeyModifiers, _pendingHotkeyVirtualKey, out int hotkeyError))
        {
            string message = hotkeyError == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
                ? Loc.Get("SettingsHotkeyAlreadyUsedError")
                : string.Format(Loc.Get("SettingsHotkeyRegisterError"), hotkeyError);
            await ShowMessageDialog(Loc.Get("SettingsHotkeyInvalidTitle"), message);
            return;
        }

        if (folderChanged)
        {
            if (!TryValidateFolder(newFolder, out string validationError))
            {
                await ShowMessageDialog(
                    Loc.Get("SettingsFolderInvalidTitle"),
                    string.Format(Loc.Get("SettingsFolderInvalidBody"), validationError));
                return;
            }

            var moveDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = Loc.Get("SettingsFolderMoveDialogTitle"),
                Content = string.Format(Loc.Get("SettingsFolderMoveDialogBody"), previousFolder, newFolder),
                PrimaryButtonText = Loc.Get("SettingsFolderMoveDialogMovePrimary"),
                SecondaryButtonText = Loc.Get("SettingsFolderMoveDialogKeepSecondary"),
                CloseButtonText = Loc.Get("SettingsFolderMoveDialogCancel"),
                DefaultButton = ContentDialogButton.Close
            };

            var moveResult = await moveDialog.ShowAsync();
            if (moveResult == ContentDialogResult.None)
                return; // user cancelled — abort the whole save, nothing is persisted

            if (moveResult == ContentDialogResult.Primary)
            {
                try
                {
                    MoveDirectoryContents(previousFolder, newFolder);
                    moved = true;
                }
                catch (Exception ex)
                {
                    await ShowMessageDialog(
                        Loc.Get("SettingsFolderMoveErrorTitle"),
                        string.Format(Loc.Get("SettingsFolderMoveErrorBody"), ex.Message));
                    return;
                }
            }
        }

        var settings = new AppSettings
        {
            HotkeyModifiers = _pendingHotkeyModifiers,
            HotkeyVirtualKey = _pendingHotkeyVirtualKey,
            SoundEnabled = SoundToggle.IsOn,
            ShowWeekend = ShowWeekendToggle.IsOn,
            SelectedMonitorDeviceIds = selectedMonitorIds,
            Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "System",
            ScreenshotFolder = newFolder,
            Theme = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "System"
        };

        SettingsService.Save(settings);

        if (App.Current is App app)
        {
            app.ApplyTheme(settings.Theme);
            if (folderChanged)
                app.NotifyScreenshotFolderChanged();
            if (hotkeyChanged)
                app.ReregisterHotkey(settings.HotkeyModifiers, settings.HotkeyVirtualKey);
            if (settings.ShowWeekend != previousShowWeekend)
                app.NotifyShowWeekendChanged();
        }

        SavedStatusText.Text = moved
            ? Loc.Get("SettingsSavedAndMovedMessage")
            : Loc.Get("SettingsSavedMessage");
        SavedStatusText.Visibility = Visibility.Visible;

        // .resw resources are only re-resolved at process start, so a language
        // change needs a restart to actually show up in the UI.
        LanguageRestartInfoBar.IsOpen = !string.Equals(previousLanguage, settings.Language, StringComparison.Ordinal);
    }

    private void RestartNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.Current is App app)
            app.RestartApp();
    }

    private async Task ShowMessageDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = Loc.Get("DialogOkButton")
        };
        await dialog.ShowAsync();
    }

    private static bool FolderPathsEqual(string a, string b)
    {
        try
        {
            string fullA = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullB = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(fullA, fullB, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Confirms the folder exists and is actually writable — a folder picked via
    // FolderPicker always exists, but TimeSnap may still lack write permission
    // (e.g. a protected system folder).
    private static bool TryValidateFolder(string folder, out string errorDetail)
    {
        errorDetail = string.Empty;
        try
        {
            Directory.CreateDirectory(folder);
            string probeFile = Path.Combine(folder, $".timesnap-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, string.Empty);
            File.Delete(probeFile);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException or ArgumentException)
        {
            errorDetail = ex.Message;
            return false;
        }
    }

    // Merges the contents of sourceDir into destDir (recursing into same-named
    // subfolders rather than failing), then removes sourceDir once emptied.
    private static void MoveDirectoryContents(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir))
            return;

        Directory.CreateDirectory(destDir);

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            if (Directory.Exists(destSubDir))
                MoveDirectoryContents(dir, destSubDir);
            else
                Directory.Move(dir, destSubDir);
        }

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            if (File.Exists(destFile))
                File.Delete(destFile);
            File.Move(file, destFile);
        }

        if (Directory.GetFileSystemEntries(sourceDir).Length == 0)
            Directory.Delete(sourceDir);
    }

    // ── Screenshot folder picker ─────────────────────────────────────────

    private async void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();

        if (_hostWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(_hostWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            FolderPathTextBox.Text = folder.Path;
    }

    // ── Delete all screenshots ───────────────────────────────────────────

    private async void DeleteAllButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Loc.Get("SettingsDeleteConfirmTitle"),
            Content = Loc.Get("SettingsDeleteConfirmBody"),
            PrimaryButtonText = Loc.Get("SettingsDeleteConfirmPrimary"),
            CloseButtonText = Loc.Get("SettingsDeleteConfirmCancel"),
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        int deletedCount = DeleteAllScreenshots(SettingsService.EffectiveScreenshotFolder);

        DeleteStatusText.Text = deletedCount > 0
            ? Loc.Get("SettingsDeleteSuccessMessage")
            : Loc.Get("SettingsDeleteEmptyMessage");
        DeleteStatusText.Visibility = Visibility.Visible;

        if (App.Current is App app)
            app.NotifyScreenshotsCleared();
    }

    private static int DeleteAllScreenshots(string folder)
    {
        if (!Directory.Exists(folder))
            return 0;

        int count = 0;

        foreach (var weekDir in Directory.GetDirectories(folder))
        {
            count += Directory.GetFiles(weekDir, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(weekDir, recursive: true);
        }

        foreach (var file in Directory.GetFiles(folder))
        {
            File.Delete(file);
            count++;
        }

        return count;
    }
}
