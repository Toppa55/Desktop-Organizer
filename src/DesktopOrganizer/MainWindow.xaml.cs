using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;

namespace DesktopOrganizer;

public partial class MainWindow : Window
{
    private readonly SecureSettingsService _settingsService = new();
    private readonly ActivityLogService _activityLog = new();
    private readonly LocalFileClassifier _localClassifier = new();
    private readonly OpenAiClassifier _aiClassifier = new();

    private AppSettings? _settings;
    private FileOrganizerService? _organizer;
    private bool _initializing = true;
    private bool _busy;

    public ObservableCollection<ActivityItem> Activities { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        UndoButton.IsEnabled = false;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_settings is not null)
            return;

        _settings = await _settingsService.LoadAsync();
        var history = await _activityLog.LoadAsync();

        foreach (var item in history.OrderByDescending(x => x.Timestamp))
            Activities.Add(item);

        AutoOrganizeToggle.IsChecked = _settings.AutoOrganizeEnabled;
        AiToggle.IsChecked = _settings.AiEnabled;
        ApiKeyBox.Password = _settingsService.UnprotectApiKey(_settings.EncryptedApiKey) ?? string.Empty;

        _organizer = new FileOrganizerService(
            _settings,
            _localClassifier,
            _aiClassifier,
            _settingsService);

        _organizer.ActivityAdded += Organizer_ActivityAdded;
        _organizer.StatusChanged += Organizer_StatusChanged;
        _organizer.ApplySettings(_settings);

        _initializing = false;
        UpdateActivitySummary();

        SetStatus(_settings.AutoOrganizeEnabled
            ? "Watching your Desktop"
            : "Ready when you are");
    }

    private async void OrganizeNow_Click(object sender, RoutedEventArgs e)
    {
        if (_organizer is null || _busy)
            return;

        _busy = true;
        OrganizeNowButton.IsEnabled = false;

        try
        {
            await _organizer.OrganizeExistingAsync();
        }
        finally
        {
            OrganizeNowButton.IsEnabled = true;
            _busy = false;
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null)
            return;

        _settings.AutoOrganizeEnabled = AutoOrganizeToggle.IsChecked == true;
        _settings.AiEnabled = AiToggle.IsChecked == true;
        _settings.EncryptedApiKey = _settingsService.ProtectApiKey(ApiKeyBox.Password);

        await _settingsService.SaveAsync(_settings);
        _organizer?.ApplySettings(_settings);

        SetStatus(_settings.AiEnabled && string.IsNullOrWhiteSpace(ApiKeyBox.Password)
            ? "AI is on — add an API key to use it"
            : "Settings saved");
    }

    private async void SettingsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || _settings is null)
            return;

        _settings.AutoOrganizeEnabled = AutoOrganizeToggle.IsChecked == true;
        _settings.AiEnabled = AiToggle.IsChecked == true;
        await _settingsService.SaveAsync(_settings);
        _organizer?.ApplySettings(_settings);

        if (_settings.AutoOrganizeEnabled)
            SetStatus("Watching your Desktop");
        else
            SetStatus("Automatic organizing is paused");
    }

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_organizer is null || ActivityList.SelectedItem is not ActivityItem item)
            return;

        UndoButton.IsEnabled = false;
        var restored = await _organizer.UndoAsync(item);

        if (restored)
        {
            ActivityList.Items.Refresh();
            await _activityLog.SaveAsync(Activities.ToList());
            UpdateActivitySummary();
        }

        UpdateUndoButton();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null)
            return;

        var path = string.IsNullOrWhiteSpace(_settings.DestinationRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Organized")
            : _settings.DestinationRoot;

        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void ActivityList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateUndoButton();
    }

    private void Organizer_ActivityAdded(ActivityItem item)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            Activities.Insert(0, item);
            while (Activities.Count > 250)
                Activities.RemoveAt(Activities.Count - 1);

            await _activityLog.SaveAsync(Activities.ToList());
            UpdateActivitySummary();
        });
    }

    private void Organizer_StatusChanged(string status)
    {
        Dispatcher.BeginInvoke(() => SetStatus(status));
    }

    private void SetStatus(string status)
    {
        MainStatusText.Text = status;
        HeaderStatusText.Text = _settings?.AutoOrganizeEnabled == true ? "Active" : "Ready";
    }

    private void UpdateActivitySummary()
    {
        var activeMoves = Activities.Count(x => !x.Undone);
        ActivitySummaryText.Text = activeMoves == 0
            ? "Nothing organized yet."
            : $"{activeMoves} file{(activeMoves == 1 ? string.Empty : "s")} currently organized.";
    }

    private void UpdateUndoButton()
    {
        UndoButton.IsEnabled = ActivityList.SelectedItem is ActivityItem item && !item.Undone;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App app && app.IsExiting)
        {
            _organizer?.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
