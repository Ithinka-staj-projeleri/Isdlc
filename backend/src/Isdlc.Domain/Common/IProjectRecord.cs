namespace Isdlc.Domain.Common;

/// <summary>
/// A record that belongs to a project (Risk, Requirement, OpenIssue, ...).
/// Its audit log rows carry the ProjectId so they can be listed per project.
/// </summary>
public interface IProjectRecord
{
    int ProjectId { get; }
}
