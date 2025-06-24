using System.Security.Cryptography;
using HeyRed.ImageSharp.Heif.Formats.Avif;
using HeyRed.ImageSharp.Heif.Formats.Heif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace MorphPhoto.MorphPhoto;

public partial class Organizer
{
    private static DecoderOptions DecoderHeifOptions = new DecoderOptions()
    {
        Configuration = new Configuration(new HeifConfigurationModule(), new AvifConfigurationModule())
    };
    private static readonly HashSet<string> SkipFilePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        // macOS metadata files
        "._*",
        ".DS_Store",
        ".localized",
        
        // Windows system files
        "Thumbs.db",
        "desktop.ini",
        "ehthumbs.db",
        
        // Other common system files
        ".fseventsd",
        ".Spotlight-V100",
        ".TemporaryItems",
        ".Trashes",
        ".VolumeIcon.icns",
        ".AppleDouble",
        ".LSOverride"
    };
    private static readonly string[] SkipPrefixes = { "._", ".tmp", "~$" };

    private static void WriteToConsole(string message, ConsoleColor color = ConsoleColor.White)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        Console.ForegroundColor = originalColor;
    }

    private string GetRelativePath(string basePath, string fullPath)
    {
        var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var fullUri = new Uri(fullPath);
        return baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar);
    }

    private string GetDestPath(string originalFilePath, ImageMetadata imageMetadata)
    {
        var fileName = Path.GetFileName(originalFilePath);
        switch (imageMetadata.Corruptness)
        {
            case CorruptChecker.ImageCorruptness.Partial:
            case CorruptChecker.ImageCorruptness.Truncated:
            case CorruptChecker.ImageCorruptness.Invalid:
            {
                var corruptionFolder = GetCorruptionFolder(imageMetadata.Corruptness);

                var destPath = organizerOptions.OrganizationType switch
                {
                    OrganizationType.ByDate => Path.Combine(destDir, corruptionFolder, fileName),
                    OrganizationType.ByExtension => Path.Combine(destDir, GetFormattedExtension(originalFilePath),
                        corruptionFolder,
                        fileName),
                    _ => throw new ArgumentOutOfRangeException()
                };

                return GetUniqueFileName(destPath);
            }
            case CorruptChecker.ImageCorruptness.None:
            {
                if (imageMetadata.EarliestDate.HasValue)
                {
                    var yearFolder = imageMetadata.EarliestDate.Value.Year.ToString();

                    var monthFolder = imageMetadata.EarliestDate.Value.ToString("MM-MMM");

                    var destPath = organizerOptions.OrganizationType switch
                    {
                        OrganizationType.ByDate => Path.Combine(destDir, GetFormattedExtension(originalFilePath),
                            yearFolder, monthFolder, fileName),
                        OrganizationType.ByExtension => Path.Combine(destDir, GetFormattedExtension(originalFilePath),
                            yearFolder, monthFolder,
                            fileName),
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    return GetUniqueFileName(destPath);
                }

                var error = Path.Combine(destDir, "error", fileName);
                return GetUniqueFileName(error);
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    private string GetCorruptionFolder(CorruptChecker.ImageCorruptness corruptness)
    {
        return corruptness switch
        {
            CorruptChecker.ImageCorruptness.Partial => "PartialCorrupt",
            CorruptChecker.ImageCorruptness.Truncated => "Truncated",
            CorruptChecker.ImageCorruptness.Invalid => "InvalidDecoder",
            _ => "Unknown"
        };
    }

    private string GetFormattedExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath);

        return ext.TrimStart('.').ToLower();
    }

    private string GetUniqueFileName(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        var counter = 1;
        var newPath = filePath;

        while (File.Exists(newPath))
        {
            newPath = Path.Combine(directory, $"{fileName}_{counter:D3}{extension}");
            counter++;
        }

        return newPath;
    }

    private string CalculateFileHash(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToBase64String(hashBytes);
    }
}