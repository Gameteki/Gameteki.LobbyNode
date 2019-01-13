namespace CrimsonDev.Gameteki.LobbyNode.Helpers
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Security.Claims;
    using System.Text;
    using System.Threading.Tasks;
    using CrimsonDev.Gameteki.Data;
    using CrimsonDev.Gameteki.Data.Models;
    using CrimsonDev.Gameteki.LobbyNode.Config;
    using CrimsonDev.Gameteki.LobbyNode.Scheduler;
    using CrimsonDev.Gameteki.LobbyNode.Services;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.IdentityModel.Tokens;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using Quartz;
    using Quartz.Impl;
    using Quartz.Spi;
    using StackExchange.Redis;

    [ExcludeFromCodeCoverage]
    public static class LobbyExtensions
    {
        public static void AddLobbyBase(this IServiceCollection services, IConfiguration configuration)
        {
            var generalSection = configuration.GetSection("General");
            var generalConfig = generalSection.Get<GametekiLobbyOptions>();
            var tokenOptions = configuration.GetSection("Tokens").Get<AuthTokenOptions>();

            if (generalConfig.DatabaseProvider.ToLower() == "mssql")
            {
                services.AddDbContext<GametekiDbContext>(settings => settings.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
            }
            else
            {
                services.AddDbContext<GametekiDbContext>(settings => settings.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            }

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1).AddJsonOptions(
                options =>
                {
                    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                    options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                });
            services.AddIdentityCore<GametekiUser>(settings =>
            {
                settings.User.RequireUniqueEmail = true;
            }).AddEntityFrameworkStores<GametekiDbContext>().AddDefaultTokenProviders();

            services.AddSignalR().AddStackExchangeRedis(generalConfig.RedisUrl, options =>
            {
                options.Configuration.ChannelPrefix = generalConfig.RedisName;
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(JwtBearerDefaults.AuthenticationScheme, policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireClaim(ClaimTypes.NameIdentifier);
                });
            });

            services.AddAuthentication(settings =>
            {
                settings.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                settings.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = tokenOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = tokenOptions.Issuer,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenOptions.Key)),
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                            {
                                context.Response.Headers.Add("Token-Expired", "true");
                            }

                            return Task.CompletedTask;
                        },
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];

                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                context.Token = context.Request.Query["access_token"];
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(generalConfig.RedisUrl));
            services.AddSingleton<IJobFactory, JobFactory>();
            services.AddSingleton<ILobbyService, LobbyService>();
            services.AddSingleton<NodeHeartbeat>();
            services.Configure<GametekiLobbyOptions>(generalSection);
        }

        public static IApplicationBuilder UseLobby(this IApplicationBuilder app)
        {
            app.UseAuthentication();
            app.UseMvc();

            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().GetAwaiter().GetResult();

            scheduler.JobFactory = app.ApplicationServices.GetService<IJobFactory>();

            var job = JobBuilder.Create<NodeHeartbeat>().WithIdentity("NodeMonitor").Build();
            var trigger = TriggerBuilder
                .Create()
                .WithIdentity("NodeMonitorTrigger")
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(60).RepeatForever())
                .Build();

            scheduler.ScheduleJob(job, trigger);
            scheduler.Start().GetAwaiter().GetResult();

            var lobbyService = app.ApplicationServices.GetService<ILobbyService>();
            lobbyService.Init();

            return app;
        }
    }
}
