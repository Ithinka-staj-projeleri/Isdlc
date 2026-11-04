namespace Isdlc.Domain.Common;

/// <summary>
/// Changes to this entity are written to AuditLogs automatically.
/// Only for AuditableEntity types that hold project data (Project, Risk, Requirement, ...).
/// </summary>
public interface IAudited;
