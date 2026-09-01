using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace PortfolioOS.AdminWeb.Services;

/// <summary>
/// Memecah claim bernilai array menjadi satu claim per elemen.
///
/// Pabrik bawaan menyalin tiap properti id_token apa adanya, jadi user dengan lebih dari
/// satu role berakhir sebagai satu claim berisi teks <c>["admin","user"]</c> — yang tidak
/// pernah cocok dengan <c>Roles="admin"</c> dan membuat admin terlihat tidak punya akses.
/// </summary>
public class ArrayClaimsPrincipalFactory(IAccessTokenProviderAccessor accessor)
    : AccountClaimsPrincipalFactory<RemoteUserAccount>(accessor)
{
    public override async ValueTask<ClaimsPrincipal> CreateUserAsync(
        RemoteUserAccount account, RemoteAuthenticationUserOptions options)
    {
        var user = await base.CreateUserAsync(account, options);

        if (user.Identity is not ClaimsIdentity identity || account.AdditionalProperties is null)
            return user;

        foreach (var (key, value) in account.AdditionalProperties)
        {
            if (value is not JsonElement { ValueKind: JsonValueKind.Array } array) continue;

            foreach (var existing in identity.FindAll(key).ToArray())
                identity.RemoveClaim(existing);

            foreach (var item in array.EnumerateArray())
            {
                var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                if (!string.IsNullOrWhiteSpace(text)) identity.AddClaim(new Claim(key, text));
            }
        }

        return user;
    }
}
