namespace PropertyLeasing.BusinessLogic;

/// <summary>
/// Validation rules for lease application supporting documents (regular applications only).
/// </summary>
public static class LeaseApplicationDocumentRules
{
    public const string NationalId   = "NationalId";
    public const string SalaryIncome = "SalaryIncome";

    public const string DocumentStatusSubmitted = "Submitted";
    public const string DocumentStatusRejected  = "Rejected";

    public const string ApplicationStatusScreening         = "Screening";
    /// <summary>Legacy status; re-upload now stays on Screening.</summary>
    public const string ApplicationStatusDocumentsRequired = "DocumentsRequired";

    /// <summary>Application statuses where managers may open/download tenant documents.</summary>
    public static readonly string[] ManagerDocumentViewStatuses =
    {
        "Screening",
        "Approved",
        "Rejected",
        "Canceled",
        ApplicationStatusDocumentsRequired
    };

    public static readonly string[] RequiredRegularDocumentTypes = { NationalId, SalaryIncome };

    public const string AllowedExtension = ".pdf";

    public const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>Root folder under the app content root for approved-application document copies.</summary>
    public const string DocumentsArchiveFolderName = "Documents";

    public static bool IsRenewalApplication(int? parentLeaseId) => parentLeaseId.HasValue;

    /// <summary>Per-tenant subdirectory: {userId}_{sanitizedName}.</summary>
    public static string BuildUserDocumentsSubdirectory(int userId, string fullName) =>
        $"{userId}_{SanitizeApplicantName(fullName)}";

    public static string BuildUserDocumentsArchiveRelativePath(int userId, string fullName) =>
        Path.Combine(DocumentsArchiveFolderName, BuildUserDocumentsSubdirectory(userId, fullName));

    public static bool IsDocumentTypeRejected(
        string documentType,
        IEnumerable<(string Type, string Status)> documents) =>
        documents.Any(d =>
            string.Equals(d.Type, documentType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(d.Status, DocumentStatusRejected, StringComparison.OrdinalIgnoreCase));

    public static bool TenantCanReUploadDocumentType(
        string applicationStatus,
        string documentType,
        IEnumerable<(string Type, string Status)> documents) =>
        TenantCanReUploadDocuments(applicationStatus, documents)
        && IsDocumentTypeRejected(documentType, documents);

    public static bool ManagerCanViewDocuments(string applicationStatus) =>
        ManagerDocumentViewStatuses.Contains(applicationStatus, StringComparer.OrdinalIgnoreCase);

    /// <summary>Statuses where a tenant may replace rejected PDFs (Screening, or legacy DocumentsRequired).</summary>
    public static readonly string[] TenantReUploadApplicationStatuses =
    {
        ApplicationStatusScreening,
        ApplicationStatusDocumentsRequired
    };

    /// <summary>Tenant may replace rejected PDFs while the application stays in Screening.</summary>
    public static bool TenantCanReUploadDocuments(
        string applicationStatus,
        IEnumerable<(string Type, string Status)> documents) =>
        TenantReUploadApplicationStatuses.Contains(applicationStatus, StringComparer.OrdinalIgnoreCase)
        && GetRejectedDocumentTypes(documents).Count > 0;

    /// <summary>True when an active (non-rejected) document exists for the type and can be rejected.</summary>
    public static bool HasRejectableDocument(
        IEnumerable<(string Type, string Status)> documents,
        string documentType) =>
        documents.Any(d =>
            string.Equals(d.Type, documentType, StringComparison.OrdinalIgnoreCase)
            && IsActiveDocumentStatus(d.Status));

    /// <summary>Browser tab title when viewing a PDF inline.</summary>
    public static string GetDocumentViewerTitle(string? fileName, string? documentType)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
            return fileName.Trim();
        return GetDisplayName(documentType ?? string.Empty);
    }

