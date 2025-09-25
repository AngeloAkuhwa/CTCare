namespace CTCare.Application.Files;

public interface IFileStorage
{
    Task<FileUploadResult> UploadAsync(Stream content, string fileName, string contentType, long length, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}

public sealed class FileUploadResult
{
    public string StoragePath { get; set; }
    public long SizeBytes { get; set; }
    public string ContentType { get; set; }
    public string FileName { get; set; }
    public string SecureUrl { get; set; }
    public string? ETag { get; set; }
    public string Version { get; set; }
}
