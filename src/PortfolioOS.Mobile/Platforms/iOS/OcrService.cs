using Foundation;
using PortfolioOS.Shared.Scanning;
using UIKit;
using Vision;

namespace PortfolioOS.Mobile.Services.Ocr;

/// <summary>
/// iOS OCR via the built-in Vision framework - no package needed, and like the Android side
/// it runs entirely on the device.
/// </summary>
public class OcrService : IOcrService
{
    public Task<OcrText> RecognizeAsync(string filePath, CancellationToken ct = default)
    {
        using var image = UIImage.FromFile(filePath);
        if (image?.CGImage is null) return Task.FromResult(OcrText.Empty);

        double width = image.CGImage.Width;
        double height = image.CGImage.Height;
        var lines = new List<OcrLine>();

        using var request = new VNRecognizeTextRequest((req, error) =>
        {
            if (error is not null || req.GetResults<VNRecognizedTextObservation>() is not { } results) return;

            foreach (var observation in results)
            {
                var candidate = observation.TopCandidates(1).FirstOrDefault();
                if (candidate?.String is not { Length: > 0 } value) continue;

                var box = observation.BoundingBox;

                // Vision reports normalised coordinates with the origin at the BOTTOM-left,
                // while OcrLine - and ML Kit - use top-left pixels. Skipping this conversion
                // silently inverts the page, and every same-row lookup picks the wrong value
                // on iOS only.
                lines.Add(new OcrLine(
                    value,
                    box.X * width,
                    (1 - box.Y - box.Height) * height,
                    box.Width * width,
                    box.Height * height));
            }
        })
        {
            RecognitionLevel = VNRequestTextRecognitionLevel.Accurate,
            UsesLanguageCorrection = true
        };

        using var handler = new VNImageRequestHandler(image.CGImage, new NSDictionary());
        handler.Perform([request], out var performError);

        if (performError is not null)
            throw new InvalidOperationException($"OCR gagal: {performError.LocalizedDescription}");

        return Task.FromResult(new OcrText(string.Join("\n", lines.Select(l => l.Text)), lines));
    }
}
