namespace ApplianceManager.Garage;

/// <summary>
/// One S3 access key as reported by `garage key info`.
/// </summary>
/// <param name="SecretKey">
/// Only ever non-null immediately after <see cref="IGarageClient.CreateKey"/> -- `garage key info`
/// prints "(redacted)" for every call after the key's own creation, and this client passes that
/// through as null rather than the literal string, so callers can never mistake "(redacted)" for a
/// real secret. There is no way to recover a key's secret later; losing it means creating a new key.
/// </param>
/// <param name="Buckets">Every bucket this key currently has some permission on.</param>
public sealed record GarageKey(
    string Id,
    string Name,
    string? SecretKey,
    string CreatedAt,
    bool Valid,
    string? Expiration,
    bool CanCreateBuckets,
    IReadOnlyList<GarageKeyBucketGrant> Buckets);

/// <summary>One bucket a <see cref="GarageKey"/> has some permission on.</summary>
public sealed record GarageKeyBucketGrant(string BucketId, string? BucketName, bool Read, bool Write, bool Owner);
