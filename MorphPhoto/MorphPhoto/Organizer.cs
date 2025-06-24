using System.Security.Cryptography;
using System.Drawing;
using System.Drawing.Imaging;
using HeyRed.ImageSharp.Heif.Formats.Avif;
using HeyRed.ImageSharp.Heif.Formats.Heif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace MorphPhoto.MorphPhoto;

public class Organizer(string sourceDir, string destDir)
{
    private readonly string _sourceDir = sourceDir;
    private readonly string _destDir = destDir;
    private readonly Dictionary<string, string> _duplicates = new();

    private readonly HashSet<string> _fileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // ".jpg",
        // ".jpeg",
        // ".png"
        ".heic"
    };

    private static DecoderOptions DecoderHeifOptions = new DecoderOptions()
    {
        Configuration = new Configuration(new HeifConfigurationModule(), new AvifConfigurationModule())
    };

    public void Organize()
    {
        try
        {
            WriteToConsole("Starting organize...");
            WriteToConsole($"Source Dir {_sourceDir}");
            WriteToConsole($"Destination Dir {_destDir}");

            Directory.CreateDirectory(_destDir);


            var images = GetImageFiles(_sourceDir);

            WriteToConsole($"Image count: {images.Count}");


            for (int i = 0; i < images.Count; i++)
            {
                var file = images[i];
                WriteToConsole($"Processing {i + 1}/{images.Count}: {Path.GetFileName(file)}", ConsoleColor.DarkGreen);
                ProcessImageFile(file);
            }
        }
        catch (Exception ex)
        {
            WriteToConsole($"Error during organization: {ex.Message}", ConsoleColor.Red);
            throw;
        }
    }

    private static void WriteToConsole(string message, ConsoleColor color = ConsoleColor.White)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        Console.ForegroundColor = originalColor;
    }

    private List<string> GetImageFiles(string directory)
    {
        var files = new List<string>();

        try
        {
            var allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Where(file => _fileExtensions.Contains(Path.GetExtension(file)))
                .ToList();

            files.AddRange(allFiles);
        }
        catch (Exception ex)
        {
            WriteToConsole($"Error scanning directory {directory}: {ex.Message}", ConsoleColor.Red);
        }

        return files;
    }

    private void ProcessImageFile(string filePath)
    {
        // Get image metadata
        var imageInfo = GetImageInfo(filePath);

        var destPath = GetDestPath(filePath, imageInfo);

        // Ensure destination directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(destPath));

        // Copy file to destination
        File.Copy(filePath, destPath, false);

        // Ensure destination directory exists
        WriteToConsole($"Organized: {Path.GetFileName(filePath)} -> {GetRelativePath(_destDir, destPath)}",
            ConsoleColor.Green);
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
            {
                var destPath = Path.Combine(_destDir, "PartialCorrupt", fileName);

                return GetUniqueFileName(destPath);
            }
            case CorruptChecker.ImageCorruptness.Truncated:
            {
                var destPath = Path.Combine(_destDir, "Truncated", fileName);

                return GetUniqueFileName(destPath);
            }
            case CorruptChecker.ImageCorruptness.Invalid:
            {
                var destPath = Path.Combine(_destDir, "InvalidDecoder", fileName);

                return GetUniqueFileName(destPath);
            }
            case CorruptChecker.ImageCorruptness.None:
            {
                if (imageMetadata.EarliestDate.HasValue)
                {
                    var yearFolder = imageMetadata.EarliestDate.Value.Year.ToString();

                    var monthFolder = imageMetadata.EarliestDate.Value.ToString("MM-MMM");
                    var destPath = Path.Combine(_destDir, yearFolder, monthFolder, fileName);

                    return GetUniqueFileName(destPath);
                }

                var error = Path.Combine(_destDir, "error", fileName);
                return GetUniqueFileName(error);
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
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


    private ImageMetadata GetImageInfo(string filePath)
    {
        var creationTime = File.GetCreationTime(filePath);
        var lastWriteTime = File.GetLastWriteTime(filePath);
        var earliest = creationTime < lastWriteTime ? creationTime : lastWriteTime;

        var info = new ImageMetadata()
        {
            FilePath = filePath,
            EarliestDate = earliest,
        };


        try
        {
            bool isHeic = filePath.ToLower().EndsWith(".heic") || filePath.ToLower().EndsWith(".heif");

            var image = filePath switch
            {
                _ when isHeic => Image.Load(DecoderHeifOptions, filePath),
                _ => Image.Load(filePath)
            };

            using (image)
            {
                info.Width = image.Width;
                info.Height = image.Height;
                var rgba32 = image.CloneAs<Rgba32>();
                info.Corruptness = CorruptChecker.CheckForPartialCorruption(rgba32);
            }
        }
        catch (Exception heicEx) when (heicEx.Message.Contains("Unexpected end of file") ||
                                       heicEx.Message.Contains("iloc box") ||
                                       heicEx.Message.Contains("file bounds"))
        {
            info.Corruptness = CorruptChecker.ImageCorruptness.Truncated;
        }
        catch (Exception ex)
        {
            // If we can't read the image, use file dates
            Console.WriteLine($"Error creating image info : {filePath} -> {ex.Message}");
            info.Corruptness = CorruptChecker.ImageCorruptness.Invalid;
        }

        return info;
    }


    private string CalculateFileHash(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToBase64String(hashBytes);
    }
}