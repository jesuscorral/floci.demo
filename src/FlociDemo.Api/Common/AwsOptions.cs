namespace FlociDemo.Api.Common;

public sealed class AwsOptions
{
    public const string SectionName = "Aws";

    /// <summary>
    /// Endpoint del emulador (Floci). Si está vacío se usa AWS real
    /// con la cadena de credenciales por defecto.
    /// </summary>
    public string? ServiceUrl { get; set; }

    public string Region { get; set; } = "us-east-1";

    // Floci acepta cualquier credencial no vacía.
    public string AccessKey { get; set; } = "test";
    public string SecretKey { get; set; } = "test";

    public string BucketName { get; set; } = "floci-demo-documents";
    public string QueueName { get; set; } = "floci-demo-document-events";

    public bool UsesEmulator => !string.IsNullOrWhiteSpace(ServiceUrl);
}
