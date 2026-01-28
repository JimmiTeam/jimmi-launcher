using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace JimmiLauncher;

public static class FileWatcher
{
    public static void OnCreated(object sender, FileSystemEventArgs e)
    {
        var image = Image.Load(e.FullPath);
        var encoder = new JpegEncoder
        {
            Quality = 40
        };
        image.Mutate(x => x.Resize(320, 240));
        image.Save(e.FullPath.Replace(e.Name!, "thumbnail.jpg"), encoder);
        File.Delete(e.FullPath);
    }

    public static void OnDeleted(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"File deleted: {e.FullPath}");
        // Add additional logic here, e.g., update database or UI
        
    }
    public static void OnChanged(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"File changed: {e.FullPath}");
        // Add additional logic here, e.g., update database or UI
    }
    public static void OnRenamed(object sender, RenamedEventArgs e)
    {
        Console.WriteLine($"File renamed from {e.OldFullPath} to {e.FullPath}");
        // Add additional logic here, e.g., update database or UI
    }
    public static void OnError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"File system watcher error: {e.GetException().Message}");
        // Add additional error handling logic here
    }
}