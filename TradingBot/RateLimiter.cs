namespace BybitPerpetualsTradingBot
{
    internal class RateLimiter
    {
        private readonly SemaphoreSlim _semaphoreSlim;
        private readonly ushort _limit;
        private readonly Timer _timer;

        /// <summary>
        /// Rate limits requests per given period
        /// </summary>
        public RateLimiter(ushort limit, TimeSpan releasePeriod)
        {
            _limit = limit;
            _semaphoreSlim = new SemaphoreSlim(_limit, _limit);
            _timer = new Timer(ReleaseTokens, null, releasePeriod, releasePeriod);
        }

        /// <summary>
        /// Waits asynchronously until a token is available
        /// </summary>
        public async Task WaitAsync() =>
            await _semaphoreSlim.WaitAsync();

        /// <summary>
        /// Releases tokens
        /// </summary>
        private void ReleaseTokens(object? state)
        {
            ushort tokensToRelease = (ushort)(_limit - _semaphoreSlim.CurrentCount);

            if (tokensToRelease > 0)
                _semaphoreSlim.Release(tokensToRelease);
        }
    }
}
