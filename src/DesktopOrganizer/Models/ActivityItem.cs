namespace DesktopOrganizer.Models;

public sealed class ActivityItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ClassificationMethod { get; set; } = "Local";
    public bool Undone { get; set; }

    public string FileName => Path.GetFileName(DestinationPath);
    public string DisplayTime => Timestamp.LocalDateTime.ToString("dd MMM, HH:mm");
    public string DisplayMethod => Undone ? "Undone" : ClassificationMethod;
}
