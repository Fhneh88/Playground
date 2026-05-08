var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/greet/{name}", (string name) =>
{
    return $"Привет, {name}!";
});

app.MapGet("/age/{year}", (int year) =>
{
    int currentAge = DateTime.Now.Year - year;
    return new
    {
        birthYear = year,
        currentAge
    };
});

app.Run();