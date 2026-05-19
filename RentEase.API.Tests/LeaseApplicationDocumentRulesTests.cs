using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class LeaseApplicationDocumentRulesTests
{
    [Fact]
    public void GetRequiredDocumentTypes_RegularApplication_ReturnsIdAndSalary()
    {
        var required = LeaseApplicationDocumentRules.GetRequiredDocumentTypes(parentLeaseId: null);

        Assert.Equal(2, required.Count);
        Assert.Contains(LeaseApplicationDocumentRules.NationalId, required);
        Assert.Contains(LeaseApplicationDocumentRules.SalaryIncome, required);
    }

    [Fact]
    public void GetRequiredDocumentTypes_Renewal_ReturnsEmpty()
    {
        var required = LeaseApplicationDocumentRules.GetRequiredDocumentTypes(parentLeaseId: 42);
        Assert.Empty(required);
    }

    [Fact]
    public void BuildStoredFileName_FollowsConvention()
    {
        var name = LeaseApplicationDocumentRules.BuildStoredFileName(
            12, 34, "Sara Al-Khalifa", LeaseApplicationDocumentRules.NationalId);

        Assert.Equal("12_34_Sara_Al_Khalifa_NationalId.pdf", name);
    }

    [Fact]
    public void SanitizeApplicantName_RemovesInvalidCharacters()
    {
        var sanitized = LeaseApplicationDocumentRules.SanitizeApplicantName("John/Doe (Jr.)");
        Assert.DoesNotContain("/", sanitized);
        Assert.DoesNotContain("(", sanitized);
    }

    [Fact]
    public void HasAllRequiredDocuments_IgnoresRejectedDocuments()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusRejected),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted)
        };

        Assert.False(LeaseApplicationDocumentRules.HasAllRequiredDocuments(null, docs));
    }

    [Fact]
    public void HasAllRequiredDocuments_WhenBothActive_ReturnsTrue()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted)
        };

        Assert.True(LeaseApplicationDocumentRules.HasAllRequiredDocuments(null, docs));
    }

    [Fact]
    public void GetRejectedDocumentTypes_ReturnsRejectedOnly()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusRejected),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted)
        };

        var rejected = LeaseApplicationDocumentRules.GetRejectedDocumentTypes(docs);
        Assert.Single(rejected);
        Assert.Equal(LeaseApplicationDocumentRules.NationalId, rejected[0]);
    }

    [Theory]
    [InlineData("id.pdf", true)]
    [InlineData("scan.PDF", true)]
    [InlineData("pay.png", false)]
    [InlineData("virus.exe", false)]
    [InlineData("", false)]
    public void IsAllowedExtension_OnlyPdf(string fileName, bool expected)
    {
        Assert.Equal(expected, LeaseApplicationDocumentRules.IsAllowedExtension(fileName));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10 * 1024 * 1024, true)]
    [InlineData(0, false)]
    [InlineData(10 * 1024 * 1024 + 1, false)]
    public void IsAllowedFileSize_ValidatesSize(long length, bool expected)
    {
        Assert.Equal(expected, LeaseApplicationDocumentRules.IsAllowedFileSize(length));
    }

    [Fact]
    public void ValidateRegularApplicationUploads_WhenBothMissing_ReturnsTwoErrors()
    {
        var errors = LeaseApplicationDocumentRules.ValidateRegularApplicationUploads(
            hasNationalIdFile: false,
            hasSalaryIncomeFile: false,
            nationalIdFileName: null,
            nationalIdLength: 0,
            salaryIncomeFileName: null,
            salaryIncomeLength: 0);

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void ValidateRegularApplicationUploads_WhenNotPdf_ReturnsPdfError()
    {
        var errors = LeaseApplicationDocumentRules.ValidateRegularApplicationUploads(
            hasNationalIdFile: true,
            hasSalaryIncomeFile: true,
            nationalIdFileName: "id.jpg",
            nationalIdLength: 100,
            salaryIncomeFileName: "pay.pdf",
            salaryIncomeLength: 100);

        Assert.Contains(errors, e => e.Contains("PDF", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRegularApplicationUploads_WhenValidPdf_ReturnsNoErrors()
    {
        var errors = LeaseApplicationDocumentRules.ValidateRegularApplicationUploads(
            hasNationalIdFile: true,
            hasSalaryIncomeFile: true,
            nationalIdFileName: "cpr.pdf",
            nationalIdLength: 2048,
            salaryIncomeFileName: "salary.pdf",
            salaryIncomeLength: 4096);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSinglePdfUpload_WhenMissing_ReturnsError()
    {
        var errors = LeaseApplicationDocumentRules.ValidateSinglePdfUpload(
            false, null, 0, "National ID");

        Assert.Single(errors);
    }

    [Theory]
    [InlineData(LeaseApplicationDocumentRules.NationalId, "National ID")]
    [InlineData(LeaseApplicationDocumentRules.SalaryIncome, "Salary / Income Proof")]
    public void GetDisplayName_ReturnsFriendlyLabel(string type, string expected)
    {
        Assert.Equal(expected, LeaseApplicationDocumentRules.GetDisplayName(type));
    }

    [Theory]
    [InlineData("Pending", false)]
    [InlineData("Screening", true)]
    [InlineData("Approved", true)]
    [InlineData("Rejected", true)]
    [InlineData("Canceled", true)]
    [InlineData("DocumentsRequired", true)]
    [InlineData("screening", true)]
    public void ManagerCanViewDocuments_OnlyAfterScreeningOrLater(string status, bool expected)
    {
        Assert.Equal(expected, LeaseApplicationDocumentRules.ManagerCanViewDocuments(status));
    }

    [Theory]
    [InlineData("Screening", LeaseApplicationDocumentRules.DocumentStatusRejected, true)]
    [InlineData("Screening", LeaseApplicationDocumentRules.DocumentStatusSubmitted, false)]
    [InlineData("Pending", LeaseApplicationDocumentRules.DocumentStatusRejected, false)]
    [InlineData("DocumentsRequired", LeaseApplicationDocumentRules.DocumentStatusRejected, true)]
    public void TenantCanReUploadDocuments_OnlyScreeningWithRejected(
        string appStatus, string docStatus, bool expected)
    {
        var docs = new[] { (LeaseApplicationDocumentRules.NationalId, docStatus) };
        Assert.Equal(expected,
            LeaseApplicationDocumentRules.TenantCanReUploadDocuments(appStatus, docs));
    }

    [Fact]
    public void TenantCanReUploadDocuments_WhenBothTypesRejected_ReturnsTrue()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusRejected),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusRejected)
        };

        Assert.True(LeaseApplicationDocumentRules.TenantCanReUploadDocuments(
            LeaseApplicationDocumentRules.ApplicationStatusScreening, docs));
    }

    [Fact]
    public void HasRejectableDocument_AllowsRejectingEachTypeIndependently()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusRejected),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted)
        };

        Assert.False(LeaseApplicationDocumentRules.HasRejectableDocument(
            docs, LeaseApplicationDocumentRules.NationalId));
        Assert.True(LeaseApplicationDocumentRules.HasRejectableDocument(
            docs, LeaseApplicationDocumentRules.SalaryIncome));
    }

    [Fact]
    public void HasRejectableDocument_WhenBothSubmitted_BothRejectable()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted)
        };

        Assert.True(LeaseApplicationDocumentRules.HasRejectableDocument(
            docs, LeaseApplicationDocumentRules.NationalId));
        Assert.True(LeaseApplicationDocumentRules.HasRejectableDocument(
            docs, LeaseApplicationDocumentRules.SalaryIncome));
    }

    [Theory]
    [InlineData("12_34_Applicant_NationalId.pdf", LeaseApplicationDocumentRules.NationalId, "12_34_Applicant_NationalId.pdf")]
    [InlineData(null, LeaseApplicationDocumentRules.SalaryIncome, "Salary / Income Proof")]
    public void GetDocumentViewerTitle_PrefersFileName(string? fileName, string type, string expected)
    {
        Assert.Equal(expected, LeaseApplicationDocumentRules.GetDocumentViewerTitle(fileName, type));
    }

    [Theory]
    [InlineData(LeaseApplicationDocumentRules.NationalId, true)]
    [InlineData(LeaseApplicationDocumentRules.SalaryIncome, true)]
    [InlineData("Other", false)]
    public void IsRequiredDocumentType_ValidatesTypes(string type, bool expected)
    {
        Assert.Equal(expected, LeaseApplicationDocumentRules.IsRequiredDocumentType(type));
    }

    [Fact]
    public void BuildUserDocumentsSubdirectory_UsesUserIdAndSanitizedName()
    {
        var dir = LeaseApplicationDocumentRules.BuildUserDocumentsSubdirectory(42, "Sara Al-Khalifa");
        Assert.Equal("42_Sara_Al_Khalifa", dir);
    }

    [Fact]
    public void BuildUserDocumentsArchiveRelativePath_IncludesDocumentsRoot()
    {
        var path = LeaseApplicationDocumentRules.BuildUserDocumentsArchiveRelativePath(7, "John Doe");
        Assert.StartsWith(LeaseApplicationDocumentRules.DocumentsArchiveFolderName, path);
        Assert.Contains("7_John_Doe", path);
    }

    [Fact]
    public void IsDocumentTypeRejected_DetectsRejectedTypeOnly()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusRejected),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted)
        };

        Assert.True(LeaseApplicationDocumentRules.IsDocumentTypeRejected(
            LeaseApplicationDocumentRules.NationalId, docs));
        Assert.False(LeaseApplicationDocumentRules.IsDocumentTypeRejected(
            LeaseApplicationDocumentRules.SalaryIncome, docs));
    }

    [Fact]
    public void GetRejectedDocumentTypes_WhenOneRejected_ReturnsSingleType()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusRejected),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted)
        };

        var rejected = LeaseApplicationDocumentRules.GetRejectedDocumentTypes(docs);
        Assert.Single(rejected);
        Assert.Equal(LeaseApplicationDocumentRules.NationalId, rejected[0]);
    }

    [Fact]
    public void GetRejectedDocumentTypes_WhenTwoRejected_ReturnsBothTypes()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusRejected),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusRejected)
        };

        Assert.Equal(2, LeaseApplicationDocumentRules.GetRejectedDocumentTypes(docs).Count);
    }

    [Theory]
    [InlineData(LeaseApplicationDocumentRules.NationalId, "NationalIdFile")]
    [InlineData(LeaseApplicationDocumentRules.SalaryIncome, "SalaryIncomeFile")]
    public void GetReUploadPropertyName_MapsFormFields(string type, string expected)
    {
        Assert.Equal(expected, LeaseApplicationDocumentRules.GetReUploadPropertyName(type));
    }

    [Fact]
    public void TenantCanReUploadDocumentType_RequiresScreeningAndRejectedType()
    {
        var docs = new[]
        {
            (LeaseApplicationDocumentRules.NationalId, LeaseApplicationDocumentRules.DocumentStatusRejected),
            (LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted)
        };

        Assert.True(LeaseApplicationDocumentRules.TenantCanReUploadDocumentType(
            LeaseApplicationDocumentRules.ApplicationStatusScreening,
            LeaseApplicationDocumentRules.NationalId, docs));
        Assert.False(LeaseApplicationDocumentRules.TenantCanReUploadDocumentType(
            LeaseApplicationDocumentRules.ApplicationStatusScreening,
            LeaseApplicationDocumentRules.SalaryIncome, docs));
    }
}
