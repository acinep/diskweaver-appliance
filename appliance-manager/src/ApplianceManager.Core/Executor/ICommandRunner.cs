namespace ApplianceManager.Executor;

/// <summary>
/// Runs a command and returns its stdout. Exists so state-collecting code (e.g.
/// <see cref="ApplianceManager.Garage.GarageCliClient"/>) can be tested without real subprocesses.
/// Deliberately duplicated from DiskWeaver.Core's identical abstraction rather than shared via a
/// package reference -- the two solutions live in separate repos with no shared package feed, and
/// this interface is small enough (one method) that keeping it in sync by hand costs less than the
/// cross-repo dependency would.
/// </summary>
public interface ICommandRunner
{
    string Run(string command, IReadOnlyList<string> arguments);
}
