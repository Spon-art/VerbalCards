using System.Net;
using System.Net.Http.Headers;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MongoDB.Driver.Linq;

namespace VerbalCards.Services;

public class MongoAudioService : IAudioService
{
    private readonly IMongoDatabase _db;
    private readonly GridFSBucket _bucket;
    private readonly HttpClient _httpClient;

    public MongoAudioService(IMongoDatabase database, HttpClient httpClient)
    {
        _db = database;
        _httpClient = httpClient;
        _bucket = new GridFSBucket(database);
    }
    
    public async Task<GetAudioResult> GetAudioAsync(string audioId)
    {
        try
        {
            var cts = new CancellationTokenSource(10000);
            var id = new ObjectId(audioId);

            var gridStream = await _bucket.OpenDownloadStreamAsync(id, null, cts.Token);
            
            return new GetAudioResult
            {
                StatusCode = 200,
                Stream = gridStream,
                Filename = gridStream.FileInfo.Filename,
                FileLength = gridStream.FileInfo.Length,
                ContentType = gridStream.FileInfo.Metadata.GetElement("MediaType").Value.AsString,
            };
        }
        catch (GridFSFileNotFoundException)
        {
            return new GetAudioResult
            {
                StatusCode = 404,
                Error = "Audio not found",
            };
        }
        catch (FormatException)
        {
            return new GetAudioResult
            {
                StatusCode = 400,
                Error = "Invalid ObjectID format",
            };
        }
    }

    public Task<HttpStatusCode> DeleteAudioAsync(string audioId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<AudioPlaylistItem>> GetPlaylistAsync()
    {
        var filter = Builders<GridFSFileInfo>.Filter.Empty;
        var cursor = await _bucket.FindAsync(filter);
        var files = await cursor.ToListAsync();

        return files.Select(f => new AudioPlaylistItem
        {
            Id = f.Id.ToString(),
            Filename = f.Filename,
            ContentType = f.Metadata["MediaType"].AsString
        }).ToList();
    }
    
    public async Task<string?> TranscribeAsync(string audioId)
    {
        var audioResult = await GetAudioAsync(audioId);

        if (audioResult.StatusCode != 200 || audioResult.Stream == null)
            return null;

        using var content = new MultipartFormDataContent();
        await using var stream = audioResult.Stream;

        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType =
            new MediaTypeHeaderValue(audioResult.ContentType);

        content.Add(streamContent, "file", audioResult.Filename);

        var response = await _httpClient.PostAsync(
            "http://asr:8000/transcribe",
            content
        );

        if (!response.IsSuccessStatusCode)
            return null;
        
        return await response.Content.ReadAsStringAsync();
    }
    
    public async Task<string?> GetTranscriptionStatusAsync(string jobId)
    {
        var response = await _httpClient.GetAsync(
            $"http://asr:8000/transcribe/status/{jobId}"
        );

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsStringAsync();
    }
}