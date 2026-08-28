using CivicFlow.Domain.Common;

namespace CivicFlow.Domain.Entities;

public sealed class ServiceCategory : BaseEntity
{
    private ServiceCategory()
    {
    }

    public ServiceCategory(
        string name,
        string description,
        int firstResponseHours,
        int resolutionHours,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        if (firstResponseHours <= 0 || resolutionHours <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstResponseHours),
                "SLA targets must be greater than zero.");
        }

        Name = name.Trim();
        Description = description.Trim();
        FirstResponseHours = firstResponseHours;
        ResolutionHours = resolutionHours;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int FirstResponseHours { get; private set; }

    public int ResolutionHours { get; private set; }

    public bool IsActive { get; private set; }
}
