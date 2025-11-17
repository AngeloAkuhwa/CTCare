using CTCare.Infrastructure.Persistence;

using Microsoft.OpenApi.Models;

namespace CTCare.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwtAndApiKey(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SupportNonNullableReferenceTypes();
            c.MapType<IFormFile>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "binary"
            });

            var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));

            c.SwaggerDoc("v1", new() { Title = "CTCare API", Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}"
            });

            c.AddSecurityDefinition("XApiKey", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = ApiKeyAuthOptions.HeaderName,
                Type = SecuritySchemeType.ApiKey,
                Description = "CTCare API key header"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    Array.Empty<string>()
                },
                {
                    new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "XApiKey" } },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
