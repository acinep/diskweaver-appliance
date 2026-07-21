using ApplianceManager.Garage;

namespace ApplianceManager.Daemon.Tests;

/// <summary>In-memory stand-in for <see cref="IGarageClient"/> -- no real `garage` CLI/process involved.</summary>
public sealed class FakeGarageClient : IGarageClient
{
    private readonly Dictionary<string, GarageBucket> _buckets = [];
    private readonly Dictionary<string, GarageKey> _keys = [];
    private int _nextId;

    public IReadOnlyList<GarageBucket> GetBuckets() => _buckets.Values.ToList();

    public GarageBucket CreateBucket(string name)
    {
        if (_buckets.Values.Any(b => b.Name == name))
        {
            throw new InvalidOperationException($"Bad request: {name}: bucket already exists.");
        }

        var bucket = new GarageBucket($"bucket-{_nextId++}", name, "2026-01-01 00:00:00.000 +00:00", 0, 0, false, []);
        _buckets[bucket.Id] = bucket;
        return bucket;
    }

    public void DeleteBucket(string name)
    {
        var bucket = FindBucket(name);
        _buckets.Remove(bucket.Id);
    }

    public IReadOnlyList<GarageKey> GetKeys() => _keys.Values.Select(Redacted).ToList();

    public GarageKey CreateKey(string name)
    {
        var key = new GarageKey($"key-{_nextId++}", name, $"secret-{Guid.NewGuid():N}", "2026-01-01 00:00:00.000 +00:00", true, null, false, []);
        _keys[key.Id] = key;
        return key;
    }

    public void DeleteKey(string id) => _keys.Remove(id);

    public GarageKey SetCanCreateBuckets(string keyId, bool allow)
    {
        var key = _keys[keyId];
        var updated = key with { CanCreateBuckets = allow };
        _keys[keyId] = updated;
        return Redacted(updated);
    }

    public GarageBucket SetBucketPermission(string bucketName, string keyId, bool read, bool write, bool owner)
    {
        var bucket = FindBucket(bucketName);
        var key = _keys[keyId];
        var grants = bucket.Keys.Where(g => g.KeyId != keyId).Append(new GarageBucketKeyGrant(keyId, key.Name, read, write, owner)).ToList();
        var updated = bucket with { Keys = grants };
        _buckets[bucket.Id] = updated;
        return updated;
    }

    public GarageBucket RevokeBucketPermission(string bucketName, string keyId, bool read, bool write, bool owner)
    {
        var bucket = FindBucket(bucketName);
        var updated = bucket with { Keys = bucket.Keys.Where(g => g.KeyId != keyId).ToList() };
        _buckets[bucket.Id] = updated;
        return updated;
    }

    private GarageBucket FindBucket(string name) =>
        _buckets.Values.FirstOrDefault(b => b.Name == name || b.Id == name)
            ?? throw new InvalidOperationException($"GetBucketInfo returned NoSuchBucket (404): Bucket not found: {name}");

    private static GarageKey Redacted(GarageKey key) => key with { SecretKey = null };
}
