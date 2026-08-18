namespace Detara.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Secao = "Storage";
    public string Provider { get; init; } = "Local";
    public LocalStorageOptions Local { get; init; } = new();
}

public sealed class LocalStorageOptions
{
    public string RootPath { get; init; } = "data/storage";
}
