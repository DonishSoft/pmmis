using PMMIS.Domain.Entities;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис загрузки и управления файлами
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Загрузить файл и создать запись Document
    /// </summary>
    Task<Document> UploadFileAsync(IFormFile file, string folder, DocumentType type, string? userId = null);
    
    /// <summary>
    /// Загрузить несколько файлов
    /// </summary>
    Task<List<Document>> UploadFilesAsync(IEnumerable<IFormFile> files, string folder, DocumentType type, string? userId = null);
    
    /// <summary>
    /// Удалить файл и запись Document
    /// </summary>
    Task DeleteFileAsync(int documentId);
}

/// <summary>
/// Реализация сервиса загрузки файлов
/// </summary>
public class FileService : IFileService
{
    private readonly IWebHostEnvironment _environment;
    private readonly Infrastructure.Data.ApplicationDbContext _context;

    public FileService(IWebHostEnvironment environment, Infrastructure.Data.ApplicationDbContext context)
    {
        _environment = environment;
        _context = context;
    }

    public async Task<Document> UploadFileAsync(IFormFile file, string folder, DocumentType type, string? userId = null)
    {
        var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadsPath);
        
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsPath, fileName);
        
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        
        var document = new Document
        {
            FileName = fileName,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            FilePath = $"/uploads/{folder}/{fileName}",
            Type = type,
            UploadedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        
        return document;
    }
    
    public async Task<List<Document>> UploadFilesAsync(IEnumerable<IFormFile> files, string folder, DocumentType type, string? userId = null)
    {
        var documents = new List<Document>();
        foreach (var file in files)
        {
            if (file.Length > 0)
            {
                var doc = await UploadFileAsync(file, folder, type, userId);
                documents.Add(doc);
            }
        }
        return documents;
    }

    public async Task DeleteFileAsync(int documentId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document != null)
        {
            // Delete physical file
            var fullPath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
        }
    }
}
