using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.Runtime;
using Amazon.S3.Model;

namespace WebMobileAssignment.Services;

public class S3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public S3Service(IConfiguration config)
    {
        // Add null checks and logging to diagnose the issue
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config), "IConfiguration is null");
        }

        var awsAccessKey = config["AWS:AccessKey"];
        var awsSecretKey = config["AWS:SecretKey"];
        var region = config["AWS:Region"];
        var bucketName = config["AWS:BucketName"];

        // Log configuration values for debugging (remove in production!)
        Console.WriteLine($"[S3Service] AWS AccessKey: {(string.IsNullOrEmpty(awsAccessKey) ? "NULL/EMPTY" : "PRESENT")}");
        Console.WriteLine($"[S3Service] AWS SecretKey: {(string.IsNullOrEmpty(awsSecretKey) ? "NULL/EMPTY" : "PRESENT")}");
        Console.WriteLine($"[S3Service] AWS Region: {region ?? "NULL"}");
        Console.WriteLine($"[S3Service] AWS BucketName: {bucketName ?? "NULL"}");

        // Validate all required configuration values
        if (string.IsNullOrEmpty(awsAccessKey))
            throw new InvalidOperationException("AWS AccessKey is not configured in appsettings.json");
        
        if (string.IsNullOrEmpty(awsSecretKey))
            throw new InvalidOperationException("AWS SecretKey is not configured in appsettings.json");
        
        if (string.IsNullOrEmpty(region))
            throw new InvalidOperationException("AWS Region is not configured in appsettings.json");
        
        if (string.IsNullOrEmpty(bucketName))
            throw new InvalidOperationException("AWS BucketName is not configured in appsettings.json");

        try
        {
            var credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            var s3Config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
            };

            _s3Client = new AmazonS3Client(credentials, s3Config);
            _bucketName = bucketName;
            
            Console.WriteLine("[S3Service] S3 client initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[S3Service] ERROR initializing S3 client: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"[S3Service] Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException($"Failed to initialize S3Service: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Upload a file to S3 and return the public URL
    /// </summary>
    public async Task<string> UploadFileAsync(IFormFile file, string userId)
    {
        Console.WriteLine($"[S3Service.UploadFileAsync] Starting upload - File: {file?.FileName}, Size: {file?.Length}, UserId: {userId}");

        // Validate file
        if (file == null || file.Length == 0)
        {
            Console.WriteLine("[S3Service.UploadFileAsync] ERROR: File is null or empty");
            throw new ArgumentException("File is empty");
        }

        // Validate file type (images only)
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        Console.WriteLine($"[S3Service.UploadFileAsync] File extension: {extension}");
        
        if (!allowedExtensions.Contains(extension))
        {
            Console.WriteLine($"[S3Service.UploadFileAsync] ERROR: Invalid file type: {extension}");
            throw new ArgumentException($"Invalid file type. Allowed: {string.Join(", ", allowedExtensions)}");
        }

        // Validate file size (max 5 MB)
        if (file.Length > 5 * 1024 * 1024)
        {
            Console.WriteLine($"[S3Service.UploadFileAsync] ERROR: File too large: {file.Length} bytes");
            throw new ArgumentException("File size must not exceed 5 MB");
        }

        // Generate unique filename
        var fileName = $"profiles/{userId}_{Guid.NewGuid()}{extension}";
        Console.WriteLine($"[S3Service.UploadFileAsync] Generated S3 key: {fileName}");
        Console.WriteLine($"[S3Service.UploadFileAsync] Bucket: {_bucketName}");

        // Check if _s3Client is null
        if (_s3Client == null)
        {
            Console.WriteLine("[S3Service.UploadFileAsync] ERROR: _s3Client is NULL!");
            throw new InvalidOperationException("S3 client is not initialized. Check your AWS configuration in appsettings.json");
        }

        try
        {
            // Upload to S3 with public-read ACL
            using var stream = file.OpenReadStream();
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = fileName,
                BucketName = _bucketName,
                ContentType = file.ContentType,
                CannedACL = S3CannedACL.PublicRead // Make file publicly readable
            };

            Console.WriteLine($"[S3Service.UploadFileAsync] Starting S3 upload - ContentType: {file.ContentType}");

            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadAsync(uploadRequest);

            // Return public URL
            var publicUrl = $"https://{_bucketName}.s3.ap-southeast-1.amazonaws.com/{fileName}";
            Console.WriteLine($"[S3Service.UploadFileAsync] Upload successful! URL: {publicUrl}");
            
            return publicUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[S3Service.UploadFileAsync] UPLOAD FAILED: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"[S3Service.UploadFileAsync] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[S3Service.UploadFileAsync] Inner exception: {ex.InnerException.Message}");
            }
            throw new InvalidOperationException($"Failed to upload file to S3: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Upload a document file (PDF, DOC, images) to S3 and return the public URL
    /// For leave applications and other document uploads
    /// </summary>
    public async Task<string> UploadDocumentAsync(IFormFile file, string folderPath)
    {
        Console.WriteLine($"[S3Service] Starting document upload - File: {file?.FileName}, Size: {file?.Length}");
        
        // Validate file
        if (file == null || file.Length == 0)
        {
            Console.WriteLine("[S3Service] ERROR: File is null or empty");
            throw new ArgumentException("File is empty");
        }

        // Validate file type (images and documents)
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        Console.WriteLine($"[S3Service] File extension: {extension}");
        
        if (!allowedExtensions.Contains(extension))
        {
            Console.WriteLine($"[S3Service] ERROR: Invalid file type: {extension}");
            throw new ArgumentException($"Invalid file type. Allowed: {string.Join(", ", allowedExtensions.Select(e => e.TrimStart('.')))}");
        }

        // Validate file size (max 5 MB)
        if (file.Length > 5 * 1024 * 1024)
        {
            Console.WriteLine($"[S3Service] ERROR: File too large: {file.Length} bytes");
            throw new ArgumentException("File size must not exceed 5 MB");
        }

        // Generate unique filename
        var fileName = $"{folderPath}/{Guid.NewGuid()}{extension}";
        Console.WriteLine($"[S3Service] Generated S3 key: {fileName}");
        Console.WriteLine($"[S3Service] Bucket: {_bucketName}");

        try
        {
            // Upload to S3 with public-read ACL
            using var stream = file.OpenReadStream();
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = fileName,
                BucketName = _bucketName,
                ContentType = file.ContentType,
                CannedACL = S3CannedACL.PublicRead // Make file publicly readable
            };

            Console.WriteLine($"[S3Service] Starting S3 upload - ContentType: {file.ContentType}");
            
            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadAsync(uploadRequest);

            // Return public URL
            var publicUrl = $"https://{_bucketName}.s3.ap-southeast-1.amazonaws.com/{fileName}";
            Console.WriteLine($"[S3Service] Upload successful! URL: {publicUrl}");
            
            return publicUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[S3Service] UPLOAD FAILED: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"[S3Service] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[S3Service] Inner exception: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Delete a file from S3 using its URL
    /// </summary>
    public async Task DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl) || fileUrl == "/images/default-avatar.png")
            return; // Don't delete default avatar

        try
        {
            // Extract filename from URL
            var uri = new Uri(fileUrl);
            var fileName = uri.AbsolutePath.TrimStart('/');

            await _s3Client.DeleteObjectAsync(_bucketName, fileName);
        }
        catch (Exception ex)
        {
            // Log error but don't throw (file might already be deleted)
            Console.WriteLine($"Error deleting S3 file: {ex.Message}");
        }
    }
}
