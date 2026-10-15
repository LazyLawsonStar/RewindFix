using RewindFix.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

builder.Services.AddRewindFix(options =>
{
    options.ServerUrl = "http://localhost:5000";
    options.SendTimeout = TimeSpan.FromSeconds(3);
    options.QueueCapacity = 1000;
});

var app = builder.Build();

// RewindFix должен стоять достаточно рано,
// чтобы перехватывать ошибки следующих middleware/endpoints.
app.UseRewindFix();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing",
    "Bracing",
    "Chilly",
    "Cool",
    "Mild",
    "Warm",
    "Balmy",
    "Hot",
    "Sweltering",
    "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable
        .Range(1, 5)
        .Select(index =>
            new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                summaries[Random.Shared.Next(summaries.Length)]
            ))
        .ToArray();

    return forecast;
})
.WithName("GetWeatherForecast");



// Специально сломанный endpoint для проверки RewindFix.
app.MapGet("/api/crash", () =>
{
    string? customerName = null;

    return customerName!.ToUpper();
});

app.Run();

record WeatherForecast(
    DateOnly Date,
    int TemperatureC,
    string? Summary)
{
    public int TemperatureF =>
        32 + (int)(TemperatureC / 0.5556);
}

public partial class Program
{
}