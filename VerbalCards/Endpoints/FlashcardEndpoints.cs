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
        pathBuilder.MapPost("/check", CheckPronunciationHandler);
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

    private static async Task CheckPronunciationHandler(HttpContext httpContext)
    {
        try
        {
            var request = await httpContext.Request.ReadFromJsonAsync<PronunciationCheckRequest>();
            
            if (request == null || string.IsNullOrEmpty(request.UserSpeech) || string.IsNullOrEmpty(request.ExpectedText))
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsJsonAsync(new { error = "Invalid request" });
                return;
            }
            
            var result = CalculateAccuracy(request.UserSpeech, request.ExpectedText);
            
            // If flashcard ID is provided, store the attempt
            if (!string.IsNullOrEmpty(request.FlashcardId))
            {
                await StoreAttempt(request.FlashcardId, result);
            }
            
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(result);
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
                Prompt = "Hej, hvordan har du det?", 
                Hint = "Common greeting",
                ExpectedText = "hej hvordan har du det",
                Difficulty = "Beginner",
                Category = "Greetings",
                Example = "Hello, how are you today?",
                PhoneticHint = "/həˈloʊ, haʊ ɑːr juː/"
            },
            new Flashcard 
            { 
                Id = "2", 
                Prompt = "Det er virkelig godt vejr i dag", 
                Hint = "Talking about weather",
                ExpectedText = "det er virkelig godt vejr i dag",
                Difficulty = "Beginner",
                Category = "Weather",
                Example = "The weather is nice today, let's go for a walk.",
                PhoneticHint = "/ðə ˈweðər ɪz naɪs təˈdeɪ/"
            },
            new Flashcard 
            { 
                Id = "3", 
                Prompt = "Jeg vil gerne bestille en kaffe", 
                Hint = "At a café",
                ExpectedText = "jeg vil gerne bestille en kaffe",
                Difficulty = "Intermediate",
                Category = "Food & Drink",
                Example = "I would like to order coffee with milk, please.",
                PhoneticHint = "/aɪ wʊd laɪk tuː ˈɔːrdər ˈkɔːfi/"
            },
            new Flashcard 
            { 
                Id = "4", 
                Prompt = "Kan du hjælpe mig?", 
                Hint = "Asking for assistance",
                ExpectedText = "kan du hjælpe mig",
                Difficulty = "Beginner",
                Category = "Requests",
                Example = "Can you help me please? I'm lost.",
                PhoneticHint = "/kæn juː help miː pliːz/"
            },
            new Flashcard 
            { 
                Id = "5", 
                Prompt = "Hvornår er mødet?", 
                Hint = "Asking about schedule",
                ExpectedText = "hvornår er mødet",
                Difficulty = "Intermediate",
                Category = "Work",
                Example = "What time is the meeting tomorrow?",
                PhoneticHint = "/wʌt taɪm ɪz ðə ˈmiːtɪŋ/"
            }
        };
    }

    private static async Task<Flashcard> GetFlashcardById(string id)
    {
        // In a real app, this would query a database
        return GetFlashcardCollection().FirstOrDefault(f => f.Id == id);
    }

    private static async Task StoreAttempt(string flashcardId, PronunciationCheckResult result)
    {
        // In a real app, this would store the attempt in a database
        // You could use MongoDB to track user progress
        // For now, just log it
        Console.WriteLine($"Attempt stored for flashcard {flashcardId}: Accuracy {result.Accuracy}%");
    }

    private static PronunciationCheckResult CalculateAccuracy(string userSpeech, string expectedText)
    {
        userSpeech = userSpeech?.ToLower().Trim() ?? "";
        expectedText = expectedText?.ToLower().Trim() ?? "";
        
        // Remove punctuation for better comparison
        userSpeech = RemovePunctuation(userSpeech);
        expectedText = RemovePunctuation(expectedText);
        
        var userWords = userSpeech.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expectedWords = expectedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var wordMatches = new List<WordMatch>();
        int correctWords = 0;
        var matchedIndices = new HashSet<int>();
        
        // First pass: exact matches
        for (int i = 0; i < expectedWords.Length; i++)
        {
            var expectedWord = expectedWords[i];
            var exactMatchIndex = -1;
            
            for (int j = 0; j < userWords.Length; j++)
            {
                if (!matchedIndices.Contains(j) && userWords[j] == expectedWord)
                {
                    exactMatchIndex = j;
                    break;
                }
            }
            
            if (exactMatchIndex >= 0)
            {
                correctWords++;
                matchedIndices.Add(exactMatchIndex);
                wordMatches.Add(new WordMatch 
                { 
                    Word = expectedWord, 
                    IsCorrect = true,
                    Position = i
                });
            }
            else
            {
                // Second pass: fuzzy matches
                var fuzzyMatch = FindFuzzyMatch(expectedWord, userWords, matchedIndices);
                
                if (fuzzyMatch.index >= 0)
                {
                    correctWords++;
                    matchedIndices.Add(fuzzyMatch.index);
                    wordMatches.Add(new WordMatch 
                    { 
                        Word = expectedWord, 
                        IsCorrect = true,
                        SuggestedWord = fuzzyMatch.word,
                        Position = i
                    });
                }
                else
                {
                    wordMatches.Add(new WordMatch 
                    { 
                        Word = expectedWord, 
                        IsCorrect = false,
                        SuggestedWord = "?",
                        Position = i
                    });
                }
            }
        }
        
        // Calculate accuracy
        int accuracy = expectedWords.Length > 0 
            ? (int)Math.Round((double)correctWords / expectedWords.Length * 100) 
            : 0;
        
        // Add bonus for correct word order (up to 15%)
        int orderBonus = CalculateOrderBonus(userWords, expectedWords, matchedIndices);
        accuracy = Math.Min(100, accuracy + orderBonus);
        
        // Generate feedback
        string feedback = GetFeedbackMessage(accuracy, correctWords, expectedWords.Length);
        string detailedAnalysis = GenerateDetailedAnalysis(accuracy, correctWords, expectedWords.Length, wordMatches);
        
        return new PronunciationCheckResult
        {
            Accuracy = accuracy,
            Feedback = feedback,
            WordMatches = wordMatches,
            DetailedAnalysis = detailedAnalysis
        };
    }

    private static string RemovePunctuation(string text)
    {
        return new string(text.Where(c => !char.IsPunctuation(c) || c == '\'').ToArray());
    }

    private static (int index, string word) FindFuzzyMatch(string target, string[] userWords, HashSet<int> matchedIndices)
    {
        for (int i = 0; i < userWords.Length; i++)
        {
            if (!matchedIndices.Contains(i))
            {
                var userWord = userWords[i];
                
                // Check if words are similar
                if (userWord.Length > 2)
                {
                    if (userWord.Contains(target) || target.Contains(userWord))
                    {
                        return (i, userWord);
                    }
                    
                    if (LevenshteinDistance(userWord, target) <= 2)
                    {
                        return (i, userWord);
                    }
                    
                    // Check for common pronunciation variations
                    if (IsPronunciationVariant(userWord, target))
                    {
                        return (i, userWord);
                    }
                }
            }
        }
        
        return (-1, null);
    }

