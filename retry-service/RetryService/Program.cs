using RetryService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<RetryWorker>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "retry-service" }));

app.Run();
