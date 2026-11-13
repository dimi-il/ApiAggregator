using ApiAggregator.Background;
using ApiAggregator.Clients;
using ApiAggregator.Infrastructure;
using ApiAggregator.Models;
using ApiAggregator.Services;
using ApiAggregator.Statistics;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using static System.Net.WebRequestMethods;


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

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1.0",
        Title = "Aggregation API",
        Description = "An API aggregation service that consolidates data from multiple external APIs and provides a unified endpoint to access the aggregated information",
        TermsOfService = new Uri("https://localhost:41205/terms"),
        Contact = new OpenApiContact
        {
            Name = "email Contact",
            Url = new Uri("mailto:tarmac@outlook.com.gr")
        },
        License = new OpenApiLicense
        {
            Name = "Apache License",
            Url = new Uri("https://apache.org")
        }
    });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });
}

app.MapControllers();

app.Run();
