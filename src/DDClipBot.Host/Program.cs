using DDClipBot.Host.Configuration;
using DDClipBot.Host.Endpoints;
using DDClipBot.Host.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord.Hosting.Gateway;
using System;

var builder = WebApplication.CreateBuilder(args);

// Get max file size from configuration (default to 2GB if not specified)
var maxFileSize = builder.Configuration.GetValue<long?>("Upload:MaxFileSizeBytes") ?? (2L * 1024 * 1024 * 1024);

// Configure Kestrel server limits for large file uploads
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = maxFileSize;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
});

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDiscordGateway();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Configure YouTube options from user secrets
builder.Services.Configure<YouTubeOptions>(
    builder.Configuration.GetSection("YouTube"));

// Register application services
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddSingleton<IYouTubeService, YouTubeApiService>();

// Register background workers
builder.Services.AddHostedService<VideoUploadWorker>();
builder.Services.AddHostedService<YouTubeTokenRefreshService>();

// Configure request size limits for video uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxFileSize;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Add CORS for Next.js frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();

// Map endpoint groups
app.MapAuthEndpoints();
app.MapUploadEndpoints();
app.MapDiscordEndpoints();

app.Run();
