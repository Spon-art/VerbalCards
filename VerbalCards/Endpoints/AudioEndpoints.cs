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
}