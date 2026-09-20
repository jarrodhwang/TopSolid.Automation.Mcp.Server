using System;
using System.Diagnostics;
using System.Threading;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    // Native CAM animation uses a TopSolid UI timer. A mutation must not commit
    // or roll back while that timer is still updating the document.
    internal static class CamAnimationLifecycle
    {
        internal static readonly TimeSpan DefaultCompletionTimeout = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan CompletionSettleInterval = TimeSpan.FromMilliseconds(200);

        internal static TimeSpan WaitForCompletion(Func<bool> isComplete, TimeSpan? timeout = null)
        {
            if (isComplete == null) throw new ArgumentNullException(nameof(isComplete));

            var limit = timeout ?? DefaultCompletionTimeout;
            if (limit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), "The animation timeout must be positive.");

            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                if (isComplete())
                {
                    // IsSimulationComplete can become true just before the last
                    // native timer callback. Require a stable completion sample
                    // before the surrounding undo sequence is allowed to close.
                    Thread.Sleep(CompletionSettleInterval);
                    if (isComplete()) return stopwatch.Elapsed;
                }

                if (stopwatch.Elapsed >= limit)
                    throw new TimeoutException("TopSolid CAM animation did not complete within " + limit.TotalMinutes.ToString("0.##") + " minutes.");

                Thread.Sleep(PollInterval);
            }
        }
    }
}
