using APIContagem.Data;
using APIContagem.Messaging;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Npgsql;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Enrichers.Span;
using OpenTelemetry.Context.Propagation;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

var builder = WebApplication.CreateBuilder(args);

// Documentacao do OpenTelemetry:
// https://opentelemetry.io/docs/instrumentation/net/getting-started/

// Integracao do OpenTelemetry com Jaeger e tambem Grafana Tempo em .NET:
// https://github.com/open-telemetry/opentelemetry-dotnet/tree/e330e57b04fa3e51fe5d63b52bfff891fb5b7961/docs/trace/getting-started-jaeger#collect-and-visualize-traces-using-jaeger

// Exemplo que serviu de base para a implementacao desta nova aplicacao:
// https://github.com/renatogroffe/DistributedTracing-OpenTelemetry-Jaeger-DotNet7-RabbitMQ-Http-BDs/tree/main/RabbitMQ/APIContagem

// Integracao de OpenTelemetry com RabbitMQ:
// https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/main/projects/RabbitMQ.Client.OpenTelemetry/README.md

builder.Services.AddScoped<ContagemRepository>();

var compositeTextMapPropagator = new CompositeTextMapPropagator(new TextMapPropagator[]
{
    new TraceContextPropagator(),
    new BaggagePropagator()
});

OpenTelemetry.Sdk.SetDefaultTextMapPropagator(compositeTextMapPropagator);

builder.Services.AddSerilog(new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.GrafanaLoki(
        builder.Configuration["Loki:Uri"]!,
        new List<LokiLabel>()
        {
            new()
            {
                Key = "service_name",
                Value = APIContagem.Tracing.OpenTelemetryExtensions.ServiceName
            },
            new()
            {
                Key = "using_database",
                Value = "true"
            }
        })
    .Enrich.WithSpan(new SpanOptions() { IncludeOperationName = true, IncludeTags = true })
    .CreateLogger());

builder.Services.AddDbContext<ContagemContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("BaseContagem"));
});

builder.Services.AddOpenTelemetry()
    .WithTracing((traceBuilder) =>
    {
        traceBuilder
            .AddSource(APIContagem.Tracing.OpenTelemetryExtensions.ServiceName)
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName: APIContagem.Tracing.OpenTelemetryExtensions.ServiceName,
                        serviceVersion: APIContagem.Tracing.OpenTelemetryExtensions.ServiceVersion))
            .AddAspNetCoreInstrumentation()
            .AddRabbitMQInstrumentation()
            .AddNpgsql()
            .AddOtlpExporter()
            .AddConsoleExporter();
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<MessageSender>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.UseSerilogRequestLogging();

app.MapControllers();

app.Run();