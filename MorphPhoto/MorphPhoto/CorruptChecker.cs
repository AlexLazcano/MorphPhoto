using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MorphPhoto.MorphPhoto;

public static class CorruptChecker
{
    private const double CORRUPTION_THRESHOLD = 30.0 / 100.0;
    private const int sampleSize = 1000;

    public enum ImageCorruptness
    {
        None,
        Partial,
        Truncated,
        Invalid
    }


    public static ImageCorruptness CheckForPartialCorruption(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var halfWidth = width / 2;

        var totalPixels = width * height;
        int blackPixelCount = 0;
        int sampledPixels = 0;

        var sampleCount = Math.Min(totalPixels, sampleSize);
        var stepX = Math.Max(1, width / (int)Math.Sqrt(sampleCount));
        var stepY = Math.Max(1, height / (int)Math.Sqrt(sampleCount));

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y += stepY)
            {
                var row = accessor.GetRowSpan(y);

                for (int x = 0; x < Math.Min(halfWidth, row.Length); x += stepX)
                {
                    var pixel = row[x];

                    if (pixel is { R: < 10, G: < 10, B: < 10 })
                        blackPixelCount++;

                    sampledPixels++;
                }
            }
        });

        var corruptionRatio = blackPixelCount / (double)sampledPixels;

        return corruptionRatio > CORRUPTION_THRESHOLD ? ImageCorruptness.Partial : ImageCorruptness.None;
    }
}