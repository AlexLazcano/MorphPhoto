// See https://aka.ms/new-console-template for more information

using MorphPhoto.MorphPhoto;

Console.WriteLine("Hello, World!");
Console.WriteLine("=== Picture Organizer (Synchronous) ===\n");


try
{
    // Get source and destination directories
    string sourceDir, destDir;

    if (args.Length >= 2)
    {
        sourceDir = args[0];
        destDir = args[1];
    }
    else
    {
        // Console.Write("Enter source directory: ");
        // sourceDir = Console.ReadLine()?.Trim('"');
        //
        // sourceDir = @"X:\.backup\Test";
        sourceDir = @"X:\.backup\May 2022";

        
        var folderName = Path.GetFileName(sourceDir);
        // Console.Write("Enter destination directory: ");
        // destDir = Console.ReadLine()?.Trim('"');'
        destDir = @$"X:\.backup\organized\{folderName}";
    }

    if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(destDir))
    {
        Console.WriteLine("Source and destination directories are required!");
        return;
    }

    if (!Directory.Exists(sourceDir))
    {
        Console.WriteLine($"Source directory does not exist: {sourceDir}");
        return;
    }


    // Create organizer and run
    var organizer = new Organizer(sourceDir, destDir, new Organizer.OrganizerOptions()
    {
        OrganizationType = Organizer.OrganizationType.ByDate,
        HandleCorruptFiles = Organizer.EHandleCorruptFiles.NormalOrganize
    });
    organizer.Organize();
    Console.Write("Done");
}
catch (Exception ex)
{
    Console.WriteLine($"Application error: {ex.Message}");
}