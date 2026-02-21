using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using VerbalCards.Services;

namespace VerbalCards.Pages;

public class Upload : PageModel
{
    private readonly HttpClient _http;

    public Upload(HttpClient http)
    {
        _http = http;
    }

    [BindProperty]
    public IFormFile? AudioFile { get; set; }

    public AudioUploaderResult? UploadResult { get; set; }
    
    public void OnGet()
    {
        
    }
    
    public async Task<IActionResult> OnPostAsync()
    {
        if (AudioFile == null)
            return Page();

        using var content = new MultipartFormDataContent();
        using var stream = AudioFile.OpenReadStream();

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(AudioFile.ContentType);

        content.Add(fileContent, "audioFile", AudioFile.FileName);

        var response = await _http.PostAsync("http://localhost:8080/audio/upload", content);

        UploadResult = await response.Content.ReadFromJsonAsync<AudioUploaderResult>();

        return Page();
    }
}