using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace PMMIS.Web.Controllers;

/// <summary>
/// API для Syncfusion FileManager — физическая файловая система
/// Корневая папка: wwwroot/uploads/filemanager/
/// </summary>
[Authorize]
[Route("api/[controller]")]
[ApiController]
public class FileManagerController : ControllerBase
{
    private readonly string _root;
    private readonly IWebHostEnvironment _env;

    public FileManagerController(IWebHostEnvironment env)
    {
        _env = env;
        _root = Path.Combine(env.WebRootPath, "uploads", "filemanager");
        if (!Directory.Exists(_root))
            Directory.CreateDirectory(_root);
    }

    [HttpPost("FileOperations")]
    public IActionResult FileOperations([FromBody] JsonElement args)
    {
        var action = args.GetProperty("action").GetString();
        var path = args.TryGetProperty("path", out var p) ? p.GetString() ?? "/" : "/";

        return action switch
        {
            "read" => ReadFiles(path),
            "create" => CreateFolder(path, args),
            "delete" => DeleteItems(path, args),
            "rename" => RenameItem(path, args),
            "details" => GetDetails(path, args),
            "search" => SearchFiles(path, args),
            _ => Ok(new { error = new { code = "400", message = $"Unknown action: {action}" } })
        };
    }

    [HttpPost("Upload")]
    [DisableRequestSizeLimit]
    public IActionResult Upload(string path, string action, IList<IFormFile>? uploadFiles)
    {
        // Sanitize path — block invalid segments like "undefined"
        path = SanitizePath(path);

        // Handle remove action (cancel upload)
        if (action == "remove")
        {
            var fileName = Request.Form["cancel-uploading"]!;
            var filePath = Path.Combine(GetPhysicalPath(path), fileName!);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
            return Ok(new { });
        }

        var targetDir = GetPhysicalPath(path);
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        if (uploadFiles == null) return Ok(new { });

        foreach (var file in uploadFiles)
        {
            var filePath = Path.Combine(targetDir, file.FileName);

            if (action == "save" && System.IO.File.Exists(filePath))
            {
                // File exists — return proper Syncfusion conflict response
                var existFile = new FileInfo(filePath);
                var fileData = new
                {
                    name = existFile.Name,
                    size = existFile.Length,
                    isFile = true,
                    dateModified = existFile.LastWriteTimeUtc.ToString("o"),
                    dateCreated = existFile.CreationTimeUtc.ToString("o"),
                    type = existFile.Extension,
                    filterPath = path.EndsWith("/") ? path : path + "/",
                    hasChild = false
                };
                Response.StatusCode = 400;
                return new JsonResult(new { error = new { code = "400", message = "File Already Exists", fileExists = new[] { file.FileName } } });
            }

            if (action == "keepboth" && System.IO.File.Exists(filePath))
            {
                var ext = Path.GetExtension(file.FileName);
                var nameWithout = Path.GetFileNameWithoutExtension(file.FileName);
                int count = 1;
                while (System.IO.File.Exists(filePath))
                {
                    filePath = Path.Combine(targetDir, $"{nameWithout}({count}){ext}");
                    count++;
                }
            }

            // action == "replace" or new file — just write
            using var stream = new FileStream(filePath, FileMode.Create);
            file.CopyTo(stream);
        }
        return Ok(new { });
    }

    [HttpPost("Download")]
    public IActionResult Download([FromBody] JsonElement args)
    {
        var path = args.GetProperty("path").GetString() ?? "/";
        var names = args.GetProperty("names");
        var name = names.EnumerateArray().First().GetString()!;
        var fullPath = Path.Combine(GetPhysicalPath(path), name);

        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var contentType = "application/octet-stream";
        return PhysicalFile(fullPath, contentType, name);
    }

    // ========== Helpers ==========

    private string GetPhysicalPath(string virtualPath)
    {
        var clean = SanitizePath(virtualPath)
            .Replace("/", Path.DirectorySeparatorChar.ToString())
            .TrimStart(Path.DirectorySeparatorChar);
        return Path.Combine(_root, clean);
    }

