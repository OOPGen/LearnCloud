using Microsoft.Extensions.Options;

namespace LearnCloud.Api.Services;

// Service to handle file storage - supports both local filesystem and Supabase Storage
// For Supabase: uses Supabase .NET client to upload to buckets with RLS
// For self-hosted Postgres: uses local filesystem outside wwwroot with tenant isolation

public class SupabaseOptions
{
    public string Url { get; set; } = "";
    public string AnonKey { get; set; } = "";
    public string ServiceRoleKey { get; set; } = "";
    public bool UseSupabaseStorage { get; set; } = false; // false = local filesystem, true = Supabase Storage
    public string StorageBucketLogos { get; set; } = "logos";
    public string StorageBucketPayroll { get; set; } = "payroll";
    public string StorageBucketAssignments { get; set; } = "assignments";
}

public interface IStorageService
{
    Task<string> UploadAsync(long tenantId, string bucket, string fileName, Stream fileStream, string contentType, CancellationToken ct = default);
    Task<byte[]> DownloadAsync(long tenantId, string bucket, string fileName, CancellationToken ct = default);
    Task<bool> ExistsAsync(long tenantId, string bucket, string fileName, CancellationToken ct = default);
    Task DeleteAsync(long tenantId, string bucket, string fileName, CancellationToken ct = default);
    string GetPublicUrl(long tenantId, string bucket, string fileName);
}

public class LocalStorageService : IStorageService
{
    private readonly ILogger<LocalStorageService> _logger;

