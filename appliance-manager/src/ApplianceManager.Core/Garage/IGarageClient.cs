namespace ApplianceManager.Garage;

/// <summary>Bucket/key management for the appliance's local Garage instance. See <see cref="GarageCliClient"/>.</summary>
public interface IGarageClient
{
    IReadOnlyList<GarageBucket> GetBuckets();
    GarageBucket CreateBucket(string name);
    void DeleteBucket(string name);

    IReadOnlyList<GarageKey> GetKeys();
    /// <summary>The only call whose result ever carries a non-null <see cref="GarageKey.SecretKey"/> -- see its doc comment.</summary>
    GarageKey CreateKey(string name);
    void DeleteKey(string id);
    GarageKey SetCanCreateBuckets(string keyId, bool allow);

    GarageBucket SetBucketPermission(string bucketName, string keyId, bool read, bool write, bool owner);
    GarageBucket RevokeBucketPermission(string bucketName, string keyId, bool read, bool write, bool owner);
}
