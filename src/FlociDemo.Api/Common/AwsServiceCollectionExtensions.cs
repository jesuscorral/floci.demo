using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.Extensions.Options;

namespace FlociDemo.Api.Common;

public static class AwsServiceCollectionExtensions
{
    public static IServiceCollection AddAwsServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AwsOptions>().Bind(configuration.GetSection(AwsOptions.SectionName));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AwsOptions>>().Value;

            var config = new AmazonS3Config
            {
                // Floci sirve S3 en modo path-style: http://localhost:4566/bucket/key
                ForcePathStyle = options.UsesEmulator,
                // El SDK v4 añade checksums por defecto; solo los pedimos cuando son obligatorios.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };
            ApplyEndpoint(config, options);

            return options.UsesEmulator
                ? new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config)
                : new AmazonS3Client(config);
        });

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AwsOptions>>().Value;

            var config = new AmazonSQSConfig();
            ApplyEndpoint(config, options);

            return options.UsesEmulator
                ? new AmazonSQSClient(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config)
                : new AmazonSQSClient(config);
        });

        return services;
    }

    private static void ApplyEndpoint(ClientConfig config, AwsOptions options)
    {
        if (options.UsesEmulator)
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = options.Region;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
    }
}
