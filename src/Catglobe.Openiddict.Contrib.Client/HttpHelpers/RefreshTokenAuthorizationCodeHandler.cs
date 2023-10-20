using System.Security.Authentication;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;

namespace Openiddict.Contrib.Client.HttpHelpers;

/// <summary>
/// Delegate handler to set the auth header for remote api calls using the currently logged-in user's OIDC access token.
/// It will use the refresh token to get a new access token if the current one is expired or about to expire (5 min before).
/// Notice this requires the call be initiated from a web context, and the user must be logged in.
/// <example>
/// <code>
/// services.AddSingleton&lt;RefreshTokenAuthorizationCodeHandler&gt;();
/// services.AddHttpClient&lt;ClientDemoApiService&gt;(client =&gt; client.BaseAddress = new(serverUrl))
/// .AddHttpMessageHandler&lt;RefreshTokenAuthorizationCodeHandler&gt;();
/// ...
/// public class DemoApiController(ClientDemoApiService service) : Controller {
///  [HttpGet("~/api/demo")] public async Task&lt;ActionResult&lt;Dictionary&lt;string, string&gt;&gt;&gt; Demo(CancellationToken cancellationToken) => Json(service.Demo(cancellationToken));
/// }
/// </code></example>
/// </summary>
public class RefreshTokenAuthorizationCodeHandler(IHttpContextAccessor httpContextAccessor, OpenIddictClientService openIddict) : RefreshTokenAuthenticationDelegateHandlerBase
{
   protected override async ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
   {
      var context = httpContextAccessor.HttpContext ?? throw new AuthenticationException("No context");

      var auth = await context.AuthenticateAsync();

      if (auth.Properties is null) return null;

      var expiresAt = auth.Properties.ExpiresUtc;

      if (expiresAt is {} at && at.Subtract(DateTimeOffset.UtcNow).TotalMinutes < 5)
         return await RefreshToken(context, auth.Properties, cancellationToken);

      return auth.Properties.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken);
   }

   private async Task<string?> RefreshToken(HttpContext context, AuthenticationProperties oldProperties, CancellationToken cancellationToken)
   {
      var refreshtoken = await context.GetTokenAsync(OpenIddictClientAspNetCoreConstants.Tokens.RefreshToken);
      if (refreshtoken is null) return null;
         
      var result = await openIddict.AuthenticateWithRefreshTokenAsync(new()
      {
         CancellationToken = cancellationToken,
         RefreshToken = refreshtoken,
         RegistrationId = context.User.FindFirst(OpenIddictConstants.Claims.Private.RegistrationId)?.Value,
      });
      var properties = new AuthenticationProperties(oldProperties.Items)
      {
         RedirectUri = null,
      };
      properties.UpdateTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken, result.AccessToken);
      if (result.TokenResponse.ExpiresIn is { } value)
         properties.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(value);

      if (!string.IsNullOrEmpty(result.RefreshToken))
         properties.UpdateTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.RefreshToken, result.RefreshToken);

      // Issue new login cookie with new tokens
      await context.SignInAsync(context.User, properties);

      return result.AccessToken;
   }
}