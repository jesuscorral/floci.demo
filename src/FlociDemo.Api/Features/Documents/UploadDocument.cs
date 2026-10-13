using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using FlociDemo.Api.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace FlociDemo.Api.Features.Documents;

/// <summary>
/// Vertical slice: POST /documents
/// Guarda el contenido en S3 y publica un evento "DocumentUploaded" en SQS.
/// </summary>
public static class UploadDocument
{
    public sealed record Request(string FileName, string Content);

    public sealed record Command(string FileName, string Content) : IRequest<Response>;

    public sealed record Response(Guid DocumentId, string Bucket, string Key);

    public sealed record DocumentUploadedEvent(Guid DocumentId, string Bucket, string Key, DateTimeOffset OccurredAt);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FileName)
                .NotEmpty()
                .MaximumLength(200)
                .Must(name => !name.Contains('/') && !name.Contains('\\'))
                .WithMessage("'File Name' no puede contener separadores de ruta.");

            RuleFor(x => x.Content)
                .NotEmpty()
                .MaximumLength(1_000_000);
        }
    }

    internal sealed class Handler(IAmazonS3 s3, IAmazonSQS sqs, IOptions<AwsOptions> options)
        : IRequestHandler<Command, Response>
    {
        public async Task<Response> Handle(Command command, CancellationToken ct)
        {
            var aws = options.Value;
            var documentId = Guid.NewGuid();
            var key = $"documents/{documentId}/{command.FileName}";

            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = aws.BucketName,
                Key = key,
                ContentBody = command.Content,
                ContentType = "text/plain"
            }, ct);

            var queue = await sqs.GetQueueUrlAsync(aws.QueueName, ct);
            var body = JsonSerializer.Serialize(
                new DocumentUploadedEvent(documentId, aws.BucketName, key, DateTimeOffset.UtcNow));

            await sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = queue.QueueUrl,
                MessageBody = body
            }, ct);

            return new Response(documentId, aws.BucketName, key);
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/documents", async (
                Request request,
                IValidator<Command> validator,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new Command(request.FileName, request.Content);

                var validation = await validator.ValidateAsync(command, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var response = await sender.Send(command, ct);
                return Results.Created($"/documents/{response.DocumentId}", response);
            })
            .WithName("UploadDocument");
}
