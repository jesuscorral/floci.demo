using FlociDemo.Api.Common;
using FlociDemo.Api.Features.Documents;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAwsServices(builder.Configuration);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
UploadDocument.MapEndpoint(app);

app.Run();

// Necesario para WebApplicationFactory<Program> en los tests de integración.
public partial class Program;
