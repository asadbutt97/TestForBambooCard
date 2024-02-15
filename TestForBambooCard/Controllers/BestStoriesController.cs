using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace TestForBambooCard.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BestStoriesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        public BestStoriesController(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        [HttpGet("{n}")]
        public async Task<ActionResult<IEnumerable<Story>>> GetBestStories(int n)
        {
            var cacheKey = $"BestStories_{n}";

            if (_cache.TryGetValue(cacheKey, out List<Story> cachedStories))
            {
                return Ok(cachedStories);
            }

            List<int> bestStoryIds = await GetBestStoryIds(3);
            List<Story> bestStories = new List<Story>();
           
            using (var client = _httpClientFactory.CreateClient())
            {
                foreach (int storyId in bestStoryIds.GetRange(0, n))
                {
                    var response = await client.GetAsync($"https://hacker-news.firebaseio.com/v0/item/{storyId}.json");
                    if (response.IsSuccessStatusCode)
                    {
                        var storyJson = await response.Content.ReadAsStringAsync();
                        var story = JsonConvert.DeserializeObject<Story>(storyJson);
                        bestStories.Add(story);
                    }
                }
            }
            bestStories = bestStories.OrderByDescending(s => s.Score).ToList();

            _cache.Set(cacheKey, bestStories, TimeSpan.FromMinutes(10)); 

            return Ok(bestStories);
        }

        private async Task<List<int>> GetBestStoryIds(int mmaxAttempts)
        {
            List<int> bestStoryIds = new List<int>();
            int currentRetry = 0;

            while (currentRetry < mmaxAttempts)
            {
                try
                {
                    using (var client = _httpClientFactory.CreateClient())
                    {
                        var response = await client.GetAsync("https://hacker-news.firebaseio.com/v0/beststories.json");

                        if (response.IsSuccessStatusCode)
                        {
                            var bestStoryIdsJson = await response.Content.ReadAsStringAsync();
                            bestStoryIds = JsonConvert.DeserializeObject<List<int>>(bestStoryIdsJson);
                            return bestStoryIds;
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    //error logging
                }


                int delay = (int)Math.Pow(2, currentRetry) * 1000;
                await Task.Delay(delay);

                currentRetry++;
            }


            return bestStoryIds;
        }

    }

    public class Story
    {
        public string By { get; set; }
        public int Id { get; set; }
        public int Score { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
    }
}

