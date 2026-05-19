using Microsoft.AspNetCore.Http;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
using PropertyLeasing.BusinessLogic;

namespace PropertyLeasing.MVC.Services;

public class LeaseApplicationDocumentService
{
    private readonly PropertyLeasingDbContext _db;
    private readonly IWebHostEnvironment      _env;

    public LeaseApplicationDocumentService(PropertyLeasingDbContext db, IWebHostEnvironment env)
    {
        _db  = db;
        _env = env;
    }

    public async Task SaveRegularApplicationDocumentsAsync(
        int applicationId,
        int userId,
        string applicantFullName,
        IFormFile nationalIdFile,
        IFormFile salaryIncomeFile,
        CancellationToken cancellationToken = default)
    {
        await SaveFileAsync(applicationId, userId, applicantFullName, nationalIdFile,
            LeaseApplicationDocumentRules.NationalId, cancellationToken);
        await SaveFileAsync(applicationId, userId, applicantFullName, salaryIncomeFile,
            LeaseApplicationDocumentRules.SalaryIncome, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Removes the rejected row and file, then stores a new submitted document.</summary>
    public async Task ReplaceRejectedDocumentAsync(
        Document rejected,
        int applicationId,
        int userId,
        string applicantFullName,
        IFormFile file,
        string documentType,
        CancellationToken cancellationToken = default)
    {
        DeletePhysicalFile(rejected.StoragePath);
        _db.Documents.Remove(rejected);

        var doc = new Document
        {
            ApplicationId = applicationId,
            UserId          = userId,
            FileType        = documentType,
            Status          = LeaseApplicationDocumentRules.DocumentStatusSubmitted,
            UploadedAt      = DateTime.Now
        };
        _db.Documents.Add(doc);
        await WriteFileAsync(doc, applicationId, userId, applicantFullName, file, documentType, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveFileAsync(
        int applicationId,
        int userId,
        string applicantFullName,
        IFormFile file,
        string documentType,
        CancellationToken cancellationToken = default)
    {
        var doc = new Document
        {
            ApplicationId = applicationId,
            UserId          = userId,
            FileType        = documentType,
            Status          = LeaseApplicationDocumentRules.DocumentStatusSubmitted,
            UploadedAt      = DateTime.Now
        };
        _db.Documents.Add(doc);
        await WriteFileAsync(doc, applicationId, userId, applicantFullName, file, documentType, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteFileAsync(
        Document doc,
        int applicationId,
        int userId,
        string applicantFullName,
        IFormFile file,
        string documentType,
        CancellationToken cancellationToken)
    {
        var storedName  = LeaseApplicationDocumentRules.BuildStoredFileName(
            applicationId, userId, applicantFullName, documentType);
        var relativeDir = Path.Combine("uploads", "lease-applications", applicationId.ToString());
        var absoluteDir = Path.Combine(_env.WebRootPath, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var absolutePath = Path.Combine(absoluteDir, storedName);
        await using (var stream = new FileStream(absolutePath, FileMode.Create))
            await file.CopyToAsync(stream, cancellationToken);

        doc.FileName      = storedName;
        doc.StoragePath   = "/" + relativeDir.Replace('\\', '/') + "/" + storedName;
        doc.Status        = LeaseApplicationDocumentRules.DocumentStatusSubmitted;
        doc.RejectionReason = null;
        doc.UploadedAt    = DateTime.Now;
    }

    public void DeletePhysicalFile(string? storagePath)
    {
        var path = ResolveAbsolutePath(storagePath);
        if (path != null && File.Exists(path))
            File.Delete(path);
    }

    public string? ResolveAbsolutePath(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return null;
        var relative = storagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var full     = Path.GetFullPath(Path.Combine(_env.WebRootPath, relative));
        var root     = Path.GetFullPath(_env.WebRootPath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
        return File.Exists(full) ? full : null;
    }

    public string GetUserDocumentsArchiveDirectory(int userId, string fullName)
    {
        var relative = LeaseApplicationDocumentRules.BuildUserDocumentsArchiveRelativePath(userId, fullName);
        var absolute = Path.GetFullPath(Path.Combine(_env.ContentRootPath, relative));
        var root     = Path.GetFullPath(
            Path.Combine(_env.ContentRootPath, LeaseApplicationDocumentRules.DocumentsArchiveFolderName));
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid archive path.");
        Directory.CreateDirectory(absolute);
        return absolute;
    }

    /// <summary>
    /// Copies active application PDFs into Documents/{userId}_{name}/ when the application is approved.
    /// </summary>
    public int ArchiveApprovedApplicationDocuments(
        int userId,
        string fullName,
        IEnumerable<Document> documents)
    {
        var archiveDir = GetUserDocumentsArchiveDirectory(userId, fullName);
        var archived   = 0;

        foreach (var doc in documents.Where(d =>
                     LeaseApplicationDocumentRules.IsActiveDocumentStatus(d.Status)))
        {
            var sourcePath = ResolveAbsolutePath(doc.StoragePath);
            if (sourcePath == null || string.IsNullOrWhiteSpace(doc.FileName))
                continue;

            var destPath = Path.Combine(archiveDir, doc.FileName);
            File.Copy(sourcePath, destPath, overwrite: true);
            archived++;
        }

        return archived;
    }
}
