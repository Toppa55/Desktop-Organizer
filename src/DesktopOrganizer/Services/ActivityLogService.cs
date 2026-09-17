using System.Text.Json;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

public sealed class ActivityLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _directory;
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ActivityLogService()
    {
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopOrganizer");
        _path = Path.Combine(_directory, "activity.json");
    }

    public async Task<List<ActivityItem>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_path))
                return new List<ActivityItem>();

            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<List<ActivityItem>>(stream, JsonOptions)
                   ?? new List<ActivityItem>();
        }
        catch
        {
            return new List<ActivityItem>();
        }
    }

    public async Task SaveAsync(IEnumerable<ActivityItem> items)
    {
        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(_directory);
            var snapshot = items
                .OrderByDescending(x => x.Timestamp)
                .Take(250)
                .ToList();

            await using var stream = File.Create(_path);
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions);
        }
        finally
        {
            _gate.Release();
        }
    }
}
