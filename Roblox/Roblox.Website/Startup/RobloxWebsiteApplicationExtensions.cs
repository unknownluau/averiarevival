using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Roblox;
using Roblox.Services;
using Roblox.Web.Infrastructure.Middleware;
using Roblox.Website.Hubs;
using Roblox.Website.Middleware;

namespace Roblox.Website.Startup;

public static class RobloxWebsiteApplicationExtensions
{
    public static async Task RunDevelopmentBootstrapAsync(this WebApplication app)
    {
#if DEBUG
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var usersService = scope.ServiceProvider.GetRequiredService<UsersService>();
        if (await usersService.CountCreatedUsers(null) != 0)
        {
            return;
        }

        const string username = "ROBLOX";
        const string password = "roblox_dev_pass";
        string applicationId;
        string? joinId;

        try
        {
            applicationId = await usersService.CreateApplication(new Roblox.Dto.Users.CreateUserApplicationRequest
            {
                about = "Signed up",
                socialPresence = "",
                discordId = "1302918658804416553",
                discordUsername = "unknownluau",
                isVerified = true,
                verifiedUrl = null,
                verifiedId = null,
                verificationPhrase = "",
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow,
            });

            joinId = await usersService.ProcessApplication(
                applicationId,
                Configuration.AiUserId,
                Roblox.Dto.Users.UserApplicationStatus.Approved
            );
        }
        catch (Exception)
        {
            joinId = Guid.NewGuid().ToString();
        }

        var result = await usersService.CreateUser(
            username,
            password,
            Roblox.Models.Users.Gender.Male
        );

        await usersService.SetApplicationUserIdByJoinId(joinId!, result.userId);

        await usersService.InsertOrUpdateMembership(
            result.userId,
            Roblox.Models.Users.MembershipType.Premium
        );

        Console.WriteLine("Created dev account: {0}:{1}", username, password);
#endif
    }

    public static void UseRobloxWebsitePipeline(this WebApplication app)
    {
        Roblox.Services.ServiceProvider.Initialize(app.Services);

        app.UseRouting();
        app.UseRobloxRequestServicesScope();

        var prepareResponseForCache = (StaticFileResponseContext ctx) =>
        {
            const int durationInSeconds = 86400 * 365;

            ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                "public,max-age=" + durationInSeconds;

            ctx.Context.Response.Headers.Remove(HeaderNames.LastModified);
        };

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.GetFullPath(
                    Path.Combine(
                        Roblox.Configuration.PublicDirectory,
                        "css",
                        "Roblox"
                    )
                )
            ),
            RequestPath = "/css",
            OnPrepareResponse = prepareResponseForCache,
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.GetFullPath(
                    Path.Combine(
                        Roblox.Configuration.PublicDirectory,
                        "js"
                    )
                )
            ),
            RequestPath = "/js",
            OnPrepareResponse = prepareResponseForCache,
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.GetFullPath(
                    Path.Combine(
                        Roblox.Configuration.PublicDirectory,
                        "UnsecuredContent"
                    )
                )
            ),
            RequestPath = "/UnsecuredContent",
            OnPrepareResponse = prepareResponseForCache,
        });

        if (string.IsNullOrWhiteSpace(Roblox.Configuration.CdnBaseUrl))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.GetFullPath(
                        Roblox.Configuration.ThumbnailsDirectory
                    )
                ),
                RequestPath = "/images/thumbnails",
                OnPrepareResponse = prepareResponseForCache,
            });

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.GetFullPath(
                        Roblox.Configuration.GroupIconsDirectory
                    )
                ),
                RequestPath = "/images/groups",
                OnPrepareResponse = prepareResponseForCache,
            });
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.GetFullPath(
                    Path.Combine(
                        Roblox.Configuration.PublicDirectory,
                        "img"
                    )
                )
            ),
            RequestPath = "/img",
            OnPrepareResponse = prepareResponseForCache,
        });

        app.UseRequestDecompression();
        app.UseRobloxSessionMiddleware();

        if (!Configuration.IsCdnEnabled)
        {
            app.UseMiddleware<ThumbnailMiddleware>(
                Path.GetFullPath(
                    Roblox.Configuration.ThumbnailsDirectory
                )
            );
        }

        app.UseRobloxPlayerCorsMiddleware();
        app.UseRobloxCsrfMiddleware();
        app.UseApplicationGuardMiddleware();
        app.UseExceptionHandler();

        app.UseSwagger();

        app.UseSwaggerUI(c =>
        {
            c.ShowCommonExtensions();

            c.SwaggerEndpoint(
                "/swagger/UserV1/swagger.json",
                "UserV1"
            );
        });

        app.UseWebSockets();
        app.UseTimerMiddleware();
    }

    public static void MapRobloxWebsiteEndpoints(this WebApplication app)
    {
        app.MapControllers();
        app.MapRazorPages();
        app.MapHub<MessageRouterHub>("/v1/router/signalr");
    }
}