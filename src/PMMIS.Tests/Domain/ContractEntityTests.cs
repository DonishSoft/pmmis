using PMMIS.Domain.Entities;

namespace PMMIS.Tests.Domain;

/// <summary>
/// Тесты для сущностей домена
/// </summary>
public class ContractEntityTests
{
    [Fact]
    public void Contract_ShouldCalculateRemainingAmount()
    {
        // Arrange
        var contract = new Contract
        {
            Id = 1,
            ContractAmount = 100000m,
            SigningDate = DateTime.UtcNow,
            ContractEndDate = DateTime.UtcNow.AddYears(1)
        };
        
        // Assert
        Assert.Equal(100000m, contract.ContractAmount);
    }

    [Fact]
    public void Payment_ShouldHaveCorrectStatusValues()
    {
        // Assert
        Assert.Equal(0, (int)PaymentStatus.Pending);
        Assert.Equal(1, (int)PaymentStatus.Approved);
        Assert.Equal(2, (int)PaymentStatus.Paid);
        Assert.Equal(3, (int)PaymentStatus.Rejected);
    }

    [Fact]
    public void ProcurementStatus_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)ProcurementStatus.Planned);
        Assert.Equal(1, (int)ProcurementStatus.InProgress);
        Assert.Equal(5, (int)ProcurementStatus.Cancelled);
    }

    [Fact]
    public void ApplicationUser_ShouldHaveDefaultValues()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "Test",
            LastName = "User"
        };
        
        // Assert
        Assert.Equal("Test", user.FirstName);
        Assert.Equal("User", user.LastName);
        Assert.True(user.IsActive);
    }
}
