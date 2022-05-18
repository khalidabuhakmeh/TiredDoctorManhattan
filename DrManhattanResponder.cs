using ProfanityFilter.Interfaces;
using Tweetinvi;
using Tweetinvi.Core.Models;
using Tweetinvi.Models;
using Tweetinvi.Parameters;

namespace TiredDoctorManhattan;

public class DrManhattanResponder : BackgroundService
{
    private readonly TwitterClient _twitterClient;
    private readonly IProfanityFilter _profanityFilter;
    private readonly UserInfo _user;
    private readonly ILogger<DrManhattanResponder> _logger;

    public DrManhattanResponder(
        TwitterClient twitterClient,
        IProfanityFilter profanityFilter,
        UserInfo user,
        ILogger<DrManhattanResponder> logger)
    {
        _twitterClient = twitterClient;
        _profanityFilter = profanityFilter;
        _user = user;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var stream = _twitterClient.Streams.CreateFilteredStream();

            stream.AddLanguageFilter(LanguageFilter.English);
            stream.AddTrack($"@{_user.ScreenName}", Received);
            
            stream.StreamStarted += (_, _) => _logger.LogInformation("Starting Filtered Streaming for @{ScreenName} ({UserId})...", _user.ScreenName, _user.UserId);
            stream.StreamStopped += (_, _) => _logger.LogInformation("Stream Stopped");
            
            try
            {
                await stream.StartMatchingAnyConditionAsync().WaitAsync(stoppingToken);
            }
            catch (Exception e)
            {
                try
                {
                    stream.Stop();
                }
                catch
                {
                    // ignored
                }

                _logger.LogError(e, "Stream stopped due to exception");
            }
        }
    }
    
    private async void Received(ITweet tweet)
    {
        _logger.LogInformation("{tweet}", tweet);
        
        // I'm not dealing with this s#@$!
        if (_profanityFilter.IsProfanity(tweet.Text))
        {
            _logger.LogInformation("Filtered out {Text} from {From}", tweet.Text, tweet.CreatedBy);
            return;
        }

        if (!tweet.Text.StartsWith($"@{_user.ScreenName}"))
        {
            _logger.LogInformation("Ignore mentions of the bot");
            return;
        }
        
        try
        {
            var client = tweet.Client;

            var text = tweet.Text.Replace($"@{_user.ScreenName}", "").Trim();
            var content = TiredManhattanGenerator.Clean(text);
            
            var image = await TiredManhattanGenerator.GenerateBytes(content);
            var upload = await _twitterClient.Upload.UploadTweetImageAsync(image);
            
            var parameters = new PublishTweetParameters($"@{tweet.CreatedBy}")
            {
                InReplyToTweet = tweet,
                Medias = { upload }
            };
            
            await client.Tweets.PublishTweetAsync(parameters);
            _logger.LogInformation("Reply sent to {username}", tweet.CreatedBy);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to reply to {tweet}", tweet);
        }
    }
}