using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3;
using Amazon.SQS;
using Amazon.SQS.Model;
using FlociDemo.Api.Common;
using FlociDemo.Api.Features.Documents;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FlociDemo.Tests;

/// <summary>
/// Tests de integración contra Floci (docker compose up -d) con la infraestructura
/// ya creada por Terraform (carpeta infra/). No necesitan cuenta de AWS.
/// </summary>
public sealed class UploadDocumentTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly IAmazonS3 _s3;
    private readonly IAmazonSQS _sqs;
    private readonly AwsOptions _aws;

    public UploadDocumentTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _s3 = factory.Services.GetRequiredService<IAmazonS3>();
        _sqs = factory.Services.GetRequiredService<IAmazonSQS>();
        _aws = factory.Services.GetRequiredService<IOptions<AwsOptions>>().Value;
    }

    public async Task InitializeAsync()
    {
        // Cola limpia para que los tests no dependan de ejecuciones anteriores.
        var queueUrl = await GetQueueUrlAsync();
        await _sqs.PurgeQueueAsync(queueUrl);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Upload_StoresObjectInS3_AndPublishesEventToSqs()
    {
        var request = new UploadDocument.Request("hola-floci.txt", "AWS en mi portátil. Coste: 0 €");

        var httpResponse = await _client.PostAsJsonAsync("/documents", request);

        httpResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var response = await httpResponse.Content.ReadFromJsonAsync<UploadDocument.Response>();
        response.Should().NotBeNull();
        response!.Key.Should().Be($"documents/{response.DocumentId}/hola-floci.txt");

        // 1) El objeto existe en S3 con el contenido enviado.
        using var s3Object = await _s3.GetObjectAsync(_aws.BucketName, response.Key);
        using var reader = new StreamReader(s3Object.ResponseStream);
        (await reader.ReadToEndAsync()).Should().Be(request.Content);

        // 2) El evento llegó a SQS.
        var evt = await WaitForEventAsync(response.DocumentId);
        evt.Should().NotBeNull("el handler debe publicar un evento DocumentUploaded");
        evt!.Bucket.Should().Be(_aws.BucketName);
        evt.Key.Should().Be(response.Key);
    }

    [Theory]
    [InlineData("", "contenido")]
    [InlineData("a/b.txt", "contenido")]
    [InlineData("..\\secreto.txt", "contenido")]
    [InlineData("ok.txt", "")]
    public async Task Upload_WithInvalidRequest_ReturnsValidationProblem(string fileName, string content)
    {
        var httpResponse = await _client.PostAsJsonAsync("/documents", new UploadDocument.Request(fileName, content));

        httpResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<string> GetQueueUrlAsync() =>
        (await _sqs.GetQueueUrlAsync(_aws.QueueName)).QueueUrl;

    private async Task<UploadDocument.DocumentUploadedEvent?> WaitForEventAsync(Guid documentId)
    {
        var queueUrl = await GetQueueUrlAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var received = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 2
            });

            foreach (var message in received.Messages ?? [])
            {
                var evt = JsonSerializer.Deserialize<UploadDocument.DocumentUploadedEvent>(message.Body);
                if (evt?.DocumentId == documentId)
                {
                    await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle);
                    return evt;
                }
            }
        }

        return null;
    }
}
