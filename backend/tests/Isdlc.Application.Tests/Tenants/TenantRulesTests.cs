using Isdlc.Infrastructure.Persistence.Interceptors;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>The write rules of TenantInterceptor, without a database. Expectations: docs/multi-tenancy.md → Yazma kuralı.</summary>
public class TenantRulesTests
{
    [Fact]
    public void ForNewRecord_NoCompanyOnTheRecord_GetsTheCurrentOne()
    {
        Assert.Equal(2, TenantRules.ForNewRecord("Risk", 0, 2));
    }

    [Fact]
    public void ForNewRecord_SameCompany_IsKept()
    {
        Assert.Equal(2, TenantRules.ForNewRecord("Risk", 2, 2));
    }

    [Fact]
    public void ForNewRecord_AnotherCompany_IsRefused()
    {
        var error = Assert.Throws<InvalidOperationException>(() => TenantRules.ForNewRecord("Risk", 1, 2));

        Assert.Contains("company 1", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ForNewRecord_NoCurrentCompany_IsRefused(int tenantId)
    {
        Assert.Throws<InvalidOperationException>(() => TenantRules.ForNewRecord("Risk", tenantId, null));
    }

    [Fact]
    public void ForChangedRecord_SameCompany_IsFine()
    {
        TenantRules.ForChangedRecord("Risk", 1, 1);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(1, null)]
    public void ForChangedRecord_CompanyChanged_IsRefused(int? original, int? current)
    {
        Assert.Throws<InvalidOperationException>(() => TenantRules.ForChangedRecord("User", original, current));
    }
}
