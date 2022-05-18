﻿using ProfanityFilter.Interfaces;
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
};

// get text and measure it for box.
var twitterClient = new TwitterClient(credentials);

await twitterClient.Auth.InitializeClientBearerTokenAsync();
var user = await twitterClient.Users.GetAuthenticatedUserAsync();

builder.Services.AddSingleton(twitterClient);
builder.Services.AddSingleton(new UserInfo(user.Id, user.ScreenName));
builder.Services.AddSingleton<IProfanityFilter>(new global::ProfanityFilter.ProfanityFilter());

//builder.Services.AddHostedService<DrManhattanResponder>();

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
    async (string text, IProfanityFilter profanityFilter, TwitterClient twitter, ILogger<Program> logger) =>
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
            var upload = await twitter.Upload.UploadTweetImageAsync(image);

            var parameters = new PublishTweetParameters
            {
                Medias = { upload }
            };

            await twitter.Tweets.PublishTweetAsync(parameters);
            logger.LogInformation("Sent to {Text}", text);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unable to reply to {Text}", text);
        }
    });

await app.RunAsync();

public record UserInfo(long UserId, string ScreenName);