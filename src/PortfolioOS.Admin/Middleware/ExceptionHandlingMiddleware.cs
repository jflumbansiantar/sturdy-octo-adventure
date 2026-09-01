using System.Text.Json;
using PortfolioOS.Admin.Services;

namespace PortfolioOS.Admin.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DownstreamException ex)
        {
            logger.LogWarning("{Service} membalas {Status} untuk {Path}",
                ex.Service, (int)ex.StatusCode, context.Request.Path);

            context.Response.StatusCode = (int)ex.StatusCode;

            // Body error asli diteruskan apa adanya kalau memang JSON — pesan validasi
            // Identity lebih berguna bagi admin daripada pesan generik dari sini.
            if (ex.Body is not null && ex.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(ex.Body);
                return;
            }

            await WriteJsonAsync(context, new { error = $"{ex.Service} menolak permintaan ini." });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       && !context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(ex, "Service tujuan tidak dapat dihubungi untuk {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await WriteJsonAsync(context, new
            {
                error = "Service tujuan tidak dapat dihubungi. Pastikan PortfolioOS.Identity dan PortfolioOS.API berjalan.",
            });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Klien menutup koneksi lebih dulu — bukan error yang perlu dilaporkan.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await WriteJsonAsync(context, new { error = "Terjadi kesalahan tak terduga." });
        }
    }

    private static Task WriteJsonAsync(HttpContext context, object payload)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
