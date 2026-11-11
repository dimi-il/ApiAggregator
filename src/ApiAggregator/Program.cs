using System.Text.Json.Serialization;
using ApiAggregator.Background;
using ApiAggregator.Clients;
using ApiAggregator.Infrastructure;
using ApiAggregator.Models;
using ApiAggregator.Services;
using ApiAggregator.Statistics;


var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---- Options ----
builder.Services.Configure<AggregationOptions>(config.GetSection("Aggregation"));
builder.Services.Configure<AnomalyOptions>(config.GetSection("Anomaly"));

//// ---- Web API ----
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

////  Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//// ---- Core services ----
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IApiStatsStore, ApiStatsStore>();

builder.Services.AddSingleton<PerformanceAnomalyDetector>();
builder.Services.AddHostedService<PerformanceAnomalyService>();
builder.Services.AddScoped<IAggregationService, AggregationService>();

// ---- External APIs: add a new one by adding one line here ----
builder.Services.AddExternalApiClient<OpenMeteoClient>();
builder.Services.AddExternalApiClient<NagerholidaysClient>();
builder.Services.AddExternalApiClient<FrankfurterClient>();



var app = builder.Build();



app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});

app.MapControllers();

app.Run();
