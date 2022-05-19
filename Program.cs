using ProfanityFilter.Interfaces;
using TiredDoctorManhattan;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

// tired dr. manhattan
var credentials = new TwitterCredentials
{
    ConsumerKey = configuration["API_KEY"],
    ConsumerSecret = configuration["API_KEY_SECRET"],
    AccessToken = configuration["ACCESS_TOKEN"],
    AccessTokenSecret = configuration["ACCESS_TOKEN_SECRET"],
    BearerToken = configuration["BEARER_TOKEN"]
};

var clients = new TwitterClients(
    OAuth1: new TwitterClient(credentials),
    OAuth2: new TwitterClient(new ConsumerOnlyCredentials(credentials))
);

var user = await clients.OAuth1.Users.GetAuthenticatedUserAsync();

builder.Services.AddSingleton(clients);
builder.Services.AddSingleton(new UserInfo(user.Id, user.ScreenName));
builder.Services.AddSingleton<IProfanityFilter>(new global::ProfanityFilter.ProfanityFilter());

builder.Services.AddHostedService<DrManhattanResponder>();

var app = builder.Build();

app.MapGet("/",
    () => Results.Content( /* language=html */
        "<html lang='en'><h1><a href='https://twitter.com/TiredManhattan'>@TiredManhattan</a></h1> <ul><li><a href='/image?text=.NET'>.NET</a></li> <li><a href='/image?text=Programming'>Programming</a></li> <li><a href='/image?text=Vegetables'>Vegetables</a></li></ul></html>",
        "text/html"));

app.MapGet("/image", async (string? text) =>
{
    text = TiredManhattanGenerator.Clean(text);
    var image = await TiredManhattanGenerator.GenerateBytes(text);
    return Results.File(image, "image/png");
});

app.MapPost("/tweet",
    async (string text, IProfanityFilter profanityFilter, TwitterClients twitter, ILogger<Program> logger) =>
    {
        logger.LogInformation("{Text}", text);

        // I'm not dealing with this s#@$!
        if (profanityFilter.IsProfanity(text))
        {
            logger.LogInformation("Filtered out {Text}", text);
            return;
        }

        try
        {
            var content = TiredManhattanGenerator.Clean(text);
            var image = await TiredManhattanGenerator.GenerateBytes(content);
            var upload = await twitter.OAuth1.Upload.UploadTweetImageAsync(image);

            var parameters = new PublishTweetParameters
            {
                Medias = { upload }
            };

            await twitter.OAuth1.Tweets.PublishTweetAsync(parameters);
            logger.LogInformation("Sent to {Text}", text);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unable to reply to {Text}", text);
        }
    });

app.MapGet("/profanity", (string text, IProfanityFilter filter) => new {
    text,
    hasProfanity = filter.DetectAllProfanities(text).Any()
});

await app.RunAsync();

public record UserInfo(long UserId, string ScreenName);
public record TwitterClients(ITwitterClient OAuth1, ITwitterClient OAuth2);