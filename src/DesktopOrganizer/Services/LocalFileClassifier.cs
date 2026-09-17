namespace DesktopOrganizer.Services;

public sealed class LocalFileClassifier
{
    private static readonly Dictionary<string, string> ExtensionCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "Images", [".jpg"] = "Images", [".jpeg"] = "Images", [".gif"] = "Images",
        [".webp"] = "Images", [".bmp"] = "Images", [".tif"] = "Images", [".tiff"] = "Images", [".heic"] = "Images",

        [".mp3"] = "Audio", [".wav"] = "Audio", [".flac"] = "Audio", [".m4a"] = "Audio", [".aac"] = "Audio",
        [".mp4"] = "Video", [".mov"] = "Video", [".avi"] = "Video", [".mkv"] = "Video", [".webm"] = "Video",

        [".zip"] = "Archives", [".rar"] = "Archives", [".7z"] = "Archives", [".tar"] = "Archives", [".gz"] = "Archives",
        [".exe"] = "Installers", [".msi"] = "Installers", [".msix"] = "Installers", [".appx"] = "Installers",

        [".stl"] = "3D Printing", [".3mf"] = "3D Printing", [".obj"] = "3D Printing", [".step"] = "3D Printing",
        [".stp"] = "3D Printing", [".gcode"] = "3D Printing", [".iges"] = "3D Printing", [".igs"] = "3D Printing",

        [".py"] = "Code", [".cs"] = "Code", [".js"] = "Code", [".ts"] = "Code", [".html"] = "Code",
        [".css"] = "Code", [".json"] = "Code", [".xml"] = "Code", [".yaml"] = "Code", [".yml"] = "Code",
        [".sql"] = "Code", [".ps1"] = "Code", [".bat"] = "Code", [".sh"] = "Code",

        [".pdf"] = "Documents", [".doc"] = "Documents", [".docx"] = "Documents", [".rtf"] = "Documents",
        [".txt"] = "Documents", [".md"] = "Documents", [".odt"] = "Documents",
        [".xls"] = "Spreadsheets", [".xlsx"] = "Spreadsheets", [".csv"] = "Spreadsheets", [".ods"] = "Spreadsheets",
        [".ppt"] = "Presentations", [".pptx"] = "Presentations", [".odp"] = "Presentations"
    };

    private static readonly HashSet<string> AiFriendlyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".rtf", ".txt", ".md", ".odt",
        ".xls", ".xlsx", ".csv", ".ods", ".ppt", ".pptx", ".odp"
    };

    public string Classify(string path)
    {
        var extension = Path.GetExtension(path);
        return ExtensionCategories.TryGetValue(extension, out var category)
            ? category
            : "Other";
    }

    public bool ShouldUseAi(string path)
    {
        var extension = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(extension) || AiFriendlyExtensions.Contains(extension);
    }

    public bool ShouldIgnore(string path)
    {
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".temp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System);
        }
        catch
        {
            return true;
        }
    }
}
