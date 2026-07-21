namespace ApplianceManager.Garage;

/// <summary>One S3 bucket as reported by `garage bucket info`.</summary>
/// <param name="Id">Garage's internal bucket id (the long hex string) -- what every other `garage bucket` subcommand accepts, alongside a global alias.</param>
/// <param name="Name">The bucket's global alias -- what a user actually named it. Null for a bucket with no global alias (possible via `garage bucket unalias`, not something this client creates).</param>
/// <param name="Keys">Every access key currently granted some permission on this bucket.</param>
public sealed record GarageBucket(
    string Id,
    string? Name,
    string CreatedAt,
    long SizeBytes,
    long ObjectCount,
    bool WebsiteEnabled,
    IReadOnlyList<GarageBucketKeyGrant> Keys);

/// <summary>One access key's permissions on a <see cref="GarageBucket"/>.</summary>
public sealed record GarageBucketKeyGrant(string KeyId, string? KeyName, bool Read, bool Write, bool Owner);
