using Confluent.Kafka;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<Page_API.Data.AppDbContext>(options => 
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));

// Configure Facebook Options
builder.Services.Configure<Page_API.Models.FacebookOptions>(builder.Configuration.GetSection("Facebook"));
builder.Services.Configure<Page_API.Models.KafkaConsumerOptions>(builder.Configuration.GetSection("KafkaConsumer"));

// Register Facebook Service and HttpClient
builder.Services.AddHttpClient<Page_API.Services.IFacebookService, Page_API.Services.FacebookService>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("Facebook:BaseUrl") ?? "https://graph.facebook.com/v19.0/";
    if (!baseUrl.EndsWith("/")) baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["KafkaProducer:BootstrapServers"] ?? "localhost:9092"
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// Configure lowercase URLs
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddScoped<Page_API.Services.IFacebookEventHandler, Page_API.Services.FacebookEventHandler>();
builder.Services.AddHostedService<Page_API.Services.FacebookEventConsumerService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<Page_API.Data.AppDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
