using ProfanityFilter.Interfaces;
using Tweetinvi.Core.Parameters;
using Tweetinvi.Events.V2;
using Tweetinvi.Exceptions;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using Tweetinvi.Parameters.V2;

namespace TiredDoctorManhattan;

public class DrManhattanResponder : BackgroundService
{
    private readonly IProfanityFilter _profanityFilter;
    private readonly UserInfo _user;
    private readonly ILogger<DrManhattanResponder> _logger;
    private readonly TwitterClients _twitterClients;

    public DrManhattanResponder(
        TwitterClients twitterClients,
        IProfanityFilter profanityFilter,
        UserInfo user,
        ILogger<DrManhattanResponder> logger)
    {
        _twitterClients = twitterClients;
        _profanityFilter = profanityFilter;
        _user = user;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var twitterClient = _twitterClients.OAuth2;
            var stream = twitterClient.StreamsV2.CreateFilteredStream();
            
            try
            {
                var rules = await twitterClient.StreamsV2.GetRulesForFilteredStreamV2Async();

                // add a rule to the filtered stream
                if (!rules.Rules.Any()) {
                    await twitterClient.StreamsV2.AddRulesToFilteredStreamAsync(
                        new FilteredStreamRuleConfig($"@{_user.ScreenName}", "mention"));
                }

                stream.TweetReceived += (_, args) => Received(args);
                
                await stream.StartAsync(new StartFilteredStreamV2Parameters {
                    TweetFields = new TweetFields().ALL,
                    UserFields = new UserFields().ALL
                });
            }
            catch (TwitterException e)
            {
                try
                {
                    stream.StopStream();
                    
                }
                catch
                {
                    // ignored
                }

                _logger.LogError(e, "Stream stopped due to exception");

                // too many requests
                if (e.StatusCode == 429 || e.Message.Contains("Rate limit exceeded")) 
                {
                    // wait 15 minutes, cool down
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);                    
                }
                else
                {
                    // just chill for 10 seconds
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
    }
    
    private async void Received(TweetV2EventArgs args)
    {
        if (args.Tweet is null)
        {
            _logger.LogInformation("Not a tweet: {Information}", args.Json);
            return;
        }
        
        _logger.LogInformation("{@Tweet}", args);

        var tweet = args.Tweet;

        // conversation id and tweet id should be the same,
        // if they are, then it's the first tweet to the bot 
        if (!tweet.ConversationId.Equals(tweet.Id))
        {
            _logger.LogInformation("Ignore conversations, as they can get noisy");
            return;
        }
        
        var mentions = args.Includes
            .Users
            .Where(u => !u.Username.Equals(_user.ScreenName))
            .Select(u =>$"@{u.Username}");
        
        var text = tweet.Text.Replace($"@{_user.ScreenName}", "").Trim();
        // I'm not dealing with this s#@$!
        if (_profanityFilter.ContainsProfanity(text))
        {
            _logger.LogInformation("Filtered out {Text} from {@From}", text, mentions);
            return;
        }
        
        try
        {
            var twitterClient = _twitterClients.OAuth1;
            var content = TiredManhattanGenerator.Clean(text);
            
            var image = await TiredManhattanGenerator.GenerateBytes(content);
            var upload = await twitterClient.Upload.UploadTweetImageAsync(image);

            var parameters = new PublishTweetParameters(string.Join(" ", mentions))
            {
                InReplyToTweet = new TweetIdentifier(Convert.ToInt64(tweet.Id)),
                Medias = { upload }
            };
            
            await twitterClient.Tweets.PublishTweetAsync(parameters);
            _logger.LogInformation("Reply sent to {Usernames}", mentions);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to reply to {@Args}", args);
        }
    }
}