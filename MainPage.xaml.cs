using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TimeSnap.Services;

namespace TimeSnap;

public sealed partial class MainPage : Page
{
    private DateTime _currentWeekMonday;

    // Folder names on disk are always German — they are storage identifiers, not UI text.
    // Saturday/Sunday are always included here: screenshots taken on a weekend are
    // still saved into these folders regardless of the "show weekend" setting, which
    // only controls whether the matching UI columns are visible.
    private static readonly string[] DayFolders =
        ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"];

    private ListView[]  _lists         = null!;
    private TextBlock[] _headers       = null!;
    private Border[]    _headerBorders = null!;
    private TextBlock[] _emptyTexts    = null!;

    private List<ScreenshotItem>? _previewItems;
    private int _previewIndex;

    public MainPage()
    {
        InitializeComponent();

        _lists         = [MondayList,        TuesdayList,         WednesdayList,         ThursdayList,         FridayList,        SaturdayList,        SundayList];
        _headers       = [MondayHeader,        TuesdayHeader,       WednesdayHeader,       ThursdayHeader,       FridayHeader,        SaturdayHeader,       SundayHeader];
        _headerBorders = [MondayHeaderBorder,  TuesdayHeaderBorder, WednesdayHeaderBorder, ThursdayHeaderBorder, FridayHeaderBorder,  SaturdayHeaderBorder, SundayHeaderBorder];
        _emptyTexts    = [MondayEmpty,         TuesdayEmpty,        WednesdayEmpty,        ThursdayEmpty,        FridayEmpty,       SaturdayEmpty,       SundayEmpty];

        ApplyStaticStrings();

        _currentWeekMonday = GetMonday(DateTime.Today);
        LoadWeekData();

        if (App.Current is App app)
        {
            app.ScreenshotSaved += OnScreenshotSaved;
            app.ScreenshotsCleared += OnStorageChanged;
            app.ScreenshotFolderChanged += OnStorageChanged;
            app.ShowWeekendChanged += OnStorageChanged;
        }
    }

    // Sets all localizable strings that don't change after startup.
    private void ApplyStaticStrings()
    {
        string emptyDay = Loc.Get("EmptyDay");
        foreach (var t in _emptyTexts) t.Text = emptyDay;

        GotoCurrentWeekButton.Content = Loc.Get("GotoCurrentWeek");

        ToolTipService.SetToolTip(PrevWeekButton,      Loc.Get("PrevWeekTip"));
        ToolTipService.SetToolTip(NextWeekButton,      Loc.Get("NextWeekTip"));
        ToolTipService.SetToolTip(GotoCurrentWeekButton, Loc.Get("GotoCurrentWeek"));
        ToolTipService.SetToolTip(PreviewPrevButton,   Loc.Get("PrevImageTip"));
        ToolTipService.SetToolTip(PreviewNextButton,   Loc.Get("NextImageTip"));
        ToolTipService.SetToolTip(ClosePreviewButton,  Loc.Get("CloseTip"));
    }

    private void OnScreenshotSaved(string _)
    {
        var today = DateTime.Today;
        if (today >= _currentWeekMonday && today < _currentWeekMonday.AddDays(7))
            LoadWeekData();
    }

    // Screenshots were deleted, the storage folder was changed, or the weekend
    // visibility setting changed, via the settings page — close any open preview
    // (it may reference files that are now gone, moved, or hidden) and reload,
    // regardless of which week is shown.
    private void OnStorageChanged()
    {
        ClosePreview();
        LoadWeekData();
    }

    // ── Week loading ───────────────────────────────────────────────────────

