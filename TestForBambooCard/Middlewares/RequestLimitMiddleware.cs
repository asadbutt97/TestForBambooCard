using Newtonsoft.Json;

namespace TestForBambooCard.Middlewares
{
    public class RequestLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxRequests;
        private readonly TimeSpan _interval;
        private readonly Queue<DateTime> _requestTimes;

        public RequestLimitMiddleware(RequestDelegate next, int maxRequests, TimeSpan interval)
        {
            _next = next;
            _maxRequests = maxRequests;
            _interval = interval;
            _semaphore = new SemaphoreSlim(maxRequests);
            _requestTimes = new Queue<DateTime>();
        }

        public async Task Invoke(HttpContext context)
        {
            await _semaphore.WaitAsync();

            try
            {
                EnforceRateLimit();

                await _next(context);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void EnforceRateLimit()
        {
            var now = DateTime.Now;

            // Remove expired request times
            while (_requestTimes.Count > 0 && now - _requestTimes.Peek() > _interval)
            {
                _requestTimes.Dequeue();
            }

            // Add current request time
            _requestTimes.Enqueue(now);

            // Check if the number of requests exceeds the limit
            if (_requestTimes.Count > _maxRequests)
            {
                throw new RequestLimitExceededException("Rate limit exceeded. Please try again later.");
            }
        }

        public class RequestLimitExceededException : Exception
        {
            public RequestLimitExceededException(string? message) : base(message)
            {
            }
        }
    }
}