    public static bool IsRequiredDocumentType(string documentType) =>
        RequiredRegularDocumentTypes.Contains(documentType, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetRequiredDocumentTypes(int? parentLeaseId) =>
        IsRenewalApplication(parentLeaseId)
            ? Array.Empty<string>()
            : RequiredRegularDocumentTypes;

    public static string GetDisplayName(string documentType) => documentType switch
    {
        NationalId   => "National ID",
        SalaryIncome => "Salary / Income Proof",
        _            => documentType
    };

    public static bool IsAllowedExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        return string.Equals(Path.GetExtension(fileName), AllowedExtension, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAllowedFileSize(long length) =>
        length > 0 && length <= MaxFileBytes;

    public static string SanitizeApplicantName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "Applicant";
        var chars = fullName.Trim()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        var sanitized = new string(chars).Trim('_');
        while (sanitized.Contains("__", StringComparison.Ordinal))
            sanitized = sanitized.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrEmpty(sanitized) ? "Applicant" : sanitized;
    }

    /// <summary>
    /// Stored file name: {applicationId}_{userId}_{applicantName}_{documentType}.pdf
    /// </summary>
    public static string BuildStoredFileName(
        int applicationId,
        int userId,
        string applicantFullName,
        string documentType) =>
        $"{applicationId}_{userId}_{SanitizeApplicantName(applicantFullName)}_{documentType}{AllowedExtension}";

    public static bool IsActiveDocumentStatus(string? status) =>
        !string.Equals(status, DocumentStatusRejected, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true when every required type has at least one non-rejected document.
    /// </summary>
    public static bool HasAllRequiredDocuments(
        int? parentLeaseId,
        IEnumerable<(string Type, string Status)> documents)
    {
        var required = GetRequiredDocumentTypes(parentLeaseId);
        if (required.Count == 0) return true;

        var activeTypes = documents
            .Where(d => IsActiveDocumentStatus(d.Status))
            .Select(d => d.Type)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return required.All(activeTypes.Contains);
    }

    public static IReadOnlyList<string> GetMissingDocumentTypes(
        int? parentLeaseId,
        IEnumerable<(string Type, string Status)> documents)
    {
        var required = GetRequiredDocumentTypes(parentLeaseId);
        var activeTypes = documents
            .Where(d => IsActiveDocumentStatus(d.Status))
            .Select(d => d.Type)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return required.Where(t => !activeTypes.Contains(t)).ToList();
    }

    public static IReadOnlyList<string> GetRejectedDocumentTypes(
        IEnumerable<(string Type, string Status)> documents) =>
        documents
            .Where(d => string.Equals(d.Status, DocumentStatusRejected, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Type)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool IsNationalIdType(string documentType) =>
        string.Equals(documentType, NationalId, StringComparison.OrdinalIgnoreCase);

    public static bool IsSalaryIncomeType(string documentType) =>
        string.Equals(documentType, SalaryIncome, StringComparison.OrdinalIgnoreCase);

    public static string GetReUploadPropertyName(string documentType)
    {
        if (IsNationalIdType(documentType)) return "NationalIdFile";
        if (IsSalaryIncomeType(documentType)) return "SalaryIncomeFile";
        return "DocumentFile";
    }

    public static IReadOnlyList<string> ValidateRegularApplicationUploads(
        bool hasNationalIdFile,
        bool hasSalaryIncomeFile,
        string? nationalIdFileName,
        long nationalIdLength,
        string? salaryIncomeFileName,
        long salaryIncomeLength)
    {
        var errors = new List<string>();

        if (!hasNationalIdFile)
            errors.Add("National ID document is required.");
        else
            AppendFileErrors(errors, nationalIdFileName, nationalIdLength, "National ID");

        if (!hasSalaryIncomeFile)
            errors.Add("Salary / income proof document is required.");
        else
            AppendFileErrors(errors, salaryIncomeFileName, salaryIncomeLength, "Salary / income proof");

        return errors;
    }

    public static IReadOnlyList<string> ValidateSinglePdfUpload(
        bool hasFile,
        string? fileName,
        long length,
        string label)
    {
        var errors = new List<string>();
        if (!hasFile)
        {
            errors.Add($"{label} PDF is required.");
            return errors;
        }

        AppendFileErrors(errors, fileName, length, label);
        return errors;
    }

    private static void AppendFileErrors(List<string> errors, string? fileName, long length, string label)
    {
        if (!IsAllowedExtension(fileName))
            errors.Add($"{label}: only PDF files are allowed.");
        if (!IsAllowedFileSize(length))
            errors.Add($"{label}: file must be between 1 byte and 10 MB.");
    }
}
