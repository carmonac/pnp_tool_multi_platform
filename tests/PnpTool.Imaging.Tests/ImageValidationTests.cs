using PnpTool.Core;

namespace PnpTool.Imaging.Tests;

[Collection(GlobalState.Name)]
public sealed class ImageValidationTests : IDisposable
{
    private readonly Scratch _scratch = new();

    public void Dispose() => _scratch.Dispose();

    [Fact]
    public void Good_images_are_kept_broken_ones_dropped_and_odd_resolutions_restated()
    {
        string good = _scratch.Image("good.png", 50, 70, dpi: 300);
        string coarse = _scratch.Image("coarse.png", 50, 70, dpi: 72);
        string broken = Path.Combine(Path.GetDirectoryName(good)!, "broken.png");
        File.WriteAllText(broken, "not an image");
        var shares = new List<int>();

        string[] usable = ImageValidation.Usable([good, broken, coarse], shares.Add);

        Assert.Equal(2, usable.Length);
        Assert.Equal(good, usable[0]);
        Assert.NotEqual(coarse, usable[1]);                       // a copy, in the temporary folder
        Assert.True(File.Exists(usable[1]));
        Assert.Equal(300, ImageDensity.Of(usable[1]).X, 0);
        Assert.Equal(72, ImageDensity.Of(coarse).X, 0);           // the image itself is left alone
        Assert.False(ImageDensity.Validate(usable[1]).HasError);
        Assert.Equal(0, shares[0]);
        Assert.Equal(shares.Order(), shares);
    }

    [Fact]
    public void Nothing_gives_nothing_and_a_cancelled_check_stops()
    {
        Assert.Empty(ImageValidation.Usable([]));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        string image = _scratch.Image("card.png", 50, 70, dpi: 300);
        Assert.Throws<OperationCanceledException>(() => ImageValidation.Usable([image], null, cancellation.Token));
    }
}
