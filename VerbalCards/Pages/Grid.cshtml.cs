using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VerbalCards.Pages;

public class Grid : PageModel
{
    public required List<AudioItem> audioItems { get; set; } = [new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new()];

    public void OnGet()
    {
        
    }
}

public class AudioItem
{
}