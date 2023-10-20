using OpenIddict.Client;

namespace Openiddict.Contrib.Client.HttpHelpers;

/// <summary>
/// Delegate handler to set the auth header for remote api calls using client authentication for the provider specified.
/// It will use the refresh token to get a new access token if the current one is expired or about to expire (5 min before).
/// <example>
/// <code>
/// internal class MyRefreshTokenClientAuthenticationHandler(OpenIddictClientService openIddict) : RefreshTokenClientAuthenticationHandler(openIddict, "my-client");
/// services.AddSingleton&lt;MyRefreshTokenClientAuthenticationHandler&gt;();
/// services.AddHttpClient&lt;ClientDemoApiService&gt;(client =&gt; client.BaseAddress = new(serverUrl))
/// .SetHandlerLifetime(TimeSpan.FromMinutes(120))
/// .AddPolicyHandler(GetRetryPolicy())
/// .AddHttpMessageHandler&lt;MyRefreshTokenClientAuthenticationHandler&gt;();
/// ...
/// public class MyService(ClientDemoApiService service) {
///  public async MyResult Demo(CancellationToken cancellationToken) => service.Demo(cancellationToken);
/// }
/// </code></example>
/// </summary>
public abstract class RefreshTokenClientAuthenticationHandler(OpenIddictClientService openIddict, string provider) : RefreshTokenAuthenticationDelegateHandlerBase
{
   private (string accessToken, DateTimeOffset expire, string? refreshToken) _curCredentials = ("", DateTimeOffset.MinValue, "");
   private readonly SemaphoreSlim _lock = new(1);

   /// <summary>
   /// Logic to retrieve the current OIDC access token, and refresh if necessary
   /// </summary>
   /// <returns>Valid token or null</returns>
   protected override async ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
   {
      await _lock.WaitAsync(cancellationToken);
      try
      {
         if (DateTimeOffset.UtcNow.AddMinutes(5) > _curCredentials.expire) 
            await RefreshToken(cancellationToken);

         return _curCredentials.accessToken;
      }
      finally
      {
         _lock.Release();
      }
   }

   private async ValueTask RefreshToken(CancellationToken cancellationToken)
   {
      var refreshtoken = _curCredentials.refreshToken;

      if (string.IsNullOrEmpty(refreshtoken))
      {
         await ReAuth(cancellationToken);
         return;
      }

      try
      {
         var result = await openIddict.AuthenticateWithRefreshTokenAsync(new()
         {
            ProviderName = provider,
            CancellationToken = cancellationToken,
            RefreshToken = refreshtoken,
         });
         _curCredentials = (result.AccessToken, result.TokenResponse.ExpiresIn is { } value ? DateTimeOffset.UtcNow.AddSeconds(value) : DateTimeOffset.UtcNow, result.RefreshToken);
      }
      catch
      {
         await ReAuth(cancellationToken);
      }

   }

   private async Task ReAuth(CancellationToken cancellationToken)
   {
      var auth = await openIddict.AuthenticateWithClientCredentialsAsync(new()
      {
         ProviderName = provider,
         CancellationToken = cancellationToken,
      });

      _curCredentials = (auth.AccessToken, auth.TokenResponse.ExpiresIn is { } value ? DateTimeOffset.UtcNow.AddSeconds(value) : DateTimeOffset.UtcNow, auth.RefreshToken!);
   }
}