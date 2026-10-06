using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;

namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Minimal backward compatibility adapters for callers of removed Request aggregate methods
/// during CR-016 transition until dependent services and test suites are refactored in P3 and P6.
/// </summary>
public static class RequestLegacyCompatibilityExtensions
{
    [Obsolete("Evaluating state is deprecated in CR-016.")]
    public static void Evaluate(
        this Request request,
        string evaluationNotes,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        // No-op for legacy callers
    }

    [Obsolete("Accepted state is deprecated in CR-016. Use StartWork directly.")]
    public static void Accept(
        this Request request,
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null)
    {
        // No-op or starting work directly
    }

    [Obsolete("Accepted state is deprecated in CR-016. Use StartWork directly.")]
    public static void AcceptResponsibility(
        this Request request,
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null)
    {
    }

    [Obsolete("Reject is replaced by Cancel in CR-016.")]
    public static void Reject(
        this Request request,
        string reason,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        request.Cancel(reason, actorPersonId, utcNow);
    }

    [Obsolete("Escalate is replaced by PauseWork in CR-016.")]
    public static void Escalate(
        this Request request,
        string reason,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        request.PauseWork(actorPersonId, reason, utcNow);
    }

    [Obsolete("StartProgress is replaced by StartWork in CR-016.")]
    public static void StartProgress(
        this Request request,
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null)
    {
        request.StartWork(actorPersonId, notes, utcNow);
    }

    [Obsolete("ResumeProgress is replaced by StartWork in CR-016.")]
    public static void ResumeProgress(
        this Request request,
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null)
    {
        request.StartWork(actorPersonId, notes, utcNow);
    }

    [Obsolete("Management decisions are deprecated in CR-016.")]
    public static void RequestManagementDecision(
        this Request request,
        string decisionDetails,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
    }

    [Obsolete("Management decisions are deprecated in CR-016.")]
    public static void ApplyManagementDecision(
        this Request request,
        RequestStatus targetStatus,
        string decisionNotes,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        if (targetStatus == RequestStatus.InProgress)
        {
            if (request.OwnerPersonId.HasValue)
            {
                request.StartWork(request.OwnerPersonId.Value, decisionNotes, utcNow);
            }
        }
    }
}
