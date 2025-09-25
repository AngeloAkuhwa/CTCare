namespace CTCare.Shared.Settings;

public class CloudinarySettings
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; }
    public string ApiKey { get; set; }
    public string ApiSecret { get; set; }
    public string Folder { get; set; }
    public long MaxUploadSizeBytes { get; set; }
    public string[] AllowedContentTypes { get; set; }
}