    private void LoadWeekData()
    {
        bool showWeekend = SettingsService.Current.ShowWeekend;
        int dayCount = showWeekend ? 7 : 5;

        var weekendColumnWidth = showWeekend ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SaturdayColumn.Width = weekendColumnWidth;
        SundayColumn.Width   = weekendColumnWidth;
        SaturdayBorder.Visibility = showWeekend ? Visibility.Visible : Visibility.Collapsed;
        SundayBorder.Visibility   = showWeekend ? Visibility.Visible : Visibility.Collapsed;

        UpdateWeekHeader(dayCount);

        int weekNum = ISOWeek.GetWeekOfYear(_currentWeekMonday);
        string weekFolder = $"{_currentWeekMonday.Year}-KW{weekNum:D2}";

        var culture      = CultureInfo.CurrentUICulture;
        string dateFmt   = Loc.Get("ColumnDateFormat"); // "dd.MM." or "M/d"
        string basePath  = SettingsService.EffectiveScreenshotFolder;

        for (int i = 0; i < dayCount; i++)
        {
            var date = _currentWeekMonday.AddDays(i);

            // Display name comes from the OS locale; folder name stays German.
            string dayName  = culture.DateTimeFormat.GetDayName(date.DayOfWeek);
            string dateText = date.ToString(dateFmt, culture);
            _headers[i].Text = $"{dayName}\n{dateText}";

            ApplyHeaderStyle(_headerBorders[i], _headers[i], isToday: date.Date == DateTime.Today);

            var items = LoadDayItems(Path.Combine(basePath, weekFolder, DayFolders[i]));
            _lists[i].ItemsSource = items;

            bool any = items.Count > 0;
            _lists[i].Visibility      = any ? Visibility.Visible   : Visibility.Collapsed;
            _emptyTexts[i].Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static List<ScreenshotItem> LoadDayItems(string folder)
    {
        if (!Directory.Exists(folder))
            return [];

        return [.. Directory.GetFiles(folder, "*.jpg")
            .Order()
            .Select(f => new ScreenshotItem(f))];
    }

    private void UpdateWeekHeader(int dayCount)
    {
        int week    = ISOWeek.GetWeekOfYear(_currentWeekMonday);
        var lastDay = _currentWeekMonday.AddDays(dayCount - 1);
        var culture = CultureInfo.CurrentUICulture;
        var dtf     = culture.DateTimeFormat;

        string startMonth = dtf.GetMonthName(_currentWeekMonday.Month);
        string endMonth   = dtf.GetMonthName(lastDay.Month);

        string range = _currentWeekMonday.Month == lastDay.Month
            ? string.Format(Loc.Get("WeekRangeSameMonth"),
                _currentWeekMonday.Day, lastDay.Day, endMonth, lastDay.Year)
            : string.Format(Loc.Get("WeekRangeDiffMonth"),
                _currentWeekMonday.Day, startMonth, lastDay.Day, endMonth, lastDay.Year);

        WeekHeaderText.Text = $"{Loc.Get("WeekLabel")} {week}  ·  {range}";

        // Show "go to current week" only when not already there.
        // Use Opacity instead of Visibility so the button always reserves its layout space
        // and the header height never changes when navigating weeks.
        bool isCurrentWeek = _currentWeekMonday == GetMonday(DateTime.Today);
        GotoCurrentWeekButton.Opacity          = isCurrentWeek ? 0 : 1;
        GotoCurrentWeekButton.IsHitTestVisible = !isCurrentWeek;
    }

    private static void ApplyHeaderStyle(Border border, TextBlock text, bool isToday)
    {
        if (isToday)
        {
            if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var res) && res is Brush accent)
                border.Background = accent;
            if (Application.Current.Resources.TryGetValue("TextOnAccentFillColorPrimaryBrush", out var tb) && tb is Brush textBrush)
                text.Foreground = textBrush;
            else
                text.Foreground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 255, G = 255, B = 255 });
        }
        else
        {
            border.ClearValue(Border.BackgroundProperty);
            text.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    // ── Week navigation ────────────────────────────────────────────────────

    private void PrevWeek_Click(object sender, RoutedEventArgs e)
    {
        _currentWeekMonday = _currentWeekMonday.AddDays(-7);
        LoadWeekData();
    }

    private void NextWeek_Click(object sender, RoutedEventArgs e)
    {
        _currentWeekMonday = _currentWeekMonday.AddDays(7);
        LoadWeekData();
    }

    private void GotoCurrentWeek_Click(object sender, RoutedEventArgs e)
    {
        _currentWeekMonday = GetMonday(DateTime.Today);
        LoadWeekData();
    }

    // ── Preview overlay ────────────────────────────────────────────────────

    private void Screenshot_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ScreenshotItem item || sender is not ListView list) return;

        int dayIdx = Array.IndexOf(_lists, list);
        if (dayIdx < 0) return;

        if (_lists[dayIdx].ItemsSource is not List<ScreenshotItem> items) return;

        int itemIdx = items.IndexOf(item);
        if (itemIdx < 0) return;

        OpenPreview(items, itemIdx);
    }

    private void OpenPreview(List<ScreenshotItem> items, int index)
    {
        _previewItems = items;
        _previewIndex = index;
        UpdatePreviewContent();
        PreviewOverlay.Visibility = Visibility.Visible;
    }

    private void UpdatePreviewContent()
    {
        if (_previewItems is null) return;
        var item = _previewItems[_previewIndex];

        PreviewImage.Source  = new BitmapImage(new Uri(item.FilePath));
        PreviewTimeText.Text = string.IsNullOrEmpty(item.MonitorLabel)
            ? $"{item.TimeText}{Loc.Get("TimeUhrSuffix")}"
            : $"{item.TimeText}{Loc.Get("TimeUhrSuffix")} · {item.MonitorLabel}";

        bool hasTitle = !string.IsNullOrEmpty(item.WindowTitle);
        PreviewTitleText.Text       = item.WindowTitle;
        PreviewTitleText.Visibility = hasTitle ? Visibility.Visible : Visibility.Collapsed;

        PreviewPrevButton.IsEnabled = _previewIndex > 0;
        PreviewNextButton.IsEnabled = _previewIndex < _previewItems.Count - 1;
    }

    private void NavigatePreview(int delta)
    {
        if (_previewItems is null) return;
        int newIdx = _previewIndex + delta;
        if (newIdx < 0 || newIdx >= _previewItems.Count) return;
        _previewIndex = newIdx;
        UpdatePreviewContent();
    }

    private void PreviewPrev_Click(object sender, RoutedEventArgs e) => NavigatePreview(-1);
    private void PreviewNext_Click(object sender, RoutedEventArgs e) => NavigatePreview(1);

    private void ClosePreview_Click(object sender, RoutedEventArgs e) => ClosePreview();
    private void Scrim_Tapped(object sender, TappedRoutedEventArgs e)  => ClosePreview();

    private void EscapeKey_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (PreviewOverlay.Visibility == Visibility.Visible)
        {
            ClosePreview();
            args.Handled = true;
        }
    }

    private void LeftArrow_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (PreviewOverlay.Visibility == Visibility.Visible)
        {
            NavigatePreview(-1);
            args.Handled = true;
        }
    }

    private void RightArrow_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (PreviewOverlay.Visibility == Visibility.Visible)
        {
            NavigatePreview(1);
            args.Handled = true;
        }
    }

    private void ClosePreview()
    {
        PreviewOverlay.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
        _previewItems = null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static DateTime GetMonday(DateTime date)
    {
        int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff).Date;
    }
}
