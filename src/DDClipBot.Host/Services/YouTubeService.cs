using System;
using System.IO;
using System.Threading.Tasks;
using DDClipBot.Host.Configuration;
using DDClipBot.Host.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YouTubeService = Google.Apis.YouTube.v3.YouTubeService;

namespace DDClipBot.Host.Services;

public interface IYouTubeService
{
    Task<string> UploadVideoAsync(VideoUploadJob job);
    Task<bool> IsVideoProcessingCompleteAsync(string videoId);
}

public class YouTubeApiService : IYouTubeService
{
    private readonly YouTubeOptions _options;
    private readonly IJobStore _jobStore;
    private readonly ILogger<YouTubeApiService> _logger;

    public YouTubeApiService(
        IOptions<YouTubeOptions> options,
        IJobStore jobStore,
        ILogger<YouTubeApiService> logger)
    {
        _options = options.Value;
        _jobStore = jobStore;
        _logger = logger;
    }

    public async Task<string> UploadVideoAsync(VideoUploadJob job)
    {
        try
        {
            _logger.LogInformation("Starting YouTube upload for job {JobId}", job.JobId);

            // Create OAuth credential from stored refresh token
            _logger.LogInformation("Creating YouTube credential for job {JobId}", job.JobId);
            var credential = await CreateCredentialAsync();
            _logger.LogInformation("Credential created successfully for job {JobId}", job.JobId);

            var youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _options.ApplicationName
            });

            var video = new Video
            {
                Snippet = new VideoSnippet
                {
                    Title = job.Title,
                    Description = job.Description,
                    Tags = new[] { "gaming", "clip" },
                    CategoryId = "20" // Gaming category
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = "unlisted", // Can be made configurable
                    MadeForKids = false
                }
            };

            job.Status = VideoUploadStatus.Uploading;
            _jobStore.UpdateJob(job);

            using var fileStream = new FileStream(job.FilePath, FileMode.Open, FileAccess.Read);
            var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");
            
            // Attach progress handlers
            videosInsertRequest.ProgressChanged += progress =>
            {
                _logger.LogInformation("Upload progress for job {JobId}: {BytesSent} bytes sent, Status: {Status}",
                    job.JobId, progress.BytesSent, progress.Status);

                if (progress.Status == UploadStatus.Failed)
                {
                    _logger.LogError("Upload failed for job {JobId}: {Exception}", job.JobId, progress.Exception);
                    job.Status = VideoUploadStatus.Failed;
                    job.ErrorMessage = progress.Exception?.Message ?? "Upload failed";
                    _jobStore.UpdateJob(job);
                }
            };
            
            videosInsertRequest.ResponseReceived += uploadedVideo =>
            {
                _logger.LogInformation("Video uploaded successfully for job {JobId}. YouTube ID: {VideoId}",
                    job.JobId, uploadedVideo.Id);
                
                job.YouTubeVideoId = uploadedVideo.Id;
                job.Status = VideoUploadStatus.Processing;
                _jobStore.UpdateJob(job);
            };

            // Start upload asynchronously - this will complete in background
            var uploadResult = await videosInsertRequest.UploadAsync();

            // Check final status
            if (uploadResult.Status == UploadStatus.Failed)
            {
                _logger.LogError(uploadResult.Exception, "Upload failed for job {JobId}", job.JobId);
                job.Status = VideoUploadStatus.Failed;
                job.ErrorMessage = uploadResult.Exception?.Message ?? "Upload failed";
                job.CompletedAt = DateTime.UtcNow;
                _jobStore.UpdateJob(job);
                throw new Exception($"YouTube upload failed: {uploadResult.Exception?.Message}", uploadResult.Exception);
            }

            // If upload completed but ResponseReceived didn't fire, get the video from the result
            if (uploadResult.Status == UploadStatus.Completed)
            {
                var uploadedVideo = videosInsertRequest.ResponseBody;
                if (uploadedVideo != null && !string.IsNullOrEmpty(uploadedVideo.Id))
                {
                    _logger.LogInformation("Upload completed for job {JobId}. YouTube ID: {VideoId}", 
                        job.JobId, uploadedVideo.Id);
                    
                    job.YouTubeVideoId = uploadedVideo.Id;
                    job.Status = VideoUploadStatus.Processing;
                    _jobStore.UpdateJob(job);
                }
                else
                {
                    _logger.LogWarning("Upload completed but no video ID received for job {JobId}", job.JobId);
                }
            }

            return job.JobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating upload for job {JobId}", job.JobId);
            job.Status = VideoUploadStatus.Failed;
            job.ErrorMessage = ex.Message;
            _jobStore.UpdateJob(job);
            throw;
        }
    }

    private Task<UserCredential> CreateCredentialAsync()
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret
            },
            Scopes = new[] { YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.YoutubeReadonly }
        });

        var token = new Google.Apis.Auth.OAuth2.Responses.TokenResponse
        {
            RefreshToken = _options.RefreshToken
        };

        return Task.FromResult(new UserCredential(flow, "user", token));
    }

    public async Task<bool> IsVideoProcessingCompleteAsync(string videoId)
    {
        try
        {
            var credential = await CreateCredentialAsync();
            var youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _options.ApplicationName
            });

            var request = youtubeService.Videos.List("processingDetails,status");
            request.Id = videoId;

            var response = await request.ExecuteAsync();
            
            if (response.Items == null || response.Items.Count == 0)
            {
                _logger.LogWarning("Video {VideoId} not found when checking processing status", videoId);
                return false;
            }

            var video = response.Items[0];
            
            // Check if processing is complete
            var processingStatus = video.ProcessingDetails?.ProcessingStatus;
            var uploadStatus = video.Status?.UploadStatus;

            _logger.LogInformation("Video {VideoId} processing status: {ProcessingStatus}, upload status: {UploadStatus}", 
                videoId, processingStatus, uploadStatus);

            // Video is ready when processing status is "succeeded"
            return processingStatus == "succeeded";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking processing status for video {VideoId}", videoId);
            return false;
        }
    }
}
