using ApplianceManager.Executor.Tests;

namespace ApplianceManager.Garage.Tests;

public class GarageCliClientTests
{
    private const string EmptyBucketList = "ID  Created  Global aliases  Local aliases";

    private static string BucketInfo(string id, string name) => $"""
        ==== BUCKET INFORMATION ====
        Bucket:          {id}
        Created:         2026-07-21 00:24:27.463 +00:00

        Size:            0 B (0 B)
        Objects:         0

        Website access:  false

        Global alias:    {name}

        ==== KEYS FOR THIS BUCKET ====
        Permissions  Access key  Local aliases
        """;

    private static string KeyInfo(string id, string name, bool canCreateBuckets, string? secret = null) => $"""
        ==== ACCESS KEY INFORMATION ====
        Key ID:              {id}
        Key name:            {name}
        Secret key:          {secret ?? "(redacted)"}
        Created:             2026-07-20 20:07:39.443 +00:00
        Validity:            valid
        Expiration:          never

        Can create buckets:  {(canCreateBuckets ? "true" : "false")}

        ==== BUCKETS FOR THIS KEY ====
        Permissions  ID  Global aliases  Local aliases
        """;

    [Fact]
    public void GetBuckets_ListsThenDescribesEachOne()
    {
        var runner = new FakeCommandRunner();
        runner.Respond("garage", ["bucket", "list"], """
            ID       Created     Global aliases  Local aliases
            abc123   2026-07-21  my-bucket
            """);
        runner.Respond("garage", ["bucket", "info", "abc123"], BucketInfo("abc123", "my-bucket"));

        var client = new GarageCliClient(runner);
        var bucket = Assert.Single(client.GetBuckets());

        Assert.Equal("abc123", bucket.Id);
        Assert.Equal("my-bucket", bucket.Name);
    }

    [Fact]
    public void GetBuckets_NoBuckets_ReturnsEmpty_NeverCallsInfo()
    {
        // FakeCommandRunner throws on any unexpected call -- if this called `bucket info` on
        // nothing, the test would fail with "No canned response" instead of just passing empty.
        var runner = new FakeCommandRunner();
        runner.Respond("garage", ["bucket", "list"], EmptyBucketList);

        var client = new GarageCliClient(runner);

        Assert.Empty(client.GetBuckets());
    }

    [Fact]
    public void CreateBucket_PassesNameThrough_ParsesTheReturnedInfo()
    {
        var runner = new FakeCommandRunner();
        runner.Respond("garage", ["bucket", "create", "new-bucket"], BucketInfo("xyz789", "new-bucket"));

        var client = new GarageCliClient(runner);
        var bucket = client.CreateBucket("new-bucket");

        Assert.Equal("new-bucket", bucket.Name);
    }

    [Fact]
    public void DeleteBucket_PassesYesFlag_NoInteractivePrompt()
    {
        var runner = new FakeCommandRunner();
        runner.Respond("garage", ["bucket", "delete", "--yes", "old-bucket"], "Bucket xyz789 has been deleted.");

        var client = new GarageCliClient(runner);
        client.DeleteBucket("old-bucket");
    }

    [Fact]
    public void GetKeys_ListsThenDescribesEachOne()
    {
        var runner = new FakeCommandRunner();
        runner.Respond("garage", ["key", "list"], """
            ID   Created     Name    Expiration
            GK1  2026-07-20  my-key  never
            """);
        runner.Respond("garage", ["key", "info", "GK1"], KeyInfo("GK1", "my-key", canCreateBuckets: false));

        var client = new GarageCliClient(runner);
        var key = Assert.Single(client.GetKeys());

        Assert.Equal("GK1", key.Id);
        Assert.Equal("my-key", key.Name);
        // GetKeys goes through `key info`, which always redacts -- confirms the client doesn't
        // accidentally leak a secret through the listing path.
        Assert.Null(key.SecretKey);
    }

    [Fact]
    public void CreateKey_ReturnsTheOneTimeSecret()
    {
        var runner = new FakeCommandRunner();
        runner.Respond("garage", ["key", "create", "new-key"], KeyInfo("GK2", "new-key", canCreateBuckets: false, secret: "supersecrethex"));

        var client = new GarageCliClient(runner);
        var key = client.CreateKey("new-key");

        Assert.Equal("supersecrethex", key.SecretKey);
    }

    [Fact]
    public void SetCanCreateBuckets_True_UsesAllow_False_UsesDeny()
    {
        var runner = new FakeCommandRunner();
        runner.Respond("garage", ["key", "allow", "--create-bucket", "GK1"], KeyInfo("GK1", "k", canCreateBuckets: true));
        runner.Respond("garage", ["key", "deny", "--create-bucket", "GK1"], KeyInfo("GK1", "k", canCreateBuckets: false));

        var client = new GarageCliClient(runner);

        Assert.True(client.SetCanCreateBuckets("GK1", allow: true).CanCreateBuckets);
        Assert.False(client.SetCanCreateBuckets("GK1", allow: false).CanCreateBuckets);
    }

    [Fact]
    public void SetBucketPermission_OnlyPassesTheFlagsRequested()
    {
        var runner = new FakeCommandRunner();
        runner.Respond(
            "garage", ["bucket", "allow", "--read", "--key", "GK1", "my-bucket"],
            BucketInfo("abc123", "my-bucket"));

        var client = new GarageCliClient(runner);
        client.SetBucketPermission("my-bucket", "GK1", read: true, write: false, owner: false);
    }

    [Fact]
    public void RevokeBucketPermission_UsesDenyWithTheSameFlagShape()
    {
        var runner = new FakeCommandRunner();
        runner.Respond(
            "garage", ["bucket", "deny", "--write", "--owner", "--key", "GK1", "my-bucket"],
            BucketInfo("abc123", "my-bucket"));

        var client = new GarageCliClient(runner);
        client.RevokeBucketPermission("my-bucket", "GK1", read: false, write: true, owner: true);
    }
}
