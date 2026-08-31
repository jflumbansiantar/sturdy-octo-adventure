using Android.Gms.Tasks;
using PortfolioOS.Shared.Scanning;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;
using MlKitText = Xamarin.Google.MLKit.Vision.Text.Text;
// Android.Gms.Tasks defines its own CancellationToken, which collides with the BCL one.
using CancellationToken = System.Threading.CancellationToken;

namespace PortfolioOS.Mobile.Services.Ocr;

/// <summary>
/// Android OCR via ML Kit's on-device Latin text recogniser. The model ships inside the app,
/// so this works with no network and no per-scan cost.
/// </summary>
public class OcrService : IOcrService
{
    public async Task<OcrText> RecognizeAsync(string filePath, CancellationToken ct = default)
    {
        var file = new Java.IO.File(filePath);
        var uri = global::Android.Net.Uri.FromFile(file)
            ?? throw new InvalidOperationException($"Tidak bisa membuka gambar: {filePath}");

        // FromFilePath, not FromBitmap: this overload reads the EXIF orientation and rotates
        // the image itself. A portrait photo straight from the camera is stored sideways, and
        // without the rotation ML Kit returns nothing at all.
        var image = InputImage.FromFilePath(Platform.AppContext, uri);

        using var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
        using var listener = new ResultListener();

        recognizer.Process(image)
            .AddOnSuccessListener(listener)
            .AddOnFailureListener(listener);

        await using var registration = ct.Register(listener.Cancel);
        var result = await listener.Completion;

        return result is MlKitText text ? ToOcrText(text) : OcrText.Empty;
    }

    private static OcrText ToOcrText(MlKitText text)
    {
        var lines = new List<OcrLine>();

        foreach (var block in text.TextBlocks)
        {
            foreach (var line in block.Lines)
            {
                var box = line.BoundingBox;
                if (box is null || string.IsNullOrWhiteSpace(line.Text)) continue;

                lines.Add(new OcrLine(line.Text, box.Left, box.Top, box.Width(), box.Height()));
            }
        }

        // AllText is rebuilt from the lines rather than taken from the recogniser, so the
        // keyword matching and the layout heuristics always see exactly the same content.
        return new OcrText(string.Join("\n", lines.Select(l => l.Text)), lines);
    }

    /// <summary>
    /// Bridges ML Kit's Java listener callbacks onto a C# await. Google Play Services tasks
    /// are not awaitable on their own.
    /// </summary>
    private sealed class ResultListener : Java.Lang.Object, IOnSuccessListener, IOnFailureListener
    {
        private readonly TaskCompletionSource<Java.Lang.Object?> _completion = new();

        public Task<Java.Lang.Object?> Completion => _completion.Task;

        public void OnSuccess(Java.Lang.Object? result) => _completion.TrySetResult(result);

        public void OnFailure(Java.Lang.Exception e) =>
            _completion.TrySetException(new InvalidOperationException(e.Message ?? "OCR gagal."));

        public void Cancel() => _completion.TrySetCanceled();
    }
}
