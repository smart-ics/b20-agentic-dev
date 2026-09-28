namespace Cakra.Modules.WorkPackage.Domain.Exceptions;

/// <summary>
/// Exception thrown when an explicit domain business rule is violated in the Work Package module.
/// </summary>
public sealed class BusinessRuleViolationException : WorkPackageDomainException
{
    public int RuleNumber { get; }

    public BusinessRuleViolationException(int ruleNumber, string message)
        : base($"Business Rule {ruleNumber} violation: {message}")
    {
        RuleNumber = ruleNumber;
    }

    public BusinessRuleViolationException(int ruleNumber, string message, Exception innerException)
        : base($"Business Rule {ruleNumber} violation: {message}", innerException)
    {
        RuleNumber = ruleNumber;
    }
}
