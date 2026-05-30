using CoreService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IAiService, GeminiAiService>();
builder.Services.AddSingleton<RuleEngineService>();
builder.Services.AddHostedService<KafkaProcessorService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "core-service" }));

app.Run();
