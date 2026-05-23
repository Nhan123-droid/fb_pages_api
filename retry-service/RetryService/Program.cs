using RetryService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<RetryWorker>();

var host = builder.Build();

host.Run();
