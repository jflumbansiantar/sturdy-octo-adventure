using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PortfolioOS.Admin.Services;

/// <summary>
/// Service tujuan membalas dengan status error. Status dan body-nya dibawa apa adanya
/// supaya pesan validasi asli (mis. daftar error password dari ASP.NET Core Identity)
/// sampai ke UI admin tanpa diterjemahkan ulang dan kehilangan detail.
/// </summary>
public sealed class DownstreamException(
    string service,
    HttpStatusCode statusCode,
    string? body,
    string? contentType)
    : Exception($"{service} membalas {(int)statusCode} {statusCode}")
{
    public string Service { get; } = service;
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? Body { get; } = body;
    public string? ContentType { get; } = contentType;
}

internal static class DownstreamHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadAsync<T>(
        this HttpResponseMessage response, string service, CancellationToken ct)
    {
        await response.EnsureAsync(service, ct);

        var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return value ?? throw new DownstreamException(
            service, HttpStatusCode.BadGateway, null, null);
    }

    public static async Task EnsureAsync(
        this HttpResponseMessage response, string service, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new DownstreamException(
            service,
            response.StatusCode,
            string.IsNullOrWhiteSpace(body) ? null : body,
            response.Content.Headers.ContentType?.MediaType);
    }
}
