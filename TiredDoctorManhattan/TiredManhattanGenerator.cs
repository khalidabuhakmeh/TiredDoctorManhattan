using System.Text.RegularExpressions;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace TiredDoctorManhattan;

public static class TiredManhattanGenerator
{
    public static async Task<Image> Generate(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));

        var background = await Settings.GetBackground();

        var textOptions = new TextOptions(Settings.Font)
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Origin = Settings.TextBoxOrigin
        };

        var textRectangle = TextMeasurer.Measure(text, textOptions);

        var width = textRectangle.Width + Settings.TextPadding * 2;
        var height = textRectangle.Height + Settings.TextPadding * 2;

        var container = new RectangularPolygon(
            x: Settings.TextBoxOrigin.X - Settings.TextPadding,
            y: Settings.TextBoxOrigin.Y - height / 2,
            width: width,
            height: height
        );
        var blackBorder = new RectangularPolygon(
            x: container.X - Settings.BlackBorderThickness,
            y: container.Y - Settings.BlackBorderThickness,
            width: container.Width + Settings.BlackBorderThickness * 2,
            height: container.Height + Settings.BlackBorderThickness * 2
        );
        var whiteBorder = new RectangularPolygon(
            x: blackBorder.X - Settings.WhiteBorderThickness,
            y: blackBorder.Y - Settings.WhiteBorderThickness,
            width: blackBorder.Width + Settings.WhiteBorderThickness * 2,
            height: blackBorder.Height + Settings.WhiteBorderThickness * 2
        );

        background.Mutate(i =>
        {
            i.Fill(Color.White, whiteBorder);
            i.Fill(Color.Black, blackBorder);
            i.Fill(Settings.ManhattanBlue, container);
            i.DrawText(textOptions, text, Color.Black);
            // resize the image, it's kind of big right now
            i.Resize(background.Width / 2, background.Height / 2);
        });

        return background;
    }

    public static async Task<byte[]> GenerateBytes(string text)
    {
        using var ms = new MemoryStream();
        var image = await Generate(text);

        await image.SaveAsync(ms, PngFormat.Instance);
        ms.Position = 0;
        ms.Seek(0, SeekOrigin.Begin);

        return ms.ToArray();
    }

    public static async Task Save(string text, string outputPath = "./")
    {
        var path = System.IO.Path.HasExtension(outputPath)
            ? outputPath
            : System.IO.Path.Combine(outputPath, $"{text.Slugify()}.png");

        var image = await Generate(text);
        await image.SaveAsPngAsync(path);
    }

    public static string Clean(string? text)
    {
        var content = string.IsNullOrWhiteSpace(text) ? "the emptiness" : text.Trim();

        // remove newlines and tabs
        content = Regex.Replace(content, @"\t|\n|\r", "");

        // exclude long tweets
        content = content.Length > 30 ? "long tweets" : content;

        // No Unicode - damn emojis!
        content = content.ContainsUnicode() ? "Unicode & Emojis" : content;

        // make the line work

        content = content.Equals("beans", StringComparison.OrdinalIgnoreCase)
            ? "JEREMY SINCLAIR LOVES BEANS."
            : $"I AM TIRED OF {content}.".ToUpperInvariant();

        return content;
    }

    public static bool ContainsUnicode(this string value)
    {
        var asciiBytesCount = System.Text.Encoding.ASCII.GetByteCount(value);
        var unicodeBytesCount = System.Text.Encoding.UTF8.GetByteCount(value);
        return asciiBytesCount != unicodeBytesCount;
    }
}