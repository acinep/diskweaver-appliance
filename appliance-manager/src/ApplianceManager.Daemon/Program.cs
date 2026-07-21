using ApplianceManager.Daemon;
using ApplianceManager.Executor;
using ApplianceManager.Garage;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ICommandRunner>(_ => new ProcessCommandRunner());
builder.Services.AddSingleton<IGarageClient>(sp => new GarageCliClient(sp.GetRequiredService<ICommandRunner>()));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

// Production (Cockpit) transport is a Unix socket; set APPLIANCEMANAGER_SOCKET to opt into it.
// No separate auth layer here -- unlike DiskWeaver.Daemon (which also serves a standalone SPA over
// TCP and so needs its own PAM/cookie login), this daemon is Cockpit-only for now: Cockpit already
// authenticates the user's session before cockpit-bridge ever proxies onto this socket, and the
// socket file's own Group=appliance-manager/UMask=0007 (see packaging/appliance-managerd.service)
// is what gates that path, the same trust boundary DiskWeaver's own socket relies on.
var socketPath = Environment.GetEnvironmentVariable("APPLIANCEMANAGER_SOCKET");
if (socketPath is not null)
{
    builder.WebHost.ConfigureKestrel(options => options.ListenUnixSocket(socketPath));
}

// Local dev convenience: without APPLIANCEMANAGER_SOCKET, ASP.NET Core's normal configuration
// applies (ASPNETCORE_URLS etc.) -- much easier to `curl` during development than a socket file.

var app = builder.Build();

// --- Garage buckets/keys --------------------------------------------------
// See docs/garage.md: no JSON output mode on the `garage` CLI, so GarageCliClient shells out and
// scrapes text. Read endpoints (GET) map a failure to 500 (Garage itself being unreachable is an
// environment problem, not a client mistake); write endpoints (POST/DELETE) map to 400, since a
// `garage` CLI failure here is virtually always bad input (invalid/duplicate bucket name, unknown
// key id) rather than something wrong with the box.
app.MapGet("/garage/buckets", (IGarageClient garage) =>
{
    try
    {
        return Results.Ok(garage.GetBuckets());
    }
    catch (InvalidOperationException ex)
    {
        return TextError(StatusCodes.Status500InternalServerError, ex.Message);
    }
});

app.MapPost("/garage/buckets", (CreateBucketRequest request, IGarageClient garage) =>
{
    try
    {
        return Results.Ok(garage.CreateBucket(request.Name));
    }
    catch (InvalidOperationException ex)
    {
        return TextError(StatusCodes.Status400BadRequest, ex.Message);
    }
});

app.MapDelete("/garage/buckets/{name}", (string name, IGarageClient garage) =>
{
    try
    {
        garage.DeleteBucket(name);
        return Results.Ok();
    }
    catch (InvalidOperationException ex)
    {
        return TextError(StatusCodes.Status400BadRequest, ex.Message);
    }
});

app.MapPost("/garage/buckets/{name}/grants", (string name, BucketGrantRequest request, IGarageClient garage) =>
{
    try
    {
        return Results.Ok(garage.SetBucketPermission(name, request.KeyId, request.Read, request.Write, request.Owner));
    }
    catch (InvalidOperationException ex)
    {
        return TextError(StatusCodes.Status400BadRequest, ex.Message);
    }
});

app.MapDelete("/garage/buckets/{name}/grants/{keyId}", (string name, string keyId, IGarageClient garage) =>
{
    try
    {
        // Revokes every permission this key has on the bucket -- there's no partial-revoke UI
        // action (see ObjectStorage.jsx), so this always clears all three flags at once rather
        // than needing read/write/owner passed in just to say "all of them".
        return Results.Ok(garage.RevokeBucketPermission(name, keyId, read: true, write: true, owner: true));
    }
    catch (InvalidOperationException ex)
    {
        return TextError(StatusCodes.Status400BadRequest, ex.Message);
    }
});

app.MapGet("/garage/keys", (IGarageClient garage) =>
{
    try
    {
        return Results.Ok(garage.GetKeys());
    }
    catch (InvalidOperationException ex)
    {
        return TextError(StatusCodes.Status500InternalServerError, ex.Message);
    }
});

app.MapPost("/garage/keys", (CreateKeyRequest request, IGarageClient garage) =>
{
    try
    {
        // The only response that ever carries a real (non-redacted) GarageKey.SecretKey -- see its
        // doc comment. Cockpit must show/copy it right here; there is no way to recover it later.
        return Results.Ok(garage.CreateKey(request.Name));
    }
    catch (InvalidOperationException ex)
    {
        return TextError(StatusCodes.Status400BadRequest, ex.Message);
    }
});

app.MapDelete("/garage/keys/{id}", (string id, IGarageClient garage) =>
{
    try
    {
        garage.DeleteKey(id);
        return Results.Ok();
    }
    catch (InvalidOperationException ex)
    {
        return TextError(StatusCodes.Status400BadRequest, ex.Message);
    }
});

app.MapPost("/garage/keys/{id}/can-create-buckets", (string id, SetCanCreateBucketsRequest request, IGarageClient garage) =>
{
    try
    {
        return Results.Ok(garage.SetCanCreateBuckets(id, request.Allow));
    }
    catch (InvalidOperationException ex)
    {
        return TextError(StatusCodes.Status400BadRequest, ex.Message);
    }
});

app.Run();

static IResult TextError(int statusCode, string message) => Results.Text(message, "text/plain", statusCode: statusCode);

// Exposes Program for WebApplicationFactory<Program> in ApplianceManager.Daemon.Tests.
public partial class Program;
