using System.Net;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace VerbalCards.Services;

public class MongoAudioService : IAudioService
{
    private readonly GridFSBucket _bucket;

    public MongoAudioService(IMongoDatabase database)
    {
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
    
    
}