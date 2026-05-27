using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class LeaseApplicationDocumentRulesTests
{
    [Fact]
    public void RegularApplication_RequiresNationalIdAndSalary()
    {
        var required = LeaseApplicationDocumentRules.GetRequiredDocumentTypes(parentLeaseId: null);
        Assert.Contains(LeaseApplicationDocumentRules.NationalId, required);
        Assert.Contains(LeaseApplicationDocumentRules.SalaryIncome, required);
    }

    [Fact]
    public void RenewalApplication_RequiresNoDocuments() =>
        Assert.Empty(LeaseApplicationDocumentRules.GetRequiredDocumentTypes(parentLeaseId: 42));

    [Fact]
    public void HasAllRequiredDocuments_WhenBothSubmitted_ReturnsTrue()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted)
        };
        Assert.True(LeaseApplicationDocumentRules.HasAllRequiredDocuments(null, docs));
    }

    [Fact]
    public void TenantCanReUpload_WhenScreeningAndSalaryRejected_ReturnsTrue()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusRejected)
        };
        Assert.True(LeaseApplicationDocumentRules.TenantCanReUploadDocuments(
            LeaseApplicationDocumentRules.ApplicationStatusScreening, docs));
    }

    [Fact]
    public void BuildStoredFileName_UsesPdfExtensionAndSanitizedName()
    {
        var name = LeaseApplicationDocumentRules.BuildStoredFileName(
            12, 3, "Sara Al-Khalifa", LeaseApplicationDocumentRules.NationalId);
        Assert.EndsWith(".pdf", name);
        Assert.Contains("12_3_", name);
        Assert.Contains("NationalId", name);
    }
}
