using System.ComponentModel.DataAnnotations;

namespace OpenStickyMemos.Api.DTOs;

// ── Request ──

public class CreateInvitationRequest
{
    /// <summary>Email opcional al que restringir la invitación.</summary>
    [EmailAddress, MaxLength(256)]
    public string? InvitedEmail { get; set; }

    /// <summary>Días de validez de la invitación (default: 7).</summary>
    [Range(1, 365)]
    public int ExpiresInDays { get; set; } = 7;
}

// ── Response ──

public class InvitationResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? InvitedEmail { get; set; }
    public string Token { get; set; } = string.Empty;
    public string InvitationLink { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

/// <summary>
/// DTO público para que cualquiera vea info básica de una invitación (sin auth).
/// </summary>
public class InvitationPublicResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectDescription { get; set; }
    public string? InvitedEmail { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public bool IsAccepted { get; set; }
}
