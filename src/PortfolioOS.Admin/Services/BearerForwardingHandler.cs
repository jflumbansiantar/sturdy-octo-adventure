namespace PortfolioOS.Admin.Services;

/// <summary>
/// Meneruskan header <c>Authorization</c> milik pemanggil ke service tujuan.
///
/// Admin service sengaja tidak punya kredensial machine-to-machine sendiri: token yang
/// dipakai ke Identity dan API adalah token admin yang sedang login, jadi tiap service
/// tetap memutuskan otorisasinya sendiri dan tidak ada jalur "super user" yang bisa
/// bocor lewat service ini.
/// </summary>
public class BearerForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var header = accessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (!string.IsNullOrWhiteSpace(header))
            request.Headers.TryAddWithoutValidation("Authorization", header);

        return base.SendAsync(request, cancellationToken);
    }
}
