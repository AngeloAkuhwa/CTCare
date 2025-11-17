using System.Net;

using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

using CTCare.Application.Files;
using CTCare.Shared.Settings;

using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Files;

// TODO: keep this slim. separate cloudinary api stand alone and re-inject into this service

public sealed class CloudinaryFileStorage: IFileStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly CloudinarySettings _settings;

    public CloudinaryFileStorage(IOptions<CloudinarySettings> settings)
    {
        _settings = settings.Value;
        var acc = new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret);
        _cloudinary = new Cloudinary(acc)
        {
            Api =
            {
                Secure = true
            }
        };
    }

    public async Task<FileUploadResult> UploadAsync(Stream content, string fileName, string contentType, long length, CancellationToken ct = default)
    {
        if (length > _settings.MaxUploadSizeBytes || !_settings.AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException("Unsupported file type or File exceeds the maximum allowed size");
        }

        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(fileName, content),
            Folder = _settings.Folder,
            PublicId = Path.GetFileNameWithoutExtension(fileName)
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.StatusCode != HttpStatusCode.OK || (int)result.StatusCode >= (int)HttpStatusCode.BadRequest || string.IsNullOrWhiteSpace(result.PublicId))
        {
            throw new InvalidOperationException("Cloudinary upload failed.");
        }

        return new FileUploadResult
        {
            StoragePath = result.PublicId,
            SecureUrl = result.SecureUrl?.ToString() ?? "",
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = content.Length,
            ETag = result.Etag,
            Version = result.Version
        };
    }

    public async Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return;
        }

        var deletion = await _cloudinary.DestroyAsync(new DeletionParams(storagePath) { ResourceType = ResourceType.Raw });
        if (deletion.StatusCode != HttpStatusCode.OK || (int)deletion.StatusCode >= 500)
        {
            throw new InvalidOperationException("Cloudinary delete failed.");
        }
    }
}