private static bool IsPronunciationVariant(string word1, string word2)
{
    // Normalize the words for comparison
    word1 = word1.ToLowerInvariant().Trim();
    word2 = word2.ToLowerInvariant().Trim();
    
    if (word1 == word2) return true;
    
    // Common Danish pronunciation variations
    var variations = new Dictionary<string, string[]>
    {
        // Soft D (blødt d) - often pronounced like 'th' in English or silent
        { "mad", new[] { "ma", "math" } },
        { "med", new[] { "me", "meth" } },
        { "god", new[] { "go", "goth" } },
        { "rød", new[] { "rø", "røth" } },
        { "sød", new[] { "sø", "søth" } },
        { "tid", new[] { "ti", "tith" } },
        { "død", new[] { "dø", "døth" } },
        { "bred", new[] { "bre", "breth" } },
        { "glad", new[] { "gla", "glath" } },
        
        // Stød (glottal stop) variations
        { "hun", new[] { "hu'n", "hun" } },
        { "han", new[] { "ha'n", "han" } },
        { "mand", new[] { "man", "ma'n" } },
        { "hund", new[] { "hun", "hu'n" } },
        { "ven", new[] { "ve'n", "ven" } },
        { "pen", new[] { "pe'n", "pen" } },
        
        // Vowel variations - Danish vowels can be tricky
        { "øl", new[] { "øl", "ul" } },
        { "øv", new[] { "øv", "uv" } },
        { "år", new[] { "år", "or" } },
        { "æg", new[] { "æg", "eg" } },
        { "øje", new[] { "øje", "øj", "øi" } },
        
        // Common mispronunciations of Danish sounds
        { "rav", new[] { "rav", "rau" } }, // 'v' often sounds like 'u'
        { "hav", new[] { "hav", "hau" } },
        { "sov", new[] { "sov", "sou" } },
        { "giv", new[] { "giv", "giu" } },
        
        // The Danish 'r' - often soft or vocalized
        { "rød", new[] { "rød", "øød" } }, // 'r' can be very soft
        { "grøn", new[] { "grøn", "gøn" } },
        { "brød", new[] { "brød", "bød" } },
        { "mor", new[] { "mor", "mo" } },
        { "far", new[] { "far", "fa" } },
        { "bror", new[] { "bror", "bro" } },
        
        // Common words with silent letters
        { "kage", new[] { "kage", "kae" } },
        { "tage", new[] { "tage", "tae" } },
        { "bage", new[] { "bage", "bae" } },
        { "sige", new[] { "sige", "si" } },
        { "lige", new[] { "lige", "li" } },
        { "mig", new[] { "mig", "mi" } },
        { "dig", new[] { "dig", "di" } },
        { "sig", new[] { "sig", "si" } },
        
        // Common contractions in spoken Danish
        { "det er", new[] { "der", "det er" } },
        { "hvad er", new[] { "hva", "hvader" } },
        { "ikke", new[] { "ik", "icke" } },
        { "også", new[] { "os", "osa" } },
        { "selv", new[] { "sel", "sæl" } },
        
        // Numbers - often mispronounced
        { "en", new[] { "en", "n" } },
        { "to", new[] { "to", "too" } },
        { "tre", new[] { "tre", "tray" } },
        { "fire", new[] { "fire", "fir" } },
        { "fem", new[] { "fem", "fæm" } },
        { "seks", new[] { "seks", "sæks" } },
        { "syv", new[] { "syv", "syw" } },
        { "otte", new[] { "otte", "odda" } },
        { "ni", new[] { "ni", "ny" } },
        { "ti", new[] { "ti", "ty" } },
        
        // Common greetings and phrases
        { "hej", new[] { "hej", "hai", "hey" } },
        { "farvel", new[] { "farvel", "favel" } },
        { "tak", new[] { "tak", "ta" } },
        { "undskyld", new[] { "undskyld", "uskyld" } },
        { "velkommen", new[] { "velkommen", "velkom" } },
        
        // Days of the week
        { "mandag", new[] { "mandag", "manda" } },
        { "tirsdag", new[] { "tirsdag", "tisdag" } },
        { "onsdag", new[] { "onsdag", "onsda" } },
        { "torsdag", new[] { "torsdag", "tosdag" } },
        { "fredag", new[] { "fredag", "freda" } },
        { "lørdag", new[] { "lørdag", "løda" } },
        { "søndag", new[] { "søndag", "sønda" } },
        
        // Common verbs
        { "spise", new[] { "spise", "spis" } },
        { "drikke", new[] { "drikke", "drik" } },
        { "løbe", new[] { "løbe", "løb" } },
        { "gå", new[] { "gå", "go" } },
        { "komme", new[] { "komme", "kom" } },
        { "se", new[] { "se", "si" } },
        { "høre", new[] { "høre", "hør" } },
        
        // Food and drink
        { "smørrebrød", new[] { "smørrebrød", "smørbrød" } },
        { "frikadelle", new[] { "frikadelle", "frikadælle" } },
        { "kartoffel", new[] { "kartoffel", "kartofl" } },
        { "æble", new[] { "æble", "æbel" } },
        { "banan", new[] { "banan", "banan" } },
        
        // Places and common words
        { "København", new[] { "København", "Københaun" } },
        { "Danmark", new[] { "Danmark", "Danmak" } },
        { "dansk", new[] { "dansk", "dansg" } },
        { "skole", new[] { "skole", "skol" } },
        { "hus", new[] { "hus", "huus" } },
        { "bil", new[] { "bil", "biil" } }
    };
    
    // Check direct variations
    if (variations.ContainsKey(word1) && variations[word1].Contains(word2))
        return true;
    
    if (variations.ContainsKey(word2) && variations[word2].Contains(word1))
        return true;
    
    // Handle common patterns for soft D at the end of words
    if (word1.EndsWith("d") && (word2 == word1.TrimEnd('d') || word2 == word1.TrimEnd('d') + "th"))
        return true;
    
    if (word2.EndsWith("d") && (word1 == word2.TrimEnd('d') || word1 == word2.TrimEnd('d') + "th"))
        return true;
    
    // Handle silent 'e' at the end (common in Danish)
    if (word1.EndsWith("e") && word2 == word1.TrimEnd('e'))
        return true;
    
    if (word2.EndsWith("e") && word1 == word2.TrimEnd('e'))
        return true;
    
    // Handle 'v' sounding like 'u' at the end
    if (word1.EndsWith("v") && word2 == word1.TrimEnd('v') + "u")
        return true;
    
    if (word2.EndsWith("v") && word1 == word2.TrimEnd('v') + "u")
        return true;
    
    // Handle soft 'd' after vowels
    string[] vowels = { "a", "e", "i", "o", "u", "æ", "ø", "å" };
    foreach (var vowel in vowels)
    {
        string pattern = vowel + "d";
        if (word1.EndsWith(pattern) && (word2 == word1.TrimEnd('d') || word2 == word1.Replace("d", "th")))
            return true;
        
        if (word2.EndsWith(pattern) && (word1 == word2.TrimEnd('d') || word1 == word2.Replace("d", "th")))
            return true;
    }
    
    return false;
}

    private static int CalculateOrderBonus(string[] userWords, string[] expectedWords, HashSet<int> matchedIndices)
    {
        int bonus = 0;
        var matchedWordsList = matchedIndices.OrderBy(i => i).ToList();
        
        for (int i = 0; i < matchedWordsList.Count - 1; i++)
        {
            if (matchedWordsList[i + 1] == matchedWordsList[i] + 1)
            {
                bonus += 2; // Words in correct sequence
            }
        }
        
        return Math.Min(15, bonus);
    }

    private static string GetFeedbackMessage(int accuracy, int correctWords, int totalWords)
    {
        if (accuracy >= 95) return "Outstanding! Perfect pronunciation! 🌟";
        if (accuracy >= 85) return "Excellent! Very clear! ✨";
        if (accuracy >= 70) return "Good job! Keep practicing! 👍";
        if (accuracy >= 50) return "Not bad! Focus on problem words 🎯";
        if (accuracy >= 30) return "Keep trying! Listen carefully 📚";
        if (accuracy >= 10) return "Start slowly, say each word clearly 🗣️";
        return "Try again, take your time with each word 💪";
    }

    private static string GenerateDetailedAnalysis(int accuracy, int correctWords, int totalWords, List<WordMatch> wordMatches)
    {
        if (totalWords == 0) return "No words to compare";
        
        var incorrectWords = wordMatches.Where(w => !w.IsCorrect).ToList();
        
        if (incorrectWords.Count == 0)
            return $"Perfect! All {totalWords} words pronounced correctly!";
        
        if (incorrectWords.Count == 1)
            return $"Almost perfect! Just one word to work on: '{incorrectWords[0].Word}'";
        
        var problemWords = string.Join(", ", incorrectWords.Take(3).Select(w => $"'{w.Word}'"));
        if (incorrectWords.Count > 3)
            problemWords += $" and {incorrectWords.Count - 3} more";
        
        return $"You got {correctWords} out of {totalWords} words right. Focus on: {problemWords}";
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++);
        for (int j = 0; j <= m; d[0, j] = j++);

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
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

