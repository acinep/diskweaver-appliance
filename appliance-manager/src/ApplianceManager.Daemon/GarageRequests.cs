namespace ApplianceManager.Daemon;

/// <summary>Request bodies for the <c>/garage/*</c> endpoints. See docs/daemon-api.md.</summary>
public sealed record CreateBucketRequest(string Name);

public sealed record CreateKeyRequest(string Name);

public sealed record SetCanCreateBucketsRequest(bool Allow);

public sealed record BucketGrantRequest(string KeyId, bool Read, bool Write, bool Owner);
