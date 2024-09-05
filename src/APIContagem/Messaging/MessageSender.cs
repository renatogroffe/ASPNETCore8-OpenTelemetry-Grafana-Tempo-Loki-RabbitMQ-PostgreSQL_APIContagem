using APIContagem.Tracing;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace APIContagem.Messaging;

public class MessageSender
{
    private readonly ILogger<MessageSender> _logger;
    private readonly IConfiguration _configuration;

    public MessageSender(ILogger<MessageSender> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendMessageAsync<T>(T message)
    {
        using var activity1 = OpenTelemetryExtensions.ActivitySource
            .StartActivity("SendMessageFila")!;

        var queueName = _configuration["RabbitMQ:Queue"];
        var exchangeName = _configuration["RabbitMQ:Exchange"];
        var bodyContent = JsonSerializer.Serialize(message);

        activity1.SetTag("queueName", queueName);
        activity1.SetTag("conteudoAtual", bodyContent);

        try
        {
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(_configuration["RabbitMQ:ConnectionString"]!)
            };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();
                
            await channel.BasicPublishAsync(
                exchange: exchangeName!,
                routingKey: queueName!,
                body: Encoding.UTF8.GetBytes(bodyContent));

            _logger.LogInformation(
                $"RabbitMQ - Envio para a fila {queueName} concluído | " +
                $"{bodyContent}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message publishing failed.");
            throw;
        }
    }
}