public class PronunciationCheckRequest
{
    public string UserSpeech { get; set; }
    public string ExpectedText { get; set; }
    public string FlashcardId { get; set; }
}

public class PronunciationCheckResult
{
    public int Accuracy { get; set; }
    public string Feedback { get; set; }
    public List<WordMatch> WordMatches { get; set; }
    public string DetailedAnalysis { get; set; }
}

public class WordMatch
{
    public string Word { get; set; }
    public bool IsCorrect { get; set; }
    public string SuggestedWord { get; set; }
    public int Position { get; set; }
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

// Variation af LevenshteinDistance der tager højde for æ, ø, å besvær. Ved ikke om den fungerer. Ved ikke om parakeet kun kan finde på at matche med danske ord, eller om den prøver at matche med alt muligt hvis den er i tvivl såsom 'ö'
/*
private static int LevenshteinDistance(string s, string t)
   {
       int n = s.Length;
       int m = t.Length;
       int[,] d = new int[n + 1, m + 1];
   
       if (n == 0) return m;
       if (m == 0) return n;
   
       for (int i = 0; i <= n; d[i, 0] = i++);
       for (int j = 0; j <= m; d[0, j] = j++);
   
       for (int i = 1; i <= n; i++)
       {
           for (int j = 1; j <= m; j++)
           {
               int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
               
               // Make Danish characters more forgiving
               if (!cost == 0)
               {
                   // Treat æ/ä, ø/ö, å/aa as similar
                   if ((s[i - 1] == 'æ' && t[j - 1] == 'ä') ||
                       (s[i - 1] == 'ä' && t[j - 1] == 'æ') ||
                       (s[i - 1] == 'ø' && t[j - 1] == 'ö') ||
                       (s[i - 1] == 'ö' && t[j - 1] == 'ø') ||
                       (s[i - 1] == 'å' && (t[j - 1] == 'a' || t[j - 1] == 'ä')) ||
                       (s[i - 1] == 'a' && t[j - 1] == 'å'))
                   {
                       cost = 0; // Treat as match for pronunciation purposes
                   }
               }
               
               d[i, j] = Math.Min(
                   Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                   d[i - 1, j - 1] + cost);
           }
       }
       return d[n, m];
   }
*/