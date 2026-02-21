using System.Net;

namespace VerbalCards.Services;

public interface IAudioService
{
    /// <summary>
    /// Retrieves an audio based on its ID. This will return a result including a file stream.
    /// </summary>
    /// <param name="audioId"></param>
    /// <returns></returns>
    Task<GetAudioResult> GetAudioAsync(string audioId);

    /// <summary>
    /// Deletes an audio based on its ID. This will mark the image as deleted in the database and remove it from DB.
    /// </summary>
    /// <param name="audioId"></param>
    /// <returns></returns>
    Task<HttpStatusCode> DeleteAudioAsync(string audioId);
}

/// <summary>
/// The result of downloading an audio.
/// </summary>
public sealed class GetAudioResult
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
    /// Download stream. Empty stream in case of failure.
    /// </summary>
    public Stream Stream { get; set; } = Stream.Null;

    /// <summary>
    /// Content type. "text/plain" in case of failure.
    /// </summary>
    public string ContentType { get; set; } = "text/plain";
}