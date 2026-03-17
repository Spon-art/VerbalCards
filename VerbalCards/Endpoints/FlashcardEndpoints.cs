using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using VerbalCards.Services;

namespace VerbalCards.Endpoints;

public static class FlashcardEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder pathBuilder)
    {
        pathBuilder.MapPost("/transcribe", TranscribeAudioHandler);
        pathBuilder.MapGet("/", GetFlashcardsHandler);
        pathBuilder.MapGet("/{id}", GetFlashcardByIdHandler);
        pathBuilder.MapGet("/categories", GetCategoriesHandler);
    }

    private static async Task TranscribeAudioHandler(
        HttpContext httpContext,
        [FromServices] MongoAudioUploader audioUploader,
        [FromServices] MongoAudioService audioService,
        [FromServices] IAsrService asrService)
    {
        try
        {
            // Read the file from the request
            var form = await httpContext.Request.ReadFormAsync();
            var file = form.Files["file"];
            
            if (file == null || file.Length == 0)
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsJsonAsync(new { error = "No audio file provided" });
                return;
            }

            // Use MongoAudioUploader to store the audio
            audioUploader.OriginalFilename = file.FileName;
            audioUploader.ContentType = file.ContentType;
            audioUploader.InputStream = file.OpenReadStream();

            var uploaderResult = await audioUploader.StoreAsync();
            
            if (uploaderResult.StatusCode != 201)
            {
                httpContext.Response.StatusCode = uploaderResult.StatusCode;
                await httpContext.Response.WriteAsJsonAsync(new { error = uploaderResult.Error ?? "Upload failed" });
                return;
            }

            // Now retrieve the audio for transcription
            var audioResult = await audioService.GetAudioAsync(uploaderResult.AudioId);
            
            if (audioResult.StatusCode != 200)
            {
                httpContext.Response.StatusCode = audioResult.StatusCode;
                await httpContext.Response.WriteAsJsonAsync(new { error = audioResult.Error ?? "Failed to retrieve audio" });
                return;
            }

            // Transcribe the audio using ASR service
            var transcription = await asrService.TranscribeAsync(audioResult.Stream);
            
            // Get the flashcard context if provided
            var flashcardId = form["flashcardId"].FirstOrDefault();
            Flashcard currentCard = null;
            
            if (!string.IsNullOrEmpty(flashcardId))
            {
                currentCard = await GetFlashcardById(flashcardId);
            }

            httpContext.Response.StatusCode = 200;
            await httpContext.Response.WriteAsJsonAsync(new 
            { 
                transcription = transcription,
                audioId = uploaderResult.AudioId,
                flashcard = currentCard
            });
        }
        catch (Exception ex)
        {
            httpContext.Response.StatusCode = 500;
            await httpContext.Response.WriteAsJsonAsync(new { error = $"Transcription failed: {ex.Message}" });
        }
    }

    private static async Task GetFlashcardsHandler(HttpContext httpContext)
    {
        try
        {
            var category = httpContext.Request.Query["category"].FirstOrDefault();
            var difficulty = httpContext.Request.Query["difficulty"].FirstOrDefault();
            
            var flashcards = GetFlashcardCollection();
            
            // Apply filters if provided
            if (!string.IsNullOrEmpty(category))
            {
                flashcards = flashcards.Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            if (!string.IsNullOrEmpty(difficulty))
            {
                flashcards = flashcards.Where(f => f.Difficulty.Equals(difficulty, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(flashcards);
        }
        catch (Exception ex)
        {
            httpContext.Response.StatusCode = 500;
            await httpContext.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }

    private static async Task GetFlashcardByIdHandler(HttpContext httpContext)
    {
        try
        {
            var id = httpContext.Request.RouteValues["id"]?.ToString();
            
            if (string.IsNullOrEmpty(id))
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsJsonAsync(new { error = "Flashcard ID is required" });
                return;
            }
            
            var flashcard = await GetFlashcardById(id);
            
            if (flashcard == null)
            {
                httpContext.Response.StatusCode = 404;
                await httpContext.Response.WriteAsJsonAsync(new { error = "Flashcard not found" });
                return;
            }
            
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(flashcard);
        }
        catch (Exception ex)
        {
            httpContext.Response.StatusCode = 500;
            await httpContext.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }

    private static async Task GetCategoriesHandler(HttpContext httpContext)
    {
        var flashcards = GetFlashcardCollection();
        var categories = flashcards
            .GroupBy(f => f.Category)
            .Select(g => new CategoryInfo 
            { 
                Name = g.Key, 
                Count = g.Count(),
                Difficulties = g.Select(f => f.Difficulty).Distinct().ToList()
            })
            .ToList();
        
        await httpContext.Response.WriteAsJsonAsync(categories);
    }

    // Helper methods
    private static List<Flashcard> GetFlashcardCollection()
    {
        // In a real app, this would come from a database
        // For now, return a default set
        return new List<Flashcard>
        {
            new Flashcard 
            { 
                Id = "1", 
                Prompt = "Hello, how are you?", 
                Hint = "Hej, hvordan har du det",
                ExpectedText = "hej hvordan har du det",
                Difficulty = "Beginner",
                Category = "Greetings",
            },
            new Flashcard 
            { 
                Id = "2", 
                Prompt = "It is really nice weather today", 
                Hint = "Det er virkelig godt vejr i dag",
                ExpectedText = "det er virkelig godt vejr i dag",
                Difficulty = "Beginner",
                Category = "Weather",
            },
            new Flashcard 
            { 
                Id = "3", 
                Prompt = "I want to order a coffee", 
                Hint = "Jeg vil gerne bestille en kaffe",
                ExpectedText = "jeg vil gerne bestille en kaffe",
                Difficulty = "Intermediate",
                Category = "Food & Drink",
            },
            new Flashcard 
            { 
                Id = "4", 
                Prompt = "Can you help me?", 
                Hint = "Kan du hjælpe mig?",
                ExpectedText = "kan du hjælpe mig",
                Difficulty = "Beginner",
                Category = "Requests",
            },
            new Flashcard 
            { 
                Id = "5", 
                Prompt = "When is the meeting?", 
                Hint = "Hvornår er mødet?",
                ExpectedText = "hvornår er mødet",
                Difficulty = "Intermediate",
                Category = "Work",
            },
            new Flashcard
            {
                Id = "6",
                Prompt = "Coffee",
                Hint = "kaffe",
                ExpectedText = "kaffe",
                Difficulty = "Beginner",
                Category = "Food & Drink"
            },
            new Flashcard
            {
                Id = "7",
                Prompt = "Cake",
                Hint = "kage",
                ExpectedText = "kage",
                Difficulty = "Beginner",
                Category = "Food & Drink"
            },
            new Flashcard
            {
                Id = "8",
                Prompt = "Sausage",
                Hint = "pølse",
                ExpectedText = "pølse",
                Difficulty = "Beginner",
                Category = "Food & Drink"
            }
        };
    }

    private static async Task<Flashcard> GetFlashcardById(string id)
    {
        // In a real app, this would query a database
        return GetFlashcardCollection().FirstOrDefault(f => f.Id == id);
    }
}

// Models
public class Flashcard
{
    public string Id { get; set; }
    public string Prompt { get; set; }
    public string Hint { get; set; }
    public string ExpectedText { get; set; }
    public string Difficulty { get; set; }
    public string Category { get; set; }
    public string Example { get; set; }
    public string PhoneticHint { get; set; }
}

public class CategoryInfo
{
    public string Name { get; set; }
    public int Count { get; set; }
    public List<string> Difficulties { get; set; }
}

// Service interface for ASR
public interface IAsrService
{
    Task<string> TranscribeAsync(Stream audioStream);
}

// Example implementation of ASR service
public class AsrService : IAsrService
{
    private readonly HttpClient _httpClient;
    
    public AsrService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<string> TranscribeAsync(Stream audioStream)
    {
        // Call your ASR model endpoint
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(audioStream);
        content.Add(streamContent, "file", "recording.wav");
        
        var response = await _httpClient.PostAsync("http://localhost:8000/transcribe", content);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<AsrResponse>();
        return result?.Transcription ?? "";
    }
}

public class AsrResponse
{
    public string Transcription { get; set; }
}