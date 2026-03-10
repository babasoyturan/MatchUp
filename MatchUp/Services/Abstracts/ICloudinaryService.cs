namespace MatchUp.Services.Abstracts
{
    public interface ICloudinaryService
    {
        Task<CloudinaryUploadResult?> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default);
    }
}
