using Microsoft.AspNetCore.Mvc.RazorPages;
using VerbalCards.Services;

namespace VerbalCards.Pages;

public class Grid : PageModel
{
    private readonly IAudioService _audioService;

    public Grid(IAudioService audioService)
    {
        _audioService = audioService;
    }
    
    public List<AudioPlaylistItem> AudioItems { get; set; } = [];

    public async Task OnGetAsync()
    {
        AudioItems = await _audioService.GetPlaylistAsync();
    }
}
