using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Slugify;

namespace TiredDoctorManhattan;

public static class Settings
{
    static Settings()
    {
        var config = new SlugHelperConfiguration();
        config.StringReplacements.Add(".", "");
        config.TrimWhitespace = true;
        config.ForceLowerCase = true;
        SlugHelper = new SlugHelper(config);
    }

    private static readonly SlugHelper SlugHelper;
    private static int FontSize => 26; //px

    private static readonly Lazy<Font> FontValue =
        new(() =>
        {
            FontCollection collection = new();
            var family = collection.Add("./assets/KMKDSPK_.ttf");
            return family.CreateFont(FontSize, FontStyle.Bold);
        });

    public static Font Font => FontValue.Value;

    // return a new instance every time
    public static async Task<Image> GetBackground()
        => await Image.LoadAsync("./assets/background.png") ;

    public static PointF TextBoxOrigin { get; } = new(x: 824, y: 165); // px
    public static float TextPadding => 15; //px
    public static float BlackBorderThickness => 3;
    public static float WhiteBorderThickness => 5;
    public static Color ManhattanBlue { get; } = new(new Rgba32(1, 215, 253));

    public static string Slugify(this string value)
    {
        return SlugHelper.GenerateSlug(value);
    }
}