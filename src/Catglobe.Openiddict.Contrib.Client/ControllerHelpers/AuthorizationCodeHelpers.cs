#if NET
using OpenIddict.Client.AspNetCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Openiddict.Contrib.Client.ControllerHelpers;

public static class AuthorizationCodeHelpers
{
   /// <summary>
   /// This will initiate the authorization code flow with the specified provider.
   /// <example>For example:
   /// <code>
   /// public class AuthenticationController : Controller
   /// {
   /// [HttpGet("~/login")] public ActionResult LogIn(string returnUrl) => this.InitiateAuthorizationCodeLogin(returnUrl, "my-client");
   /// [HttpGet("~/callback/login"), HttpPost("~/callback/login"), IgnoreAntiforgeryToken] public async Task&lt;ActionResult&gt; LogInCallback() => this.StoreRemoteAuthInSchemeAsync();
   /// }
   /// </code> would redirect the user to the specified provider, get asked for consent and if agreed, sent back to the returnUrl.
   /// </example>
   /// </summary>
   /// <param name="controller">The controller</param>
   /// <param name="returnUrl">Parameter from UI where the user will be redirected after the auth is done</param>
   /// <param name="provider">The client you want to authenticate with</param>
   /// <returns>The result you need to return from the controller</returns>
   public static ChallengeResult InitiateAuthorizationCodeLogin(this ControllerBase controller, string returnUrl, string? provider = null)
   {
      var properties = new AuthenticationProperties {
         // Only allow local return URLs to prevent open redirect attacks.
         RedirectUri = controller.Url.IsLocalUrl(returnUrl) ? returnUrl : "/",
      };
      if (!string.IsNullOrEmpty(provider))
         properties.Items[OpenIddictClientAspNetCoreConstants.Properties.ProviderName] = provider;
      // Ask the OpenIddict client middleware to redirect the user agent to the identity provider.
      return controller.Challenge(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
   }

   /// <summary>
   /// This will store the return authorization in the specified scheme. Ie, if you have specified cookies as the default scheme, this will store the authorization in the cookie.
   /// <example>For example:
   /// <code>
   ///  public class AuthenticationController : Controller
   ///  {
   ///  [HttpGet("~/login")] public ActionResult LogIn(string returnUrl) => this.InitiateAuthorizationCodeLogin(returnUrl, "my-client");
   ///  [HttpGet("~/callback/login"), HttpPost("~/callback/login"), IgnoreAntiforgeryToken] public async Task&lt;ActionResult&gt; LogInCallback() => this.StoreRemoteAuthInSchemeAsync();
   ///  }
   /// </code> would redirect the user to the specified provider, get asked for consent and if agreed, sent back to the returnUrl and a login cookie is issued.
   /// </example>
   /// </summary>
   /// <param name="controller">The controller</param>
   /// <param name="customCheck">If you want to do some additional checks (such as double check scopes granted from user match the ones you require).
   /// <example>For example:
   /// <code>
   ///  resultPrincipal => {
   ///    var requiredRoles = new HashSet&lt;string&gt;
   ///    {
   ///    "Example",
   ///    };
   ///    requiredRoles.ExceptWith(resultPrincipal.GetClaim(OpenIddictConstants.Claims.Role)?.Split(" ") ?? []);
   ///
   ///    if (requiredRoles is not { Count: &gt; 0 } missingPermissions) return null;
   ///    PermissionsMissing = requiredRoles.ToList();
   ///    return Page();
   ///  }
   /// </code></example>
   /// </param>
   /// <param name="storeRemoteInfo">Store additional properties from the remote.
   /// <example>
   /// <code>
   /// (identity, remote) => identity.SetClaim("IsSanta", remote.GetClaim("IsSanta"))
   /// </code>
   /// </example>
   /// </param>
   ///  <returns>The result you need to return from the controller</returns>
   public static async Task<ActionResult> StoreRemoteAuthInSchemeAsync(this ControllerBase controller, Func<ClaimsPrincipal, ActionResult?>? customCheck = null, Action<ClaimsIdentity, ClaimsPrincipal>? storeRemoteInfo = null)
   {
     // Retrieve the authorization data validated by OpenIddict as part of the callback handling.
      var result = await controller.HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

      // Multiple strategies exist to handle OAuth 2.0/OpenID Connect callbacks, each with their pros and cons:
      //
      //   * Directly using the tokens to perform the necessary action(s) on behalf of the user, which is suitable
      //     for applications that don't need a long-term access to the user's resources or don't want to store
      //     access/refresh tokens in a database or in an authentication cookie (which has security implications).
      //     It is also suitable for applications that don't need to authenticate users but only need to perform
      //     action(s) on their behalf by making API calls using the access token returned by the remote server.
      //
      //   * Storing the external claims/tokens in a database (and optionally keeping the essential claims in an
      //     authentication cookie so that cookie size limits are not hit). For the applications that use ASP.NET
      //     Core Identity, the UserManager.SetAuthenticationTokenAsync() API can be used to store external tokens.
      //
      //     Note: in this case, it's recommended to use column encryption to protect the tokens in the database.
      //
      //   * Storing the external claims/tokens in an authentication cookie, which doesn't require having
      //     a user database but may be affected by the cookie size limits enforced by most browser vendors
      //     (e.g Safari for macOS and Safari for iOS/iPadOS enforce a per-domain 4KB limit for all cookies).
      //
      //     Note: this is the approach used here, but the external claims are first filtered to only persist
      //     a few claims like the user identifier. The same approach is used to store the access/refresh tokens.

      // Important: if the remote server doesn't support OpenID Connect and doesn't expose a userinfo endpoint,
      // result.Principal.Identity will represent an unauthenticated identity and won't contain any claim.
      //
      // Such identities cannot be used as-is to build an authentication cookie in ASP.NET Core (as the
      // antiforgery stack requires at least a name claim to bind CSRF cookies to the user's identity) but
      // the access/refresh tokens can be retrieved using result.Properties.GetTokens() to make API calls.
      var resultPrincipal = result.Principal;
      if (resultPrincipal is not ClaimsPrincipal { Identity.IsAuthenticated: true })
      {
         throw new InvalidOperationException("The external authorization data cannot be used for authentication.");
      }

      //invoke the custom check
      if (customCheck?.Invoke(resultPrincipal) is {} customCheckFailed)
         return customCheckFailed;

      // Build an identity based on the external claims and that will be used to create the authentication cookie.
      var identity = new ClaimsIdentity(authenticationType: "ExternalLogin");

      // By default, OpenIddict will automatically try to map the email/name and name identifier claims from
      // their standard OpenID Connect or provider-specific equivalent, if available. If needed, additional
      // claims can be resolved from the external identity and copied to the final authentication cookie.
      identity.SetClaim(ClaimTypes.Email, resultPrincipal.GetClaim(ClaimTypes.Email))
         .SetClaim(ClaimTypes.Name, resultPrincipal.GetClaim(ClaimTypes.Name))
         .SetClaim(ClaimTypes.NameIdentifier, resultPrincipal.GetClaim(ClaimTypes.NameIdentifier));

      storeRemoteInfo?.Invoke(identity, resultPrincipal);

      // Preserve the registration identifier to be able to resolve it later.
      identity.SetClaim(Claims.Private.RegistrationId, resultPrincipal.GetClaim(Claims.Private.RegistrationId));

      // Build the authentication properties based on the properties that were added when the challenge was triggered.
      var resultProperties = result.Properties;
      var properties = new AuthenticationProperties(resultProperties!.Items)
      {
         RedirectUri = resultProperties.RedirectUri ?? "/"
      };

      // If needed, the tokens returned by the authorization server can be stored in the authentication cookie.
      //
      // To make cookies less heavy, tokens that are not used are filtered out before creating the cookie.
      properties.StoreTokens(resultProperties.GetTokens().Where(token => token switch
      {
         // Preserve the access, identity and refresh tokens returned in the token response, if available.
         {
            Name: OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken or
            OpenIddictClientAspNetCoreConstants.Tokens.BackchannelIdentityToken or
            OpenIddictClientAspNetCoreConstants.Tokens.RefreshToken
         } => true,

         // Ignore the other tokens.
         _ => false
      }));

      // Ask the default sign-in handler to return a new cookie and redirect the
      // user agent to the return URL stored in the authentication properties.
      //
      // For scenarios where the default sign-in handler configured in the ASP.NET Core
      // authentication options shouldn't be used, a specific scheme can be specified here.
      return controller.SignIn(new(identity), properties);
   }

   /// <summary>
   /// This will log out locally and then initiate the remote logout process with provider used for login.
   /// <example>For example:
   /// <code>
   /// public class AuthenticationController : Controller
   /// {
   /// [HttpGet("~/logout")] public ActionResult LogOut(string returnUrl) => View();
   /// [HttpPost("~/logout"),ValidateAntiForgeryToken] public ActionResult LogOut(string returnUrl) => this.LocalAndRemoteLogOutAsync(returnUrl);
   /// [HttpGet("~/callback/logout"), HttpPost("~/callback/logout"), IgnoreAntiforgeryToken] public ActionResult LogOutCallback() => this.RemoteLogOutCallbackAsync();
   /// }
   /// </code> would log out locally, and then redirect the user to the specified provider, get asked for consent to also log out at provider and if agreed, sent back to the returnUrl.
   /// </example>
   /// </summary>
   /// <param name="controller">The controller</param>
   /// <param name="returnUrl">Parameter from UI where the user will be redirected after the auth is done</param>
   /// <returns>The result you need to return from the controller</returns>
   public static async Task<ActionResult> LocalAndRemoteLogOutAsync(this ControllerBase controller, string returnUrl)
   {
      // Retrieve the identity stored in the local authentication cookie. If it's not available,
      // this indicate that the user is already logged out locally (or has not logged in yet).
      //
      // For scenarios where the default authentication handler configured in the ASP.NET Core
      // authentication options shouldn't be used, a specific scheme can be specified here.
      var result = await controller.HttpContext.AuthenticateAsync();
      if (result is not { Succeeded: true })
      {
         // Only allow local return URLs to prevent open redirect attacks.
         return controller.Redirect(controller.Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
      }

      // Remove the local authentication cookie before triggering a redirection to the remote server.
      //
      // For scenarios where the default sign-out handler configured in the ASP.NET Core
      // authentication options shouldn't be used, a specific scheme can be specified here.
      await controller.HttpContext.SignOutAsync();

      var properties = new AuthenticationProperties
      {
         // Only allow local return URLs to prevent open redirect attacks.
         RedirectUri = controller.Url.IsLocalUrl(returnUrl) ? returnUrl : "/",
         Items =
         {
            // While not required, the specification encourages sending an id_token_hint
            // parameter containing an identity token returned by the server for this user.
            [OpenIddictClientAspNetCoreConstants.Properties.IdentityTokenHint] = result.Properties.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelIdentityToken)
         }
      };
      if (result.Properties.Items.TryGetValue(OpenIddictClientAspNetCoreConstants.Properties.ProviderName, out var provider))
         properties.Items[OpenIddictClientAspNetCoreConstants.Properties.ProviderName] = provider;

      return controller.SignOut(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
   }

   /// <summary>
   /// This will process the remotes logout callback.
   /// <example>For example:
   /// <code>
   /// public class AuthenticationController : Controller
   /// {
   /// [HttpGet("~/logout")] public ActionResult LogOut(string returnUrl) => View();
   /// [HttpPost("~/logout"),ValidateAntiForgeryToken] public ActionResult LogOut(string returnUrl) => this.LocalAndRemoteLogOutAsync(returnUrl);
   /// [HttpGet("~/callback/logout"), HttpPost("~/callback/logout"), IgnoreAntiforgeryToken] public ActionResult LogOutCallback() => this.RemoteLogOutCallbackAsync();
   /// }
   /// </code> would log out locally, and then redirect the user to the specified provider, get asked for consent to also log out at provider and if agreed, sent back to the returnUrl.
   /// </example>
   /// </summary>
   /// <param name="controller">The controller</param>
   /// <returns>The result you need to return from the controller</returns>
   public static async Task<ActionResult> RemoteLogOutCallbackAsync(this ControllerBase controller)
   {
      // Retrieve the data stored by OpenIddict in the state token created when the logout was triggered.
      var result = await controller.HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

      // In this sample, the local authentication cookie is always removed before the user agent is redirected
      // to the authorization server. Applications that prefer delaying the removal of the local cookie can
      // remove the corresponding code from the logout action and remove the authentication cookie in this action.

      return controller.Redirect(result?.Properties?.RedirectUri ?? "/");
   }

}
#endif
