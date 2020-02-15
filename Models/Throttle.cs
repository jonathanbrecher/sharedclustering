using System;
using System.Threading;
using System.Threading.Tasks;

namespace AncestryDnaClustering.Models
{
    public class Throttle
    {
        private readonly SemaphoreSlim _semaphoreInternal = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _semaphore;

        // Ancestry enforces a limit that appears to be tied to the number of requests made per minute,
        // for example a maximum of 500 requests per minute. The time unit specified here is designed
        // to level the number of requests over time by making a maximum of for example 2 requests per 2/500 minutes.
        // An additional fudge factor of 1.1 is included to back off even a little bit further for safety.
        private readonly TimeSpan _timeUnit = TimeSpan.FromMinutes(2.0 / 500 * 1.1);
        private readonly int _maxCountPerTimeUnit = 2;

        private DateTimeOffset _timeUnitStart;
        private DateTimeOffset _timeUnitEnd;
        private int _countThisTimeUnit;

        public Throttle(int initialCount)
        {
            _semaphore = new SemaphoreSlim(initialCount);
            ResetTimeUnit();
        }

        private void ResetTimeUnit()
        {
            _timeUnitStart = DateTimeOffset.Now;
            _timeUnitEnd = _timeUnitStart + _timeUnit;
            _countThisTimeUnit = 0;
        }

        public async Task WaitAsync()
        {
            await _semaphore.WaitAsync();

            await _semaphoreInternal.WaitAsync();
            try
            {
                if (++_countThisTimeUnit > _maxCountPerTimeUnit)
                {
                    var delay = _timeUnitEnd - DateTimeOffset.Now;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay);
                    }
                    ResetTimeUnit();
                    _countThisTimeUnit = 1;
                }
            }
            finally
            {
                _semaphoreInternal.Release();
            }
        }

        public void Release() => _semaphore.Release();
    }

}
