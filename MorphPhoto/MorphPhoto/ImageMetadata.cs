using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MorphPhoto.MorphPhoto;

public class ImageMetadata
{
    public string FilePath { get; set; }
    // public DateTime CreationTime { get; set; }
    // public DateTime LastWriteTime { get; set; }
    public DateTime? EarliestDate { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public CorruptChecker.ImageCorruptness Corruptness;

}