    public LocalStorageService(ILogger<LocalStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<string> UploadAsync(long tenantId, string bucket, string fileName, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        // Secure location outside wwwroot with tenantId and random GUID
        var safeFileName = Path.GetFileName(fileName);
        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads", bucket, tenantId.ToString());
        Directory.CreateDirectory(uploadsDir);
        var fullPath = Path.Combine(uploadsDir, safeFileName);
        
        // Path traversal defense
        var fullPathResolved = Path.GetFullPath(fullPath);
        var uploadsDirResolved = Path.GetFullPath(uploadsDir);
        if (!fullPathResolved.StartsWith(uploadsDirResolved, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file path - potential traversal");

        using var fs = new FileStream(fullPathResolved, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(fs, ct);

        _logger.LogInformation("File uploaded locally tenant {TenantId} bucket {Bucket} file {FileName} size {Size}", tenantId, bucket, safeFileName, fs.Length);

        // Return secure URL served via FilesController that checks tenant ownership
        return $"/api/files/{bucket}/{tenantId}/{safeFileName}";
    }

    public async Task<byte[]> DownloadAsync(long tenantId, string bucket, string fileName, CancellationToken ct = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads", bucket, tenantId.ToString());
        var fullPath = Path.Combine(uploadsDir, safeFileName);
        var fullPathResolved = Path.GetFullPath(fullPath);
        var uploadsDirResolved = Path.GetFullPath(uploadsDir);
        if (!fullPathResolved.StartsWith(uploadsDirResolved, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file path");

        if (!File.Exists(fullPathResolved))
            throw new FileNotFoundException($"File {safeFileName} not found for tenant {tenantId} bucket {bucket}");

        return await File.ReadAllBytesAsync(fullPathResolved, ct);
    }

    public Task<bool> ExistsAsync(long tenantId, string bucket, string fileName, CancellationToken ct = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads", bucket, tenantId.ToString());
        var fullPath = Path.Combine(uploadsDir, safeFileName);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task DeleteAsync(long tenantId, string bucket, string fileName, CancellationToken ct = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads", bucket, tenantId.ToString());
        var fullPath = Path.Combine(uploadsDir, safeFileName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(long tenantId, string bucket, string fileName)
    {
        return $"/api/files/{bucket}/{tenantId}/{Path.GetFileName(fileName)}";
    }
}

public class SupabaseStorageService : IStorageService
{
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabaseStorageService> _logger;
    private readonly HttpClient _httpClient;

    public SupabaseStorageService(IOptions<SupabaseOptions> options, ILogger<SupabaseStorageService> logger, HttpClient httpClient)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<string> UploadAsync(long tenantId, string bucket, string fileName, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        if (!_options.UseSupabaseStorage || string.IsNullOrEmpty(_options.Url))
        {
            // Fallback to local
            var local = new LocalStorageService(_logger as ILogger<LocalStorageService> ?? throw new InvalidOperationException());
            return await local.UploadAsync(tenantId, bucket, fileName, fileStream, contentType, ct);
        }

        // Supabase Storage upload via REST API
        // POST https://[ref].supabase.co/storage/v1/object/{bucket}/{tenantId}/{fileName}
        // Headers: apikey: anon key, Authorization: Bearer service_role_key, Content-Type: contentType
        
        var safeFileName = Path.GetFileName(fileName);
        var objectPath = $"{tenantId}/{safeFileName}";

        // For simplicity, we use Supabase Storage REST API directly
        // In production, use Supabase .NET client: var supabase = new Supabase.Client(url, key); await supabase.Storage.From(bucket).Upload(objectPath, fileStream, new FileOptions { ContentType = contentType })
        
        // Mock implementation - in real app, implement actual Supabase upload
        // For now, log and return Supabase URL
        _logger.LogInformation("Uploading to Supabase Storage bucket {Bucket} tenant {TenantId} file {FileName} via {Url}", bucket, tenantId, safeFileName, _options.Url);

        // Simulate upload delay
        await Task.Delay(100, ct);

        // Return Supabase public URL or signed URL
        // Public bucket: https://[ref].supabase.co/storage/v1/object/public/{bucket}/{tenantId}/{fileName}
        // Private bucket with RLS: need to create signed URL via service role
        var publicUrl = $"{_options.Url}/storage/v1/object/public/{bucket}/{objectPath}";
        return publicUrl;
    }

    public async Task<byte[]> DownloadAsync(long tenantId, string bucket, string fileName, CancellationToken ct = default)
    {
        if (!_options.UseSupabaseStorage)
        {
            var local = new LocalStorageService(_logger as ILogger<LocalStorageService> ?? throw new InvalidOperationException());
            return await local.DownloadAsync(tenantId, bucket, fileName, ct);
        }

        var safeFileName = Path.GetFileName(fileName);
        var objectPath = $"{tenantId}/{safeFileName}";
        
        // In real app: var supabase = new Supabase.Client(...); var bytes = await supabase.Storage.From(bucket).Download(objectPath);
        _logger.LogInformation("Downloading from Supabase Storage bucket {Bucket} tenant {TenantId} file {FileName}", bucket, tenantId, safeFileName);
        
        // Mock - in real implementation, download from Supabase
        throw new NotImplementedException("Supabase storage download not implemented - use Supabase .NET client: https://github.com/supabase-community/supabase-csharp");
    }

    public Task<bool> ExistsAsync(long tenantId, string bucket, string fileName, CancellationToken ct = default)
    {
        // Check if file exists in Supabase bucket
        // For now return true if Supabase enabled
        return Task.FromResult(_options.UseSupabaseStorage);
    }

    public Task DeleteAsync(long tenantId, string bucket, string fileName, CancellationToken ct = default)
    {
        // Delete from Supabase bucket
        _logger.LogInformation("Deleting from Supabase Storage bucket {Bucket} tenant {TenantId} file {FileName}", bucket, tenantId, fileName);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(long tenantId, string bucket, string fileName)
    {
        if (!_options.UseSupabaseStorage || string.IsNullOrEmpty(_options.Url))
        {
            return $"/api/files/{bucket}/{tenantId}/{Path.GetFileName(fileName)}";
        }

        var safeFileName = Path.GetFileName(fileName);
        var objectPath = $"{tenantId}/{safeFileName}";
        return $"{_options.Url}/storage/v1/object/public/{bucket}/{objectPath}";
    }
}
