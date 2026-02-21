using MongoDB.Driver;
using VerbalCards.Endpoints;
using VerbalCards.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery();
builder.Services.AddSession();

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IMongoClient>(sp => 
    new MongoClient(builder.Configuration.GetConnectionString("Mongo"))
);

builder.Services.AddScoped<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var database = client.GetDatabase("AudioDB");
    return database;
});

builder.Services.AddScoped<IAudioService, MongoAudioService>();
builder.Services.AddTransient<IAudioUploader, MongoAudioUploader>();

var app = builder.Build();

app.UseSession();
app.UseStaticFiles();
app.UseRouting();

app.UseAntiforgery();

app.MapRazorPages();

var audioGroup = app.MapGroup("/audio");

if (app.Environment.IsDevelopment())
{
    audioGroup.DisableAntiforgery();
    app.UseSwagger();
    app.UseSwaggerUI();
}

AudioEndpoints.MapEndpoints(audioGroup);

app.UseRouting();

app.Run();
