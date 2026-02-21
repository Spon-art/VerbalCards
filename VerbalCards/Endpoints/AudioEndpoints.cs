using Microsoft.AspNetCore.Mvc;
using VerbalCards.Services;

namespace VerbalCards.Endpoints;

public static class AudioEndpoints
{
    /// <summary>
    /// Where all the endpoints are initialized to their respective handler. 
    /// </summary>
    /// <param name="pathBuilder"></param>
    public static void MapEndpoints(RouteGroupBuilder pathBuilder)
    {
        // Anti-forgery is disabled. This was decided because the backend will not serve any forms.
        // Anti-forgery measures are covered in the front-end, and by the JWT token protection. 
        // pathBuilder.RequireAuthorization().DisableAntiforgery();
        
        pathBuilder.MapPost("/upload", UploadAudioHandler);
        
        pathBuilder.MapGet("/get/{audioId}", GetAudioHandler);

        //pathBuilder.MapDelete("/delete/{audioId}", DeleteAudioHandler);

        //pathBuilder.MapGet("/get-metadata/{audioId}", GetMetaDataHandler);

        //pathBuilder.MapGet("/filter/{category}", FilterAudioHandler);
    }

    private static async Task<AudioUploaderResult> UploadAudioHandler(
        IFormFile audioFile,
        HttpContext httpContext,
        [FromServices] IAudioUploader audioUploader
    )
    {
        audioUploader.OriginalFilename = audioFile.FileName;
        audioUploader.ContentType = audioFile.ContentType;
        audioUploader.InputStream = audioFile.OpenReadStream();

        var uploaderResult = await audioUploader.StoreAsync();

        httpContext.Response.StatusCode = uploaderResult.StatusCode;
        return uploaderResult;
    }

    private static async Task<IResult> GetAudioHandler(
        [FromRoute] string audioId,
        HttpContext httpContext,
        [FromServices] IAudioService audioService
    )
    {
        var getAudioResult = await audioService.GetAudioAsync(audioId);
        httpContext.Response.StatusCode = getAudioResult.StatusCode;
        return Results.Stream(getAudioResult.Stream, getAudioResult.ContentType);
    }
}