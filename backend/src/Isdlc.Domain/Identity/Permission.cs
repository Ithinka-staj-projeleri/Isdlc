namespace Isdlc.Domain.Identity;

/// <summary>
/// Yetki (Faz 7 · A2): doing a kind of work in the flow. A fixed, short list, the same in every company; a user can have
/// any number of them. Work outside the flow (risks, open issues, minutes…) needs none.
/// Contract: docs/configurability.md → Yetkiler.
/// </summary>
public enum Permission
{
    /// <summary>Analysis steps; opening a request; being a project's business analyst.</summary>
    Analysis,

    /// <summary>Project management steps; being a project's project manager.</summary>
    ProjectManagement,

    /// <summary>Approval steps; rejecting, postponing and taking back a request.</summary>
    Approval,

    /// <summary>Offer and agreement steps.</summary>
    Sales,

    Development,

    Testing,
}
