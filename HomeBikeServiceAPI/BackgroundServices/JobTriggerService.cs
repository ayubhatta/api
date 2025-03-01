using HomeBikeServiceAPI.BackgroundServices;
using Hangfire;
using System;

namespace HomeBikeServiceAPI.Services
{
    public class JobTriggerService
    {
        private readonly JobService _jobService;

        public JobTriggerService(JobService jobService)
        {
            _jobService = jobService;
        }

        // Trigger a delayed job for In Progress status
        public void TriggerInProgressJob(int bookingId, TimeSpan delay)
        {
            BackgroundJob.Schedule(() => _jobService.InProgressJob(bookingId), delay);
        }

        // Trigger a delayed job for Completed status
        public void TriggerCompletedJob(int bookingId, TimeSpan delay)
        {
            BackgroundJob.Schedule(() => _jobService.CompletedJob(bookingId), delay);
        }
    }
}
