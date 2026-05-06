using System.Security.Cryptography;

namespace CadastraAI.API.Storage;

public interface ILocalFileStorage
{
    /// <summary>
    /// Saves the upload to wwwroot/uploads/{folder}/. Returns the public URL relative to the API host
    /// (e.g. "/uploads/empresa-logos/abc.png").
    /// </summary>
    Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct);

    /// <summary>Best-effort delete. Returns true if a file was actually removed.</summary>
    bool TryDelete(string publicUrl);
}

public class LocalFileStorage(IWebHostEnvironment env, ILogger<LocalFileStorage> logger) : ILocalFileStorage
{
    private static readonly Dictionary<string, string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "image/png", ".png" },
        { "image/jpeg", ".jpg" },
        { "image/webp", ".webp" },
    };

    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const string UploadsRoot = "uploads";

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidOperationException("Arquivo vazio.");
        }

        if (file.Length > MaxImageBytes)
        {
            throw new InvalidOperationException("Arquivo excede o tamanho máximo de 5MB.");
        }

        if (!AllowedImageExtensions.TryGetValue(file.ContentType, out var extension))
        {
            throw new InvalidOperationException("Formato não suportado. Envie PNG, JPG ou WEBP.");
        }

        var webRootPath = env.WebRootPath;
        if (string.IsNullOrEmpty(webRootPath))
        {
            webRootPath = Path.Combine(env.ContentRootPath, "wwwroot");
        }

        var folderPath = Path.Combine(webRootPath, UploadsRoot, folder);
        Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid():N}-{NowSlug()}{extension}";
        var fullPath = Path.Combine(folderPath, fileName);

        await using (var fs = File.Create(fullPath))
        {
            await file.CopyToAsync(fs, ct);
        }

        logger.LogInformation("Stored upload {Path} ({Size} bytes)", fullPath, file.Length);

        return $"/{UploadsRoot}/{folder}/{fileName}";
    }

    public bool TryDelete(string publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl)) return false;
        if (!publicUrl.StartsWith($"/{UploadsRoot}/", StringComparison.OrdinalIgnoreCase)) return false;

        var webRootPath = env.WebRootPath;
        if (string.IsNullOrEmpty(webRootPath))
        {
            webRootPath = Path.Combine(env.ContentRootPath, "wwwroot");
        }

        var relative = publicUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(webRootPath, relative);

        // Defensive: make sure we don't escape the uploads root via traversal.
        var uploadsRoot = Path.GetFullPath(Path.Combine(webRootPath, UploadsRoot));
        var normalized = Path.GetFullPath(fullPath);
        if (!normalized.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Refusing to delete path outside uploads root: {Path}", fullPath);
            return false;
        }

        try
        {
            if (File.Exists(normalized))
            {
                File.Delete(normalized);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete {Path}", normalized);
        }
        return false;
    }

    private static string NowSlug()
    {
        return DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    }

    // Kept here in case we want short hashes later — not used right now but cheap to keep around.
    public static string ShortRandom(int bytes = 6)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}
