using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.SwaggerGen;
using WebApi.Core.Auth.Filters.Operation;
using WebApi.Core.Auth.Schemes.ImportKey;
using WebApi.Core.Auth.Schemes.Token;
using WebApi.Core.Auth.Services;
using WebApi.Core.Auth.Services.Hosted;
using SessionOptions = WebApi.Core.Auth.Services.Options.SessionOptions;

namespace WebApi.Core.Auth;

public static class AddServicesExtension
{
    /// <summary>
    ///     Adds all auth related services that are likely to be used either directly or indirectly in some features.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app configuration.</param>
    public static void AddAuthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SessionOptions>(configuration.GetSection("Session"));
        services.AddScoped<SessionManager>();

        services.AddHostedService<ScheduledAuthJobsService>();

        services.AddAuthentication()
            .AddScheme<TokenAuthenticationSchemeOptions, TokenAuthenticationSchemeHandler>(AuthConstants.TokenScheme,
                o => { }) //TODO: Add HubsBasePath once there are hubs
            .AddScheme<ImportKeyAuthenticationSchemeOptions, ImportKeyAuthenticationSchemeHandler>(
                AuthConstants.ImportKeyScheme, o => { });

        services.AddAuthorization(o =>
        {
            o.DefaultPolicy = new AuthorizationPolicyBuilder().AddAuthenticationSchemes(AuthConstants.TokenScheme)
                .RequireAuthenticatedUser().Build(); //TODO: Replace with the policy of Warden
        });
    }

    public static void AddAuthOperationFilters(this SwaggerGenOptions options)
    {
        options.OperationFilter<RequireImportKeyOperationFilter>();
        options.OperationFilter<AuthorizeOperationFilter>();
    }
}