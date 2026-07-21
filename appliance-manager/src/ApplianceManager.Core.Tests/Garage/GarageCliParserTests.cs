using ApplianceManager.Garage;

namespace ApplianceManager.Garage.Tests;

// Every raw string here is real `garage` v2.3.0 CLI output, captured live against a real Garage
// instance (not hand-written/guessed) -- see docs/garage.md's captured samples.
public class GarageCliParserTests
{
    [Fact]
    public void ParseListIds_SkipsHeaderRow_TakesFirstColumnOfEachDataRow()
    {
        const string output = """
            ID                          Created     Name         Expiration
            GK101dd383fa8d8dd14f780cb3  2026-07-20  s3-01-admin  never
            """;

        var ids = GarageCliParser.ParseListIds(output);

        Assert.Equal(["GK101dd383fa8d8dd14f780cb3"], ids);
    }

    [Fact]
    public void ParseListIds_NoDataRows_ReturnsEmpty()
    {
        const string output = "ID  Created  Global aliases  Local aliases";

        Assert.Empty(GarageCliParser.ParseListIds(output));
    }

    [Fact]
    public void ParseBucketInfo_EmptyBucket_NoGrantsYet()
    {
        const string output = """
            ==== BUCKET INFORMATION ====
            Bucket:          3396db51a7484ee6c96f5d8c1da67646ccb7d1e7c4d9864631dedf72b95d644c
            Created:         2026-07-21 00:24:27.463 +00:00

            Size:            0 B (0 B)
            Objects:         0

            Website access:  false

            Global alias:    probe-test-bucket

            ==== KEYS FOR THIS BUCKET ====
            Permissions  Access key    Local aliases
            """;

        var bucket = GarageCliParser.ParseBucketInfo(output);

        Assert.Equal("3396db51a7484ee6c96f5d8c1da67646ccb7d1e7c4d9864631dedf72b95d644c", bucket.Id);
        Assert.Equal("probe-test-bucket", bucket.Name);
        Assert.Equal("2026-07-21 00:24:27.463 +00:00", bucket.CreatedAt);
        Assert.Equal(0, bucket.SizeBytes);
        Assert.Equal(0, bucket.ObjectCount);
        Assert.False(bucket.WebsiteEnabled);
        Assert.Empty(bucket.Keys);
    }

    [Fact]
    public void ParseBucketInfo_WithAGrant_ParsesPermissionCodeAndKeyIdentity()
    {
        // Same report `bucket create`/`bucket allow`/`bucket deny` print, not just `bucket info` --
        // one parser covers all four, this is the "after granting RWO to a key" shape.
        const string output = """
            ==== BUCKET INFORMATION ====
            Bucket:          3396db51a7484ee6c96f5d8c1da67646ccb7d1e7c4d9864631dedf72b95d644c
            Created:         2026-07-21 00:24:27.463 +00:00

            Size:            0 B (0 B)
            Objects:         0

            Website access:  false

            Global alias:    probe-test-bucket

            ==== KEYS FOR THIS BUCKET ====
            Permissions  Access key                               Local aliases
            RWO          GK101dd383fa8d8dd14f780cb3  s3-01-admin
            """;

        var bucket = GarageCliParser.ParseBucketInfo(output);

        var grant = Assert.Single(bucket.Keys);
        Assert.Equal("GK101dd383fa8d8dd14f780cb3", grant.KeyId);
        Assert.Equal("s3-01-admin", grant.KeyName);
        Assert.True(grant.Read);
        Assert.True(grant.Write);
        Assert.True(grant.Owner);
    }

    [Fact]
    public void ParseKeyInfo_FreshlyCreated_CapturesTheOneTimeSecret()
    {
        const string output = """
            ==== ACCESS KEY INFORMATION ====
            Key ID:              GK101dd383fa8d8dd14f780cb3
            Key name:            s3-01-admin
            Secret key:          f6636233c5eb19fc46b71e0438c13bb959eeaa7e11feaec3571edef1b6e9b8f3
            Created:             2026-07-20 20:07:39.443 +00:00
            Validity:            valid
            Expiration:          never

            Can create buckets:  false

            ==== BUCKETS FOR THIS KEY ====
            Permissions  ID  Global aliases  Local aliases
            """;

        var key = GarageCliParser.ParseKeyInfo(output);

        Assert.Equal("GK101dd383fa8d8dd14f780cb3", key.Id);
        Assert.Equal("s3-01-admin", key.Name);
        Assert.Equal("f6636233c5eb19fc46b71e0438c13bb959eeaa7e11feaec3571edef1b6e9b8f3", key.SecretKey);
        Assert.True(key.Valid);
        Assert.Null(key.Expiration);
        Assert.False(key.CanCreateBuckets);
        Assert.Empty(key.Buckets);
    }

    [Fact]
    public void ParseKeyInfo_Redacted_NeverSurfacesTheLiteralPlaceholderAsASecret()
    {
        // Regression test: every call to `garage key info` after the key's own creation prints
        // "(redacted)" instead of the real secret -- a caller mistaking that literal string for a
        // real secret would silently use "(redacted)" as if it were valid credentials.
        const string output = """
            ==== ACCESS KEY INFORMATION ====
            Key ID:              GK101dd383fa8d8dd14f780cb3
            Key name:            s3-01-admin
            Secret key:          (redacted)
            Created:             2026-07-20 20:07:39.443 +00:00
            Validity:            valid
            Expiration:          never

            Can create buckets:  true

            ==== BUCKETS FOR THIS KEY ====
            Permissions  ID                Global aliases     Local aliases
            RWO          3396db51a7484ee6  probe-test-bucket
            """;

        var key = GarageCliParser.ParseKeyInfo(output);

        Assert.Null(key.SecretKey);
        Assert.True(key.CanCreateBuckets);
        var grant = Assert.Single(key.Buckets);
        Assert.Equal("3396db51a7484ee6", grant.BucketId);
        Assert.Equal("probe-test-bucket", grant.BucketName);
        Assert.True(grant.Read);
        Assert.True(grant.Write);
        Assert.True(grant.Owner);
    }

    [Fact]
    public void ParseSizeBytes_UsesTheExactParenthesizedByteCount_NotTheHumanReadablePrefix()
    {
        const string output = """
            ==== BUCKET INFORMATION ====
            Bucket:          abc123
            Created:         2026-07-21 00:24:27.463 +00:00

            Size:            1.5 GiB (1610612736 B)
            Objects:         42

            Website access:  false

            ==== KEYS FOR THIS BUCKET ====
            Permissions  Access key  Local aliases
            """;

        var bucket = GarageCliParser.ParseBucketInfo(output);

        Assert.Equal(1610612736, bucket.SizeBytes);
        Assert.Equal(42, bucket.ObjectCount);
    }
}
