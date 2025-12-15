using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.Runtime;

namespace WebMobileAssignment.Services;

public class S3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public S3Service(IConfiguration config)
    {
        var awsAccessKey = config["AWS:AccessKey"];
        var awsSecretKey = config["AWS:SecretKey"];
        var region = config["AWS:Region"];

        var credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
        };

        _s3Client = new AmazonS3Client(credentials, s3Config);
        _bucketName = config["AWS:BucketName"]!;
    }

    /// <summary>
    /// Upload a file to S3 and return the public URL
    /// </summary>
    public async Task<string> UploadFileAsync(IFormFile file, string userId)
    {
        // Validate file
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        // Validate file type (images only)
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(extension))
            throw new ArgumentException($"Invalid file type. Allowed: {string.Join(", ", allowedExtensions)}");

        // Validate file size (max 5 MB)
        if (file.Length > 5 * 1024 * 1024)
            throw new ArgumentException("File size must not exceed 5 MB");

        // Generate unique filename
        var fileName = $"profiles/{userId}_{Guid.NewGuid()}{extension}";

        // Upload to S3
        using var stream = file.OpenReadStream();
        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = stream,
            Key = fileName,
            BucketName = _bucketName,
            ContentType = file.ContentType
            // Removed CannedACL - using bucket policy instead
        };

        var transferUtility = new TransferUtility(_s3Client);
        await transferUtility.UploadAsync(uploadRequest);

        // Return public URL
        return $"https://{_bucketName}.s3.ap-southeast-1.amazonaws.com/{fileName}";
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
