using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

public sealed class FileOrganizerService : IDisposable
{
    private readonly LocalFileClassifier _localClassifier;
    private readonly OpenAiClassifier _aiClassifier;
    private readonly SecureSettingsService _settingsService;
    private readonly SemaphoreSlim _organizeGate = new(1, 1);
    private readonly string _desktopPath;

    private AppSettings _settings;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public event Action<ActivityItem>? ActivityAdded;
    public event Action<string>? StatusChanged;

    public FileOrganizerService(
        AppSettings settings,
        LocalFileClassifier localClassifier,
        OpenAiClassifier aiClassifier,
        SecureSettingsService settingsService)
    {
        _settings = settings;
        _localClassifier = localClassifier;
        _aiClassifier = aiClassifier;
        _settingsService = settingsService;
        _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    public string DesktopPath => _desktopPath;

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;

        if (_settings.AutoOrganizeEnabled)
            StartMonitoring();
        else
            StopMonitoring();
    }

    public void StartMonitoring()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = true;
            StatusChanged?.Invoke("Watching your Desktop");
            return;
        }

        _watcher = new FileSystemWatcher(_desktopPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileAppeared;
        _watcher.Renamed += OnFileRenamed;
        StatusChanged?.Invoke("Watching your Desktop");
    }

    public void StopMonitoring()
    {
        if (_watcher is not null)
            _watcher.EnableRaisingEvents = false;

        StatusChanged?.Invoke("Automatic organizing is paused");
    }

    public async Task<int> OrganizeExistingAsync(CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(_desktopPath, "*", SearchOption.TopDirectoryOnly).ToList();
        var moved = 0;

        StatusChanged?.Invoke(files.Count == 0 ? "Your Desktop is already clean" : "Organizing your Desktop…");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await OrganizeFileAsync(file, cancellationToken);
            if (result is not null)
                moved++;
        }

        StatusChanged?.Invoke(moved == 0
            ? (_settings.AutoOrganizeEnabled ? "Watching your Desktop" : "Nothing needed organizing")
            : $"Organized {moved} file{(moved == 1 ? string.Empty : "s")}");

        return moved;
    }

    public async Task<bool> UndoAsync(ActivityItem item, CancellationToken cancellationToken = default)
    {
        if (item.Undone || !File.Exists(item.DestinationPath))
            return false;

        await _organizeGate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(item.DestinationPath))
                return false;

            var sourceDirectory = Path.GetDirectoryName(item.SourcePath);
            if (!string.IsNullOrWhiteSpace(sourceDirectory))
                Directory.CreateDirectory(sourceDirectory);

            var restorePath = GetUniquePath(item.SourcePath);
            File.Move(item.DestinationPath, restorePath);
            item.Undone = true;
            StatusChanged?.Invoke("Last move undone");
            return true;
        }
        catch
        {
            StatusChanged?.Invoke("Could not undo that move");
            return false;
        }
        finally
        {
            _organizeGate.Release();
        }
    }

    private void OnFileAppeared(object sender, FileSystemEventArgs e)
    {
        _ = OrganizeAfterWriteCompletesAsync(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _ = OrganizeAfterWriteCompletesAsync(e.FullPath);
    }

    private async Task OrganizeAfterWriteCompletesAsync(string path)
    {
        try
        {
            await Task.Delay(1200);
            await OrganizeFileAsync(path);
        }
        catch
        {
            // Background file events are deliberately non-fatal.
        }
    }

    private async Task<ActivityItem?> OrganizeFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(path) || !File.Exists(path) || _localClassifier.ShouldIgnore(path))
            return null;

        await _organizeGate.WaitAsync(cancellationToken);
        try
        {
            if (!await WaitUntilAvailableAsync(path, cancellationToken))
                return null;

            var category = _localClassifier.Classify(path);
            var method = "Local";

            if (_settings.AiEnabled && _localClassifier.ShouldUseAi(path))
            {
                var apiKey = _settingsService.UnprotectApiKey(_settings.EncryptedApiKey);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    var aiCategory = await _aiClassifier.ClassifyAsync(
                        path,
                        apiKey,
                        _settings.OpenAiModel,
                        cancellationToken);

                    if (!string.IsNullOrWhiteSpace(aiCategory))
                    {
                        category = aiCategory;
                        method = "AI";
                    }
                }
            }

            var root = string.IsNullOrWhiteSpace(_settings.DestinationRoot)
                ? Path.Combine(_desktopPath, "Organized")
                : _settings.DestinationRoot;

            var categoryFolder = Path.Combine(root, SanitizeFolderName(category));
            Directory.CreateDirectory(categoryFolder);

            var destination = GetUniquePath(Path.Combine(categoryFolder, Path.GetFileName(path)));
            File.Move(path, destination);

            var activity = new ActivityItem
            {
                SourcePath = path,
                DestinationPath = destination,
                Category = category,
                ClassificationMethod = method,
                Timestamp = DateTimeOffset.Now
            };

            ActivityAdded?.Invoke(activity);
            return activity;
        }
        catch
        {
            return null;
        }
        finally
        {
            _organizeGate.Release();
        }
    }

    private static async Task<bool> WaitUntilAvailableAsync(
        string path,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return stream.Length >= 0;
            }
            catch (IOException)
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }

    private static string GetUniquePath(string desiredPath)
    {
        if (!File.Exists(desiredPath))
            return desiredPath;

        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);

        for (var index = 2; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{fileName} ({index}){extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(directory, $"{fileName} {Guid.NewGuid():N}{extension}");
    }

    private static string SanitizeFolderName(string category)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(category.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "Other" : clean;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _watcher?.Dispose();
        _organizeGate.Dispose();
    }
}
