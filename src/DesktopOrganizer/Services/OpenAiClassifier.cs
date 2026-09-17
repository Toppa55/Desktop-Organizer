using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DesktopOrganizer.Services;

public sealed class OpenAiClassifier
{
    private static readonly string[] AllowedCategories =
    [
        "Work", "Finance", "Personal", "Projects", "Reference",
        "Documents", "Spreadsheets", "Presentations", "Other"
    ];

    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("https://api.openai.com/")
    };

    public async Task<string?> ClassifyAsync(
        string path,
        string apiKey,
        string model,
        CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(path);
        var snippet = await TryReadTextSnippetAsync(path, cancellationToken);

        var prompt = $"""
You are the classification component of a Windows desktop organizer.
Choose exactly one category from this list:
{string.Join(", ", AllowedCategories)}

Classify the file from the limited metadata below. Return only the category name and nothing else.

Filename: {info.Name}
Extension: {info.Extension}
Size bytes: {info.Length}
Last modified: {info.LastWriteTimeUtc:O}
Text snippet: {(string.IsNullOrWhiteSpace(snippet) ? "(not available)" : snippet)}
""";

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model,
            input = prompt,
            max_output_tokens = 40
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var text = ExtractOutputText(document.RootElement);

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.Trim().Trim('"', '\'', '.', '`', ' ');
        return AllowedCategories.FirstOrDefault(category =>
            category.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var directText) &&
            directText.ValueKind == JsonValueKind.String)
        {
            return directText.GetString();
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    return text.GetString();
            }
        }

        return null;
    }

    private static async Task<string?> TryReadTextSnippetAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(path);
        var safeTextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".rtf"
        };

        if (!safeTextExtensions.Contains(extension))
            return null;

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);
            using var reader = new StreamReader(stream);
            var buffer = new char[1200];
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            return new string(buffer, 0, read).ReplaceLineEndings(" ");
        }
        catch
        {
            return null;
        }
    }
}
