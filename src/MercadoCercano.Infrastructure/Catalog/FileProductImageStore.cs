using System.Text.RegularExpressions;
using MercadoCercano.Application.Catalog;

namespace MercadoCercano.Infrastructure.Catalog;

public sealed partial class FileProductImageStore(string directory) : IProductImageStore
{
    public const int MaximumBytes = 750 * 1024;

    public async Task<string> SaveAsync(Stream stream, long length, string contentType, CancellationToken ct)
    {
        if (length is <= 0 or > MaximumBytes)
            throw new CatalogException(CatalogError.Validation, "La imagen debe tener entre 1 byte y 750 KiB.");
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(block, ct)) != 0)
        {
            if (buffer.Length + read > MaximumBytes)
                throw new CatalogException(CatalogError.Validation, "La imagen supera 750 KiB.");
            await buffer.WriteAsync(block.AsMemory(0, read), ct);
        }
        var data = buffer.ToArray();
        var extension = DetectExtension(data, contentType);
        Directory.CreateDirectory(directory);
        var fileName = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(directory, fileName);
        var complete = false;
        try
        {
            await File.WriteAllBytesAsync(path, data, ct);
            complete = true;
            return fileName;
        }
        finally
        {
            if (!complete) File.Delete(path);
        }
    }

    public void Delete(string fileName) => File.Delete(GetPath(fileName));

    public StoredImageFile Open(string fileName)
    {
        var path = GetPath(fileName);
        if (!File.Exists(path))
            throw new CatalogException(CatalogError.NotFound, "No se encontro el archivo de la imagen.");
        var contentType = Path.GetExtension(fileName) switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => throw new CatalogException(CatalogError.NotFound, "Formato de imagen no disponible.")
        };
        return new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete), contentType);
    }

    private string GetPath(string fileName)
    {
        if (!FileNamePattern().IsMatch(fileName))
            throw new CatalogException(CatalogError.NotFound, "La imagen no existe.");
        return Path.Combine(directory, fileName);
    }

    private static string DetectExtension(byte[] data, string contentType)
    {
        contentType = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        ReadOnlySpan<byte> bytes = data;
        if (contentType == "image/png" && bytes.Length >= 24 &&
            bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
            return ".png";
        if (contentType == "image/jpeg" && bytes.Length >= 4 && bytes[0] == 255 &&
            bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217)
            return ".jpg";
        if (contentType == "image/webp" && bytes.Length >= 20 &&
            bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return ".webp";
        throw new CatalogException(CatalogError.Validation, "Usa una imagen PNG, JPEG o WebP cuyo contenido coincida con el formato. SVG no esta permitido.");
    }

    [GeneratedRegex(@"^[a-f0-9]{32}\.(png|jpg|webp)$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();
}
