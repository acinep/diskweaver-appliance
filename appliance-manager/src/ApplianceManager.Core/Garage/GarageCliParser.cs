using System.Text.RegularExpressions;

namespace ApplianceManager.Garage;

/// <summary>
/// Pure parsing of `garage` CLI text output -- the CLI has no JSON output mode (checked against
/// `garage --help`/`garage bucket --help`/`garage key --help` on a real v2.3.0 install; only
/// `--admin-token`-style flags exist, nothing for output format), so every command's plain-text
/// report has to be scraped. Every format parsed here was captured from real `garage` v2.3.0
/// output, not guessed from docs -- see docs/garage.md's captured samples this was built against.
/// </summary>
public static partial class GarageCliParser
{
    /// <summary>
    /// `garage bucket list`/`garage key list` share one shape: a header row starting with "ID",
    /// then one row per item with the id as the first whitespace-delimited token. Only the id is
    /// needed from either listing -- <see cref="ParseBucketInfo"/>/<see cref="ParseKeyInfo"/> (via
    /// a follow-up `info` call per id) carry every other field.
    /// </summary>
    public static IReadOnlyList<string> ParseListIds(string output)
    {
        var ids = new List<string>();
        foreach (var line in SplitLines(output))
        {
            if (line.Length == 0 || line.StartsWith("ID", StringComparison.Ordinal))
            {
                continue;
            }

            var id = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (id is not null)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Parses `garage bucket info`/`garage bucket create`/`garage bucket allow`/`garage bucket
    /// deny` output -- all four print the identical "==== BUCKET INFORMATION ====" report, so one
    /// parser covers all of them.
    /// </summary>
    public static GarageBucket ParseBucketInfo(string output)
    {
        var lines = SplitLines(output);
        var fields = ParseFieldBlock(lines, "==== KEYS FOR THIS BUCKET ====");

        var id = fields.GetValueOrDefault("Bucket") ?? throw new FormatException("garage bucket info: missing 'Bucket:' field.");
        var name = fields.GetValueOrDefault("Global alias") ?? fields.GetValueOrDefault("Global aliases");
        var sizeBytes = ParseSizeBytes(fields.GetValueOrDefault("Size"));
        var objectCount = long.Parse(fields.GetValueOrDefault("Objects") ?? "0");
        var websiteEnabled = string.Equals(fields.GetValueOrDefault("Website access"), "true", StringComparison.OrdinalIgnoreCase);

        var keys = ParseGrantTable(lines, "==== KEYS FOR THIS BUCKET ====")
            .Select(row => new GarageBucketKeyGrant(row.Id, row.Name, row.Read, row.Write, row.Owner))
            .ToList();

        return new GarageBucket(id, name, fields.GetValueOrDefault("Created") ?? "", sizeBytes, objectCount, websiteEnabled, keys);
    }

    /// <summary>
    /// Parses `garage key info`/`garage key create`/`garage key allow`/`garage key deny` output --
    /// all four print the identical "==== ACCESS KEY INFORMATION ====" report.
    /// </summary>
    public static GarageKey ParseKeyInfo(string output)
    {
        var lines = SplitLines(output);
        var fields = ParseFieldBlock(lines, "==== BUCKETS FOR THIS KEY ====");

        var id = fields.GetValueOrDefault("Key ID") ?? throw new FormatException("garage key info: missing 'Key ID:' field.");
        var name = fields.GetValueOrDefault("Key name") ?? "";
        // "(redacted)" is what every call after the key's own creation prints -- never a real
        // secret, so it's normalized to null rather than passed through as a literal string that
        // could be mistaken for one.
        var secretText = fields.GetValueOrDefault("Secret key");
        var secretKey = secretText is null or "(redacted)" ? null : secretText;
        var valid = string.Equals(fields.GetValueOrDefault("Validity"), "valid", StringComparison.OrdinalIgnoreCase);
        var expirationText = fields.GetValueOrDefault("Expiration");
        var expiration = expirationText is null or "never" ? null : expirationText;
        var canCreateBuckets = string.Equals(fields.GetValueOrDefault("Can create buckets"), "true", StringComparison.OrdinalIgnoreCase);

        var buckets = ParseGrantTable(lines, "==== BUCKETS FOR THIS KEY ====")
            .Select(row => new GarageKeyBucketGrant(row.Id, row.Name, row.Read, row.Write, row.Owner))
            .ToList();

        return new GarageKey(id, name, secretKey, fields.GetValueOrDefault("Created") ?? "", valid, expiration, canCreateBuckets, buckets);
    }

    /// <summary>
    /// Everything before <paramref name="tableHeader"/> is "Label:   value" lines (blank lines and
    /// the leading "==== ... ====" banner are skipped) -- captured real output always has exactly
    /// one colon-delimited label per line, so splitting on the first colon and trimming both sides
    /// is enough; no line's value itself contains a colon in any sample this was built against.
    /// </summary>
    private static Dictionary<string, string> ParseFieldBlock(IReadOnlyList<string> lines, string tableHeader)
    {
        var fields = new Dictionary<string, string>();
        foreach (var line in lines)
        {
            if (line == tableHeader)
            {
                break;
            }

            if (line.Length == 0 || line.StartsWith("====", StringComparison.Ordinal))
            {
                continue;
            }

            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
            {
                continue;
            }

            fields[line[..colonIndex].Trim()] = line[(colonIndex + 1)..].Trim();
        }

        return fields;
    }

    /// <summary>
    /// The "==== KEYS FOR THIS BUCKET ====" / "==== BUCKETS FOR THIS KEY ====" tables share one
    /// shape: a "Permissions  &lt;Id column&gt;  &lt;Name/aliases column&gt;" header, then one row
    /// per grant. Columns are separated by 2+ spaces (a single space inside "RWO" is never emitted,
    /// and every real id/name captured is itself space-free) -- splitting on runs of 2+ spaces
    /// reliably separates the permission code, id, and name even though the id and name sit under
    /// one wide "Access key"/"ID" header cell together. An empty table (just the header, no rows)
    /// -- e.g. a brand-new bucket/key with no grants yet -- yields no rows, not an error.
    /// </summary>
    private static IEnumerable<(string Id, string? Name, bool Read, bool Write, bool Owner)> ParseGrantTable(
        IReadOnlyList<string> lines, string tableHeader)
    {
        var headerIndex = lines.ToList().IndexOf(tableHeader);
        if (headerIndex < 0)
        {
            yield break;
        }

        // headerIndex + 1 is the table's own column-header row ("Permissions  ...") -- skip it too.
        for (var i = headerIndex + 2; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            var columns = MultiSpaceRegex().Split(line).Where(c => c.Length > 0).ToArray();
            if (columns.Length < 2)
            {
                continue;
            }

            var permissions = columns[0];
            yield return (
                Id: columns[1],
                Name: columns.Length > 2 ? columns[2] : null,
                Read: permissions.Contains('R'),
                Write: permissions.Contains('W'),
                Owner: permissions.Contains('O'));
        }
    }

    /// <summary>
    /// "Size:" prints as "&lt;human-readable&gt; (&lt;exact bytes&gt; B)", e.g. "0 B (0 B)" or "1.5
    /// GiB (1610612736 B)" -- the parenthesized figure is always the exact byte count, so that's
    /// what's extracted rather than trying to parse the human-readable prefix's unit.
    /// </summary>
    private static long ParseSizeBytes(string? sizeField)
    {
        if (sizeField is null)
        {
            return 0;
        }

        var match = ExactBytesRegex().Match(sizeField);
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }

    private static IReadOnlyList<string> SplitLines(string output) =>
        output.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"\((\d+) B\)")]
    private static partial Regex ExactBytesRegex();
}
