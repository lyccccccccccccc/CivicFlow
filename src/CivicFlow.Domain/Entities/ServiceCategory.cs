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

        ValidateSla(firstResponseHours, resolutionHours);

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

    public void Update(string name, string description, int firstResponseHours, int resolutionHours, DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required.", nameof(name));
        ValidateSla(firstResponseHours, resolutionHours);
        Name = name.Trim();
        Description = description.Trim();
        FirstResponseHours = firstResponseHours;
        ResolutionHours = resolutionHours;
        MarkUpdated(updatedAtUtc);
    }

    public void SetActive(bool isActive, DateTimeOffset updatedAtUtc)
    {
        IsActive = isActive;
        MarkUpdated(updatedAtUtc);
    }

    private static void ValidateSla(int firstResponseHours, int resolutionHours)
    {
        if (firstResponseHours <= 0 || resolutionHours <= firstResponseHours)
            throw new ArgumentOutOfRangeException(nameof(firstResponseHours),
                "Resolution SLA must be greater than the positive first-response SLA.");
    }
}
