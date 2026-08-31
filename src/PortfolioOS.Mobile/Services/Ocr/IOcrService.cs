using PortfolioOS.Shared.Scanning;

namespace PortfolioOS.Mobile.Services.Ocr;

/// <summary>
/// Reads text off a photo, on the device. Nothing is uploaded: the image never leaves the
/// phone, which is why this sits behind an interface with a per-platform implementation
/// (ML Kit on Android, the Vision framework on iOS) rather than an API call.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Recognises <paramref name="filePath"/>. Returns <see cref="OcrText.Empty"/> when the
    /// image holds no readable text - that is a normal outcome, not an error.
    /// </summary>
    Task<OcrText> RecognizeAsync(string filePath, CancellationToken ct = default);
}
