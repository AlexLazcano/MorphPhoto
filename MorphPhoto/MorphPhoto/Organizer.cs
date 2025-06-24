using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MorphPhoto.MorphPhoto;

public partial class Organizer(string sourceDir, string destDir, Organizer.OrganizerOptions organizerOptions)
{
    private readonly Dictionary<string, string> _duplicates = new();

    private readonly HashSet<string> _fileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".heic"
    };

    public enum OrganizationType
    {
        ByDate,
        ByExtension
    }

    public enum EHandleCorruptFiles
    {
        Skip,
        NormalOrganize,
        ExtensionOrganize,
    }

    public class OrganizerOptions
    {
        public OrganizationType OrganizationType = OrganizationType.ByDate;

        public EHandleCorruptFiles HandleCorruptFiles = EHandleCorruptFiles.NormalOrganize;

        // public bool SkipExistingFiles { get; set; } = false;
    }


    public void Organize()
    {
        try
        {
            WriteToConsole("Starting organize...");
            WriteToConsole($"Source Dir {sourceDir}");
            WriteToConsole($"Destination Dir {destDir}");

            Directory.CreateDirectory(destDir);


            var images = GetImageFiles(sourceDir);

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


    private List<string> GetImageFiles(string directory)
    {
        var files = new List<string>();

        try
        {
            var allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Where(file => _fileExtensions.Contains(Path.GetExtension(file)))
                .Where(file => !SkipPrefixes.Any(prefix =>
                    Path.GetFileName(file).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
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

        if (organizerOptions.HandleCorruptFiles == EHandleCorruptFiles.Skip &&
            imageInfo.Corruptness != CorruptChecker.ImageCorruptness.None)
        {
            return;
        }

        var destPath = GetDestPath(filePath, imageInfo);

        // Ensure destination directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(destPath));

        // Copy file to destination
        File.Copy(filePath, destPath, false);

        // Ensure destination directory exists
        WriteToConsole($"Organized: {Path.GetFileName(filePath)} -> {GetRelativePath(destDir, destPath)}",
            ConsoleColor.Green);
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
}