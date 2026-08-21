namespace Detara.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Secao = "Storage";
    public string Provider { get; init; } = "Local";
    public LocalStorageOptions Local { get; init; } = new();
    public S3StorageOptions S3 { get; init; } = new();
}

public sealed class LocalStorageOptions
{
    public string RootPath { get; init; } = "data/storage";
}

public sealed class S3StorageOptions
{
    public string ServiceUrl { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public bool ForcePathStyle { get; init; } = true;
}
