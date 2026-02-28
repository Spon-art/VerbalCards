using System.Net;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace VerbalCards.Services;

public class MongoAudioUploader : IAudioUploader
{
    private static readonly HashSet<string> ValidMediaTypes = new(["audio/mpeg", "audio/wav", "audio/x-wav", "audio/wave"]);   
    
    public string OriginalFilename { get; set; } =  string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Stream? InputStream { get; set; }

    private readonly GridFSBucket _bucket;
    
    private bool _storeAsyncCalled;
    
    public MongoAudioUploader(IMongoDatabase database)
    {
        _bucket = new GridFSBucket(database);
    }
    
    public async Task<AudioUploaderResult> StoreAsync()
    {
        if (_storeAsyncCalled)
        {
            throw new InvalidOperationException("StoreAsync can only be called once.");
        }
        _storeAsyncCalled = true;
        
        var problemResult = ValidateInputProperties();
        if (problemResult != null)
        {
            return problemResult;
        }

        return await UploadToStorage();
    }

    private AudioUploaderResult? ValidateInputProperties()
    {
        if (OriginalFilename == string.Empty)
            return new AudioUploaderResult
            {
                StatusCode = 400,
                Error = "Missing filename"
            };

        if (!ValidMediaTypes.Contains(ContentType))
        {
            return new AudioUploaderResult
            {
                StatusCode = 400,
                Error = $"Unsupported media type: {ContentType}"
            };
        }
        
        return null;
    }

    private async Task<AudioUploaderResult> UploadToStorage()
    {
        var fileId = await _bucket.UploadFromStreamAsync(
            filename: OriginalFilename,
            source: InputStream,
            options: new GridFSUploadOptions
            {
                Metadata = new BsonDocument
                {
                    { "MediaType", ContentType }
                }
            }
        );
        return new AudioUploaderResult
        {
            StatusCode = 201,
            AudioId = fileId.ToString()
        };
    }
}