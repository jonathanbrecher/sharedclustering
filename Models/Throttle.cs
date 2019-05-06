using System;
using System.Threading;
using System.Threading.Tasks;

namespace AncestryDnaClustering.Models
{
    public class Throttle
    {
        private readonly SemaphoreSlim _semaphoreInternal = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _semaphore;
        private readonly TimeSpan _timeUnit = TimeSpan.FromMilliseconds(150);
        private readonly int _maxCountPerTimeUnit = 1;
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
