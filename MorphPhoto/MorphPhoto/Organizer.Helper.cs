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

    /// <summary>
    /// Checks if a file should be skipped based on system file patterns
    /// </summary>
    public static bool ShouldSkipFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        // Check exact matches
        if (SkipFilePatterns.Contains(fileName))
            return true;
            
        // Check wildcard patterns
        foreach (var pattern in SkipFilePatterns.Where(p => p.Contains('*')))
        {
            var patternWithoutWildcard = pattern.Replace("*", "");
            if (fileName.StartsWith(patternWithoutWildcard, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        // Check prefixes
        if (SkipPrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return true;
            
        return false;
    }

    private string GetRelativePath(string basePath, string fullPath)
    {
        var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var fullUri = new Uri(fullPath);
        return baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Improved GetDestPath with better structure and error handling
    /// </summary>
    private string GetDestPath(string originalFilePath, ImageMetadata imageMetadata)
    {
        var fileName = Path.GetFileName(originalFilePath);
        
        // Handle corrupt files based on policy
        if (IsCorruptFile(imageMetadata.Corruptness))
        {
            return organizerOptions.HandleCorruptFiles switch
            {
                EHandleCorruptFiles.Skip => throw new InvalidOperationException($"Corrupt file should be skipped: {fileName}"),
                EHandleCorruptFiles.NormalOrganize => GetNormalOrganizePath(originalFilePath, imageMetadata, fileName),
                EHandleCorruptFiles.ExtensionOrganize => GetCorruptFileOrganizePath(originalFilePath, imageMetadata, fileName),
                _ => throw new ArgumentOutOfRangeException(nameof(organizerOptions.HandleCorruptFiles), organizerOptions.HandleCorruptFiles, "Invalid corrupt file handling option")
            };
        }
        
        // Handle normal (non-corrupt) files
        return GetNormalOrganizePath(originalFilePath, imageMetadata, fileName);
    }

    /// <summary>
    /// Checks if the file is considered corrupt
    /// </summary>
    private static bool IsCorruptFile(CorruptChecker.ImageCorruptness corruptness)
    {
        return corruptness is 
            CorruptChecker.ImageCorruptness.Invalid or 
            CorruptChecker.ImageCorruptness.Truncated or 
            CorruptChecker.ImageCorruptness.Partial;
    }

    /// <summary>
    /// Gets the destination path for corrupt files when ExtensionOrganize policy is used
    /// </summary>
    private string GetCorruptFileOrganizePath(string originalFilePath, ImageMetadata imageMetadata, string fileName)
    {
        var corruptionFolder = GetCorruptionFolder(imageMetadata.Corruptness);
        
        var destPath = organizerOptions.OrganizationType switch
        {
            OrganizationType.ByDate => Path.Combine(destDir, corruptionFolder, fileName),
            OrganizationType.ByExtension => Path.Combine(destDir, GetFormattedExtension(originalFilePath), corruptionFolder, fileName),
            _ => throw new ArgumentOutOfRangeException(nameof(organizerOptions.OrganizationType), organizerOptions.OrganizationType, "Invalid organization type")
        };
        
        return GetUniqueFileName(destPath);
    }

    /// <summary>
    /// Gets the destination path for normal file organization (by date if available, error folder otherwise)
    /// </summary>
    private string GetNormalOrganizePath(string originalFilePath, ImageMetadata imageMetadata, string fileName)
    {
        // Try to organize by date if available
        if (imageMetadata.EarliestDate.HasValue)
        {
            return GetDateBasedPath(originalFilePath, imageMetadata.EarliestDate.Value, fileName);
        }
        
        // Fallback to error folder for files without date metadata
        return GetErrorPath(fileName);
    }

    /// <summary>
    /// Creates a date-based organization path
    /// </summary>
    private string GetDateBasedPath(string originalFilePath, DateTime date, string fileName)
    {
        var yearFolder = date.Year.ToString();
        var monthFolder = date.ToString("MM-MMM");
        
        var destPath = organizerOptions.OrganizationType switch
        {
            OrganizationType.ByDate => Path.Combine(destDir, yearFolder, monthFolder, fileName),
            OrganizationType.ByExtension => Path.Combine(destDir, GetFormattedExtension(originalFilePath), yearFolder, monthFolder, fileName),
            _ => throw new ArgumentOutOfRangeException(nameof(organizerOptions.OrganizationType), organizerOptions.OrganizationType, "Invalid organization type")
        };
        
        return GetUniqueFileName(destPath);
    }

    /// <summary>
    /// Gets the error folder path for files that can't be organized by date
    /// </summary>
    private string GetErrorPath(string fileName)
    {
        var errorPath = Path.Combine(destDir, "error", fileName);
        return GetUniqueFileName(errorPath);
    }

    private string GetCorruptionFolder(CorruptChecker.ImageCorruptness corruptness)
    {
        return corruptness switch
        {
            CorruptChecker.ImageCorruptness.Partial => "PartialCorrupt",
            CorruptChecker.ImageCorruptness.Truncated => "Truncated",
            CorruptChecker.ImageCorruptness.Invalid => "InvalidDecoder",
            CorruptChecker.ImageCorruptness.None => throw new ArgumentException("Cannot get corruption folder for non-corrupt file", nameof(corruptness)),
            _ => throw new ArgumentOutOfRangeException(nameof(corruptness), corruptness, "Unknown corruption type")
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

    /// <summary>
    /// Validates if a file should be processed (combines corruption policy and system file checks)
    /// </summary>
    public bool ShouldProcessFile(string filePath, ImageMetadata imageMetadata)
    {
        // Skip system/metadata files
        if (ShouldSkipFile(filePath))
        {
            WriteToConsole($"Skipping system file: {Path.GetFileName(filePath)}", ConsoleColor.Yellow);
            return false;
        }

        // Skip corrupt files if policy is set to skip
        if (IsCorruptFile(imageMetadata.Corruptness) && 
            organizerOptions.HandleCorruptFiles == EHandleCorruptFiles.Skip)
        {
            WriteToConsole($"Skipping corrupt file: {Path.GetFileName(filePath)} ({imageMetadata.Corruptness})", ConsoleColor.Red);
            return false;
        }

        return true;
    }
}