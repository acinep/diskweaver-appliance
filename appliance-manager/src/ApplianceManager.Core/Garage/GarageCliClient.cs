using ApplianceManager.Executor;

namespace ApplianceManager.Garage;

/// <summary>
/// Manages the appliance's local Garage instance by shelling out to the `garage` CLI, since it has
/// no JSON output mode to talk to instead (see <see cref="GarageCliParser"/>'s doc comment).
/// Requires `garage` on PATH and a readable /etc/garage.toml, so this only works on the appliance
/// itself -- and needs the daemon's own user in the `garage` group (see provision.sh).
/// </summary>
public sealed class GarageCliClient(ICommandRunner? commandRunner = null) : IGarageClient
{
    private readonly ICommandRunner _runner = commandRunner ?? new ProcessCommandRunner();

    public IReadOnlyList<GarageBucket> GetBuckets()
    {
        var ids = GarageCliParser.ParseListIds(_runner.Run("garage", ["bucket", "list"]));
        return ids.Select(id => GarageCliParser.ParseBucketInfo(_runner.Run("garage", ["bucket", "info", id]))).ToList();
    }

    public GarageBucket CreateBucket(string name) =>
        GarageCliParser.ParseBucketInfo(_runner.Run("garage", ["bucket", "create", name]));

    public void DeleteBucket(string name) => _runner.Run("garage", ["bucket", "delete", "--yes", name]);

    public IReadOnlyList<GarageKey> GetKeys()
    {
        var ids = GarageCliParser.ParseListIds(_runner.Run("garage", ["key", "list"]));
        return ids.Select(id => GarageCliParser.ParseKeyInfo(_runner.Run("garage", ["key", "info", id]))).ToList();
    }

    public GarageKey CreateKey(string name) =>
        GarageCliParser.ParseKeyInfo(_runner.Run("garage", ["key", "create", name]));

    public void DeleteKey(string id) => _runner.Run("garage", ["key", "delete", "--yes", id]);

    public GarageKey SetCanCreateBuckets(string keyId, bool allow) =>
        GarageCliParser.ParseKeyInfo(_runner.Run("garage", ["key", allow ? "allow" : "deny", "--create-bucket", keyId]));

    public GarageBucket SetBucketPermission(string bucketName, string keyId, bool read, bool write, bool owner) =>
        GarageCliParser.ParseBucketInfo(_runner.Run("garage", BucketPermissionArgs("allow", bucketName, keyId, read, write, owner)));

    public GarageBucket RevokeBucketPermission(string bucketName, string keyId, bool read, bool write, bool owner) =>
        GarageCliParser.ParseBucketInfo(_runner.Run("garage", BucketPermissionArgs("deny", bucketName, keyId, read, write, owner)));

    private static List<string> BucketPermissionArgs(
        string verb, string bucketName, string keyId, bool read, bool write, bool owner)
    {
        var args = new List<string> { "bucket", verb };
        if (read) args.Add("--read");
        if (write) args.Add("--write");
        if (owner) args.Add("--owner");
        args.Add("--key");
        args.Add(keyId);
        args.Add(bucketName);
        return args;
    }
}
