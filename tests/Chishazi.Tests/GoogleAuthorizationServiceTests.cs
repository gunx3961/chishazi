using System.Net;
using Chishazi.Models;
using Chishazi.Services;
using Microsoft.JSInterop;

namespace Chishazi.Tests;

public sealed class GoogleAuthorizationServiceTests
{
    [Fact]
    public async Task Requests_ReuseTheSameFullScopeToken()
    {
        var jsRuntime = new AuthorizationJsRuntime();
        var service = new GoogleAuthorizationService(jsRuntime);

        var first = await service.RequestAccessTokenAsync("client-id");
        var second = await service.RequestAccessTokenAsync("client-id");

        Assert.Equal(first.AccessToken, second.AccessToken);
        Assert.Equal(1, jsRuntime.RequestCount);
        Assert.All(jsRuntime.Arguments, args => Assert.Single(args));
    }

    [Fact]
    public async Task ExpiredToken_IsRequestedAgain()
    {
        var jsRuntime = new AuthorizationJsRuntime(expiresInSeconds: 0);
        var service = new GoogleAuthorizationService(jsRuntime);

        var first = await service.RequestAccessTokenAsync("client-id");
        var second = await service.RequestAccessTokenAsync("client-id");

        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(2, jsRuntime.RequestCount);
    }

    [Fact]
    public async Task UnauthorizedResponse_InvalidatesTokenAndRetriesOnce()
    {
        var jsRuntime = new AuthorizationJsRuntime();
        var service = new GoogleAuthorizationService(jsRuntime);
        var operationCount = 0;

        var result = await service.ExecuteWithAccessTokenAsync(
            "client-id",
            accessToken =>
            {
                operationCount++;
                if (operationCount == 1)
                {
                    throw new GoogleSheetsException(
                        HttpStatusCode.Unauthorized,
                        "Expired.");
                }

                return Task.FromResult(accessToken);
            });

        Assert.Equal("token-2", result);
        Assert.Equal(2, operationCount);
        Assert.Equal(2, jsRuntime.RequestCount);
        Assert.Equal(1, jsRuntime.InvalidationCount);
        Assert.Equal("token-1", jsRuntime.InvalidatedAccessToken);
    }

    private sealed class AuthorizationJsRuntime(int expiresInSeconds = 3600)
        : IJSRuntime
    {
        public int RequestCount { get; private set; }
        public int InvalidationCount { get; private set; }
        public string? InvalidatedAccessToken { get; private set; }
        public List<object?[]> Arguments { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier == "chishaziAuth.invalidateAccessToken")
            {
                InvalidationCount++;
                InvalidatedAccessToken = (string?)args?[1];
                return ValueTask.FromResult(default(TValue)!);
            }

            Assert.Equal("chishaziAuth.requestAccessToken", identifier);
            RequestCount++;
            Arguments.Add(args ?? []);
            var token = new GoogleAccessToken
            {
                AccessToken = $"token-{RequestCount}",
                ExpiresInSeconds = expiresInSeconds
            };

            return ValueTask.FromResult((TValue)(object)token);
        }
    }
}
