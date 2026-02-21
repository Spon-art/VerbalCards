namespace VerbalCards.Services;

public interface IAudioUploader
{
    string OriginalFilename { get; set; }
    
    string ContentType { get; set; }
    
    Stream? InputStream { get; set; }
    
    Task<AudioUploaderResult> StoreAsync();
}

/// <summary>
/// Result of the audio upload.
/// </summary>
public sealed class AudioUploaderResult
{
    /// <summary>
    /// Status code for HTTP response.
    /// </summary>
    public required int StatusCode { get; set; }

    /// <summary>
    /// Error message if applicable.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// URI of created resource for return with "201 Created" response.
    /// </summary>
    public string AudioId { get; set; } = string.Empty;
}