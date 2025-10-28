using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DDClipBot.Host.Models;

namespace DDClipBot.Host.Services;

public interface IJobStore
{
    void AddJob(VideoUploadJob job);
    VideoUploadJob? GetJob(string jobId);
    IEnumerable<VideoUploadJob> GetPendingJobs();
    void UpdateJob(VideoUploadJob job);
}

public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<string, VideoUploadJob> _jobs = new();

    public void AddJob(VideoUploadJob job)
    {
        _jobs[job.JobId] = job;
    }

    public VideoUploadJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public IEnumerable<VideoUploadJob> GetPendingJobs()
    {
        return _jobs.Values.Where(j => 
            j.Status == VideoUploadStatus.Queued || 
            j.Status == VideoUploadStatus.Uploading ||
            j.Status == VideoUploadStatus.Processing);
    }

    public void UpdateJob(VideoUploadJob job)
    {
        _jobs[job.JobId] = job;
    }
}
