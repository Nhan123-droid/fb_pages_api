using CoreService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IAiService, MockAiService>();
builder.Services.AddSingleton<RuleEngineService>();
builder.Services.AddHostedService<KafkaProcessorService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "core-service" }));

app.Run();
