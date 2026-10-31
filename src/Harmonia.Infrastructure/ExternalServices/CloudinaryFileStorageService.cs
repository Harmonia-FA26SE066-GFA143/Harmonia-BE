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
        // A raw file's id must carry the extension itself, so it is already there.
        var isRaw = resourceType == ResourceType.Raw;
        var cloudinaryId = $"{RootFolder}/{folder}/{Guid.NewGuid()}{(isRaw ? extension : string.Empty)}";
        // Dynamic-folder accounts ignore slashes in PublicId when placing assets in the Media Library.
        var assetFolder = $"{RootFolder}/{folder}";
        // The client's original name never reaches Cloudinary; PublicId above decides the stored name.
        var file = new FileDescription("upload" + extension, content);
        var type = isPrivate ? PrivateDeliveryType : PublicDeliveryType;

        try
        {
            UploadResult result = resourceType switch
            {
                ResourceType.Video => await _cloudinary.UploadAsync(
                    new VideoUploadParams { File = file, PublicId = cloudinaryId, AssetFolder = assetFolder, Type = type, Overwrite = false },
                    cancellationToken),
                ResourceType.Raw => await _cloudinary.UploadAsync(
                    new RawUploadParams { File = file, PublicId = cloudinaryId, AssetFolder = assetFolder, Type = type, Overwrite = false },
                    "raw",
                    cancellationToken),
                _ => await _cloudinary.UploadAsync(
                    new ImageUploadParams { File = file, PublicId = cloudinaryId, AssetFolder = assetFolder, Type = type, Overwrite = false },
                    cancellationToken),
            };

            if (result.Error is not null)
            {
                _logger.LogWarning("Cloudinary rejected upload to {Folder}: {Error}", folder, result.Error.Message);
                return Result<FileUploadResponse>.Failure(ErrorCodes.ExternalStorageFailed);
            }

            return Result<FileUploadResponse>.Success(
                new FileUploadResponse(isRaw ? result.PublicId : result.PublicId + extension, result.SecureUrl.ToString()));
        }
        catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Cloudinary upload to {Folder} failed", folder);
            return Result<FileUploadResponse>.Failure(ErrorCodes.ExternalStorageFailed);
        }
    }

    public string GetSignedUrl(string publicId)
    {
        var (cloudinaryId, format, resourceType) = Split(publicId);

        // A plain signed delivery URL never expires, so use the private download API,
        // which honours expires_at.
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.SignedUrlExpiryMinutes).ToUnixTimeSeconds();

        return _cloudinary.DownloadPrivate(
            cloudinaryId,
            attachment: false,
            format: format,
            type: PrivateDeliveryType,
            expiresAt: expiresAt,
            resourceType: resourceType.ToString().ToLowerInvariant());
    }

    public async Task<Result> DeleteAsync(string publicId, bool isPrivate, CancellationToken cancellationToken)
    {
        var (cloudinaryId, _, resourceType) = Split(publicId);

        try
        {
            // The SDK's DestroyAsync takes no CancellationToken; WaitAsync stops us waiting, the call itself still completes.
            var result = await _cloudinary.DestroyAsync(new DeletionParams(cloudinaryId)
            {
                ResourceType = resourceType,
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

    private static (string CloudinaryId, string Format, ResourceType ResourceType) Split(string publicId)
    {
        var extension = Path.GetExtension(publicId).ToLowerInvariant();
        var resourceType = GetResourceType(extension);

        // A raw file's Cloudinary id includes the extension and it has no separate format.
        return resourceType == ResourceType.Raw
            ? (publicId, string.Empty, resourceType)
            : (publicId[..^extension.Length], extension.TrimStart('.'), resourceType);
    }

    // Cloudinary files audio under the "video" resource type; PDFs are served as "image".
    // Exported report files other than PDF, MIDI (which Cloudinary cannot transcode) and Word lyrics are "raw":
    // stored as they are, never transformed.
    // An unsupported extension is a caller bug (callers whitelist first), so it still throws.
    private static ResourceType GetResourceType(string extension) => extension switch
    {
        ".mp3" or ".m4a" or ".wav" => ResourceType.Video,
        ".pdf" or ".png" or ".jpg" or ".jpeg" => ResourceType.Image,
        ".csv" or ".xlsx" or ".xml" or ".json" or ".mid" or ".midi" or ".docx" => ResourceType.Raw,
        _ => throw new NotSupportedException($"File extension '{extension}' is not supported for storage.")
    };
}
