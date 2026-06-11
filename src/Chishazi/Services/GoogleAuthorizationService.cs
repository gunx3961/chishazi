using Chishazi.Models;
using Microsoft.JSInterop;

namespace Chishazi.Services;

public sealed class GoogleAuthorizationService(IJSRuntime jsRuntime)
{
    public async Task<GoogleAccessToken> RequestAccessTokenAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var token = await jsRuntime.InvokeAsync<GoogleAccessToken>(
            "chishaziAuth.requestAccessToken",
            cancellationToken,
            clientId);

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new JSException("Google authorization returned no access token.");
        }

        return token;
    }
}
