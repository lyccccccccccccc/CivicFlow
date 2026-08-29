using CivicFlow.Domain.Entities;

namespace CivicFlow.UnitTests.Domain;

public sealed class ServiceCategoryTests
{
    [Fact]
    public void UpdateAndDisable_PreservesCategoryIdentity()
    {
        var now = DateTimeOffset.UtcNow;
        var category = new ServiceCategory("Roads", "Road requests", 8, 72, now);
        var id = category.Id;

        category.Update("Road network", "Road and footpath requests", 4, 48, now.AddMinutes(1));
        category.SetActive(false, now.AddMinutes(2));

        Assert.Equal(id, category.Id);
        Assert.Equal("Road network", category.Name);
        Assert.Equal(4, category.FirstResponseHours);
        Assert.Equal(48, category.ResolutionHours);
        Assert.False(category.IsActive);
    }

    [Fact]
    public void Constructor_RejectsResolutionSlaBeforeFirstResponseSla()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ServiceCategory("Invalid", "Invalid targets", 8, 4, DateTimeOffset.UtcNow));
    }
}
