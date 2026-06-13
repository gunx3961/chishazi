using System.Net;
using Chishazi.Models;
using Chishazi.Localization;
using Microsoft.JSInterop;

namespace Chishazi.Services;

public sealed class GoogleAuthorizationService(IJSRuntime jsRuntime)
{
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private CachedToken? _token;

    public async Task<GoogleAccessToken> RequestAccessTokenAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        if (IsUsable(_token))
        {
            return _token!.Token;
        }

        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            if (IsUsable(_token))
            {
                return _token!.Token;
            }

            var token = await jsRuntime.InvokeAsync<GoogleAccessToken>(
                "chishaziAuth.requestAccessToken",
                cancellationToken,
                clientId);

            if (string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new JSException(UiText.Get("GoogleAuthorizationNoToken"));
            }

            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(
                Math.Max(0, token.ExpiresInSeconds));
            _token = new CachedToken(token, expiresAt);

            return token;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public async Task<T> ExecuteWithAccessTokenAsync<T>(
        string clientId,
        Func<string, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var token = await RequestAccessTokenAsync(clientId, cancellationToken);

        try
        {
            return await operation(token.AccessToken);
        }
        catch (GoogleSheetsException exception)
            when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            await InvalidateAsync(
                clientId,
                token.AccessToken,
                cancellationToken);
            token = await RequestAccessTokenAsync(clientId, cancellationToken);
            return await operation(token.AccessToken);
        }
    }

    public async Task ExecuteWithAccessTokenAsync(
        string clientId,
        Func<string, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithAccessTokenAsync(
            clientId,
            async accessToken =>
            {
                await operation(accessToken);
                return true;
            },
            cancellationToken);
    }

    private async Task InvalidateAsync(
        string clientId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (_token?.Token.AccessToken == accessToken)
        {
            _token = null;
        }

        await jsRuntime.InvokeVoidAsync(
            "chishaziAuth.invalidateAccessToken",
            cancellationToken,
            clientId,
            accessToken);
    }

    private static bool IsUsable(CachedToken? cachedToken) =>
        cachedToken is not null &&
        cachedToken.ExpiresAtUtc > DateTimeOffset.UtcNow;

    private sealed record CachedToken(
        GoogleAccessToken Token,
        DateTimeOffset ExpiresAtUtc);
}