    /// <summary>
    /// Remove invalid path segments like "undefined", "..", etc.
    /// </summary>
    private static string SanitizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "/";
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var clean = segments.Where(s => s != "undefined" && s != "null" && s != "..").ToArray();
        return "/" + string.Join("/", clean) + "/";
    }

    private IActionResult ReadFiles(string path)
    {
        var physPath = GetPhysicalPath(path);
        if (!Directory.Exists(physPath))
            Directory.CreateDirectory(physPath);

        var dirInfo = new DirectoryInfo(physPath);
        var cwd = GetFileInfo(dirInfo, path, true);

        var files = new List<object>();
        foreach (var dir in dirInfo.GetDirectories())
            files.Add(GetFileInfo(dir, Path.Combine(path, dir.Name), true));
        foreach (var file in dirInfo.GetFiles())
            files.Add(GetFileInfo(file, path, false));

        return Ok(new { cwd, files });
    }

    private IActionResult CreateFolder(string path, JsonElement args)
    {
        var name = args.GetProperty("name").GetString()!;
        var newDir = Path.Combine(GetPhysicalPath(path), name);
        if (!Directory.Exists(newDir))
            Directory.CreateDirectory(newDir);

        var files = new List<object> { GetFileInfo(new DirectoryInfo(newDir), Path.Combine(path, name), true) };
        return Ok(new { files });
    }

    private IActionResult DeleteItems(string path, JsonElement args)
    {
        var names = args.GetProperty("names");
        var deleted = new List<object>();

        foreach (var nameEl in names.EnumerateArray())
        {
            var name = nameEl.GetString()!;
            var fullPath = Path.Combine(GetPhysicalPath(path), name);

            if (Directory.Exists(fullPath))
            {
                deleted.Add(GetFileInfo(new DirectoryInfo(fullPath), Path.Combine(path, name), true));
                Directory.Delete(fullPath, true);
            }
            else if (System.IO.File.Exists(fullPath))
            {
                deleted.Add(GetFileInfo(new FileInfo(fullPath), path, false));
                System.IO.File.Delete(fullPath);
            }
        }
        return Ok(new { files = deleted });
    }

    private IActionResult RenameItem(string path, JsonElement args)
    {
        var name = args.GetProperty("name").GetString()!;
        var newName = args.GetProperty("newName").GetString()!;
        var oldPath = Path.Combine(GetPhysicalPath(path), name);
        var newPath = Path.Combine(GetPhysicalPath(path), newName);

        if (Directory.Exists(oldPath))
        {
            Directory.Move(oldPath, newPath);
            return Ok(new { files = new[] { GetFileInfo(new DirectoryInfo(newPath), Path.Combine(path, newName), true) } });
        }
        else if (System.IO.File.Exists(oldPath))
        {
            System.IO.File.Move(oldPath, newPath);
            return Ok(new { files = new[] { GetFileInfo(new FileInfo(newPath), path, false) } });
        }
        return Ok(new { error = new { code = "404", message = "File not found" } });
    }

    private IActionResult GetDetails(string path, JsonElement args)
    {
        var names = args.GetProperty("names");
        var details = new List<object>();
        long totalSize = 0;

        foreach (var nameEl in names.EnumerateArray())
        {
            var name = nameEl.GetString()!;
            var fullPath = Path.Combine(GetPhysicalPath(path), name);
            if (System.IO.File.Exists(fullPath))
            {
                var fi = new FileInfo(fullPath);
                totalSize += fi.Length;
                details.Add(GetFileInfo(fi, path, false));
            }
            else if (Directory.Exists(fullPath))
            {
                details.Add(GetFileInfo(new DirectoryInfo(fullPath), Path.Combine(path, name), true));
            }
        }
        return Ok(new { details = details.FirstOrDefault(), files = details });
    }

    private IActionResult SearchFiles(string path, JsonElement args)
    {
        var searchString = args.GetProperty("searchString").GetString() ?? "";
        var physPath = GetPhysicalPath(path);
        var files = new List<object>();

        if (Directory.Exists(physPath))
        {
            foreach (var f in Directory.GetFiles(physPath, $"*{searchString}*", SearchOption.AllDirectories))
                files.Add(GetFileInfo(new FileInfo(f), path, false));
            foreach (var d in Directory.GetDirectories(physPath, $"*{searchString}*", SearchOption.AllDirectories))
                files.Add(GetFileInfo(new DirectoryInfo(d), path, true));
        }

        var cwd = GetFileInfo(new DirectoryInfo(physPath), path, true);
        return Ok(new { cwd, files });
    }

    private object GetFileInfo(FileSystemInfo info, string path, bool isDir)
    {
        if (isDir)
        {
            var di = (DirectoryInfo)info;
            return new
            {
                name = di.Name,
                size = 0,
                isFile = false,
                dateModified = di.LastWriteTimeUtc.ToString("o"),
                dateCreated = di.CreationTimeUtc.ToString("o"),
                type = "",
                filterPath = path.EndsWith("/") ? path : path + "/",
                hasChild = di.GetDirectories().Length > 0
            };
        }
        else
        {
            var fi = (FileInfo)info;
            return new
            {
                name = fi.Name,
                size = fi.Length,
                isFile = true,
                dateModified = fi.LastWriteTimeUtc.ToString("o"),
                dateCreated = fi.CreationTimeUtc.ToString("o"),
                type = fi.Extension,
                filterPath = path.EndsWith("/") ? path : path + "/",
                hasChild = false
            };
        }
    }
}
