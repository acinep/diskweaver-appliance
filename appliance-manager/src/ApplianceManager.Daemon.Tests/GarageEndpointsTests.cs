using System.Net;
using System.Net.Http.Json;
using ApplianceManager.Garage;

namespace ApplianceManager.Daemon.Tests;

// Deliberately NOT an IClassFixture -- every test here mutates Garage state (creates/deletes
// buckets and keys), so a fixture shared across the whole class would let tests interfere with
// each other depending on run order (confirmed live: GetBuckets_EmptyByDefault failed once other
// tests' buckets leaked into it). A fresh factory/FakeGarageClient per test avoids that entirely.
public class GarageEndpointsTests : IDisposable
{
    private readonly ApplianceManagerWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public GarageEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetBuckets_EmptyByDefault()
    {
        var buckets = await _client.GetFromJsonAsync<List<GarageBucket>>("/garage/buckets");

        Assert.Empty(buckets!);
    }

    [Fact]
    public async Task PostBucket_ThenGetBuckets_ShowsIt()
    {
        var created = await _client.PostAsJsonAsync("/garage/buckets", new CreateBucketRequest("test-bucket-1"));
        created.EnsureSuccessStatusCode();

        var buckets = await _client.GetFromJsonAsync<List<GarageBucket>>("/garage/buckets");

        Assert.Contains(buckets!, b => b.Name == "test-bucket-1");
    }

    [Fact]
    public async Task PostBucket_DuplicateName_ReturnsBadRequest()
    {
        await _client.PostAsJsonAsync("/garage/buckets", new CreateBucketRequest("dup-bucket"));

        var response = await _client.PostAsJsonAsync("/garage/buckets", new CreateBucketRequest("dup-bucket"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBucket_RemovesIt()
    {
        await _client.PostAsJsonAsync("/garage/buckets", new CreateBucketRequest("to-delete"));

        var response = await _client.DeleteAsync("/garage/buckets/to-delete");
        response.EnsureSuccessStatusCode();

        var buckets = await _client.GetFromJsonAsync<List<GarageBucket>>("/garage/buckets");
        Assert.DoesNotContain(buckets!, b => b.Name == "to-delete");
    }

    [Fact]
    public async Task DeleteBucket_UnknownName_ReturnsBadRequest()
    {
        var response = await _client.DeleteAsync("/garage/buckets/does-not-exist");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostKey_ReturnsTheOneTimeSecret()
    {
        var response = await _client.PostAsJsonAsync("/garage/keys", new CreateKeyRequest("test-key-1"));
        var key = await response.Content.ReadFromJsonAsync<GarageKey>();

        Assert.NotNull(key!.SecretKey);
    }

    [Fact]
    public async Task GetKeys_NeverExposesTheSecret()
    {
        await _client.PostAsJsonAsync("/garage/keys", new CreateKeyRequest("test-key-2"));

        var keys = await _client.GetFromJsonAsync<List<GarageKey>>("/garage/keys");

        Assert.All(keys!, k => Assert.Null(k.SecretKey));
    }

    [Fact]
    public async Task SetCanCreateBuckets_TogglesTheFlag()
    {
        var createResponse = await _client.PostAsJsonAsync("/garage/keys", new CreateKeyRequest("togglable-key"));
        var key = await createResponse.Content.ReadFromJsonAsync<GarageKey>();

        var response = await _client.PostAsJsonAsync($"/garage/keys/{key!.Id}/can-create-buckets", new SetCanCreateBucketsRequest(true));
        var updated = await response.Content.ReadFromJsonAsync<GarageKey>();

        Assert.True(updated!.CanCreateBuckets);
    }

    [Fact]
    public async Task GrantThenRevoke_BucketPermission_RoundTrips()
    {
        await _client.PostAsJsonAsync("/garage/buckets", new CreateBucketRequest("grant-bucket"));
        var keyResponse = await _client.PostAsJsonAsync("/garage/keys", new CreateKeyRequest("grant-key"));
        var key = await keyResponse.Content.ReadFromJsonAsync<GarageKey>();

        var grantResponse = await _client.PostAsJsonAsync(
            "/garage/buckets/grant-bucket/grants", new BucketGrantRequest(key!.Id, Read: true, Write: true, Owner: false));
        var granted = await grantResponse.Content.ReadFromJsonAsync<GarageBucket>();
        Assert.Contains(granted!.Keys, g => g.KeyId == key.Id && g.Read && g.Write && !g.Owner);

        var revokeResponse = await _client.DeleteAsync($"/garage/buckets/grant-bucket/grants/{key.Id}");
        var revoked = await revokeResponse.Content.ReadFromJsonAsync<GarageBucket>();
        Assert.DoesNotContain(revoked!.Keys, g => g.KeyId == key.Id);
    }
}
