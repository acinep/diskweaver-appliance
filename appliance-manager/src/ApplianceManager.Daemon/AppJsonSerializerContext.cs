using System.Text.Json.Serialization;
using ApplianceManager.Garage;

namespace ApplianceManager.Daemon;

/// <summary>
/// Source-generated JSON type info for everything the daemon serializes/deserializes --
/// required for reflection-free (Native AOT) serialization. See Program.cs wiring.
/// </summary>
[JsonSerializable(typeof(IReadOnlyList<GarageBucket>))]
[JsonSerializable(typeof(GarageBucket))]
[JsonSerializable(typeof(IReadOnlyList<GarageKey>))]
[JsonSerializable(typeof(GarageKey))]
[JsonSerializable(typeof(CreateBucketRequest))]
[JsonSerializable(typeof(CreateKeyRequest))]
[JsonSerializable(typeof(SetCanCreateBucketsRequest))]
[JsonSerializable(typeof(BucketGrantRequest))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
