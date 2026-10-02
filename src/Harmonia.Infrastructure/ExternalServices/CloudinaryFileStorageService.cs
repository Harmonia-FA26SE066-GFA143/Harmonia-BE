using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.ExternalServices;

public class CloudinaryFileStorageService : IFileStorageService
{
    private const string RootFolder = "harmonia";
    private const string PublicDeliveryType = "upload";
    private const string PrivateDeliveryType = "authenticated";

    private readonly Cloudinary _cloudinary;
    private readonly CloudinaryOptions _options;
    private readonly ILogger<CloudinaryFileStorageService> _logger;

    public CloudinaryFileStorageService(IOptions<CloudinaryOptions> options, ILogger<CloudinaryFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _cloudinary = new Cloudinary(new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret));
        _cloudinary.Api.Secure = true;
    }

    public async Task<Result<FileUploadResponse>> UploadAsync(
        Stream content, string fileName, string folder, bool isPrivate, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var resourceType = GetResourceType(extension);

        // Cloudinary keeps the extension out of image/video public ids; we add it back in the
        // returned id so GetSignedUrl/DeleteAsync can recover the resource type and format.
        var cloudinaryId = $"{RootFolder}/{folder}/{Guid.NewGuid()}";
        // The client's original name never reaches Cloudinary; PublicId above decides the stored name.
        var file = new FileDescription("upload" + extension, content);
        var type = isPrivate ? PrivateDeliveryType : PublicDeliveryType;

        try
        {
            UploadResult result = resourceType == ResourceType.Video
                ? await _cloudinary.UploadAsync(
                    new VideoUploadParams { File = file, PublicId = cloudinaryId, Type = type, Overwrite = false },
                    cancellationToken)
                : await _cloudinary.UploadAsync(
                    new ImageUploadParams { File = file, PublicId = cloudinaryId, Type = type, Overwrite = false },
                    cancellationToken);

            if (result.Error is not null)
            {
                _logger.LogWarning("Cloudinary rejected upload to {Folder}: {Error}", folder, result.Error.Message);
                return Result<FileUploadResponse>.Failure(ErrorCodes.ExternalStorageFailed);
            }

            return Result<FileUploadResponse>.Success(
                new FileUploadResponse(result.PublicId + extension, result.SecureUrl.ToString()));
        }
        catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Cloudinary upload to {Folder} failed", folder);
            return Result<FileUploadResponse>.Failure(ErrorCodes.ExternalStorageFailed);
        }
    }

    public string GetSignedUrl(string publicId)
    {
        var (cloudinaryId, extension) = Split(publicId);

        // A plain signed delivery URL never expires, so use the private download API,
        // which honours expires_at.
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.SignedUrlExpiryMinutes).ToUnixTimeSeconds();

        return _cloudinary.DownloadPrivate(
            cloudinaryId,
            attachment: false,
            format: extension.TrimStart('.'),
            type: PrivateDeliveryType,
            expiresAt: expiresAt,
            resourceType: GetResourceType(extension).ToString().ToLowerInvariant());
    }

    public async Task<Result> DeleteAsync(string publicId, bool isPrivate, CancellationToken cancellationToken)
    {
        var (cloudinaryId, extension) = Split(publicId);

        try
        {
            // The SDK's DestroyAsync takes no CancellationToken; WaitAsync stops us waiting, the call itself still completes.
            var result = await _cloudinary.DestroyAsync(new DeletionParams(cloudinaryId)
            {
                ResourceType = GetResourceType(extension),
                Type = isPrivate ? PrivateDeliveryType : PublicDeliveryType
            }).WaitAsync(cancellationToken);

            if (result.Error is not null)
            {
                _logger.LogWarning("Cloudinary rejected delete of {PublicId}: {Error}", publicId, result.Error.Message);
                return Result.Failure(ErrorCodes.ExternalStorageFailed);
            }

            return Result.Success();
        }
        catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Cloudinary delete of {PublicId} failed", publicId);
            return Result.Failure(ErrorCodes.ExternalStorageFailed);
        }
    }

    // Network errors and HttpClient timeouts. A cancelled request is not the provider's fault,
    // so it keeps propagating as OperationCanceledException.
    private static bool IsProviderFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static (string CloudinaryId, string Extension) Split(string publicId)
    {
        var extension = Path.GetExtension(publicId).ToLowerInvariant();
        return (publicId[..^extension.Length], extension);
    }

    // Cloudinary files audio under the "video" resource type; PDFs are served as "image".
    // An unsupported extension is a caller bug (callers whitelist first), so it still throws.
    private static ResourceType GetResourceType(string extension) => extension switch
    {
        ".mp3" or ".m4a" or ".wav" => ResourceType.Video,
        ".pdf" or ".png" or ".jpg" or ".jpeg" => ResourceType.Image,
        _ => throw new NotSupportedException($"File extension '{extension}' is not supported for storage.")
    };
}
