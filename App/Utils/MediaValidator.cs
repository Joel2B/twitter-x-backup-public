namespace Backup.App.Models.Utils;

public static class MediaValidator
{
    public static bool IsValid(string path, Action? debug = null)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            if (debug is not null)
                debug();

            return false;
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            FileExtension.JPG or FileExtension.JPEG => IsValidJpeg(path) || IsValidPng(path),
            FileExtension.PNG => IsValidPng(path),
            FileExtension.MP4 => IsLikelyMp4(path),
            _ => false,
        };
    }

    private static bool IsValidJpeg(string filePath)
    {
        try
        {
            byte[] buffer = new byte[4];
            using FileStream stream = File.OpenRead(filePath);

            if (stream.Length < 4)
                return false;

            stream.ReadExactly(buffer, 0, 4);

            // JPG starts with FF D8 and ends with FF D9
            return buffer[0] == 0xFF && buffer[1] == 0xD8;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidPng(string filePath)
    {
        try
        {
            byte[] buffer = new byte[8];
            using FileStream stream = File.OpenRead(filePath);

            if (stream.Length < 8)
                return false;

            stream.ReadExactly(buffer);

            // PNG starts with: 89 50 4E 47 0D 0A 1A 0A
            return buffer[0] == 0x89
                && buffer[1] == 0x50
                && buffer[2] == 0x4E
                && buffer[3] == 0x47
                && buffer[4] == 0x0D
                && buffer[5] == 0x0A
                && buffer[6] == 0x1A
                && buffer[7] == 0x0A;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLikelyMp4(string filePath)
    {
        try
        {
            const int MaxHeaderSize = 64;
            byte[] buffer = new byte[MaxHeaderSize];

            using FileStream stream = File.OpenRead(filePath);
            int read = stream.Read(buffer, 0, buffer.Length);

            for (int i = 0; i < read - 3; i++)
            {
                if (
                    buffer[i] == 'f'
                    && buffer[i + 1] == 't'
                    && buffer[i + 2] == 'y'
                    && buffer[i + 3] == 'p'
                )
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
