namespace VendingAdSystem.Application.Services;

public interface IFileStorageService
{
    Task<FileStorageWriteResult> SaveAsync(
        string sourcePath,
        string fileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);
}

public sealed record FileStorageWriteResult(string FileUrl);
