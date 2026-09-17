namespace DesktopOrganizer.Models;

public sealed class AppSettings
{
    public bool AutoOrganizeEnabled { get; set; }
    public bool AiEnabled { get; set; }
    public string? EncryptedApiKey { get; set; }
    public string OpenAiModel { get; set; } = "gpt-5.6-luna";
    public string? DestinationRoot { get; set; }
}
