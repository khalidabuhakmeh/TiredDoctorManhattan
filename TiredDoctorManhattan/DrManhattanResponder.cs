using ProfanityFilter.Interfaces;
using Tweetinvi.Core.Parameters;
using Tweetinvi.Events.V2;
using Tweetinvi.Exceptions;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using Tweetinvi.Parameters.V2;
using Tweetinvi.Streaming.V2;

namespace TiredDoctorManhattan;

public class DrManhattanResponder : BackgroundService
{
    private readonly IProfanityFilter _profanityFilter;
    private readonly UserInfo _user;
    private readonly ILogger<DrManhattanResponder> _logger;
    private readonly TwitterClients _twitterClients;
    IFilteredStreamV2? _stream;

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

    public override void Dispose()
    {
        _stream?.StopStream();
        ;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // wait for other instances to wind down
        // because Twitter has a connection limit
        var coolDownMinutes = 1;

        while (!stoppingToken.IsCancellationRequested)
        {
            var twitterClient = _twitterClients.OAuth2;
            _stream = twitterClient.StreamsV2.CreateFilteredStream();

            try
            {
                var rules = await twitterClient.StreamsV2.GetRulesForFilteredStreamV2Async();

                // add a rule to the filtered stream
                if (!rules.Rules.Any())
                {
                    await twitterClient.StreamsV2.AddRulesToFilteredStreamAsync(
                        new FilteredStreamRuleConfig($"@{_user.ScreenName}", "mention"));
                }

                _stream.TweetReceived += (_, args) => Received(args);

                await _stream.StartAsync(new StartFilteredStreamV2Parameters
                {
                    TweetFields = new TweetFields().ALL,
                    UserFields = new UserFields().ALL,
                    Expansions = TweetResponseFields.Expansions.ALL
                });

                // know if the stream has started ever
                // if it has we can change
                coolDownMinutes = 1;
            }
            catch (TwitterException e)
            {
                try
                {
                    _stream.StopStream();
                }
                catch
                {
                    // ignored
                }

                _logger.LogError(e, "Stream stopped due to exception");

                // too many requests
                if (e.Message.Contains("Rate limit exceeded"))
                {
                    // if the stream has started we probably hit some rate limit
                    // let's wait, otherwise we probably have too many connections
                    coolDownMinutes = Math.Min(30, coolDownMinutes * 2);
                    await Task.Delay(TimeSpan.FromMinutes(coolDownMinutes), stoppingToken);
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

        // 1. conversation id and tweet id should be the same,
        // if they are, then it's the first tweet to the bot 
        //
        // 2. If the tweet has media, it might be a previous tweet
        //    or something weird is happening. Ignoring it.
        // 3. If the tweet is referencing other tweets, ignore it.
        if (
            !tweet.ConversationId.Equals(tweet.Id) ||
            tweet.Attachments?.MediaKeys?.Any() == true ||
            tweet.ReferencedTweets?.Any() == true
        )
        {
            _logger.LogInformation("Ignore conversations, as they can get noisy");
            return;
        }

        var mentions = args.Includes
            .Users
            .Where(u => !u.Username.Equals(_user.ScreenName))
            .Select(u => $"@{u.Username}");

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