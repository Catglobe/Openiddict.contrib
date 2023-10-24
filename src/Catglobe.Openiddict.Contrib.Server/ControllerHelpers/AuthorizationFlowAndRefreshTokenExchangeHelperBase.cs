#if NET
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server.AspNetCore;

namespace Openiddict.Contrib.Server.ControllerHelpers;

/// <summary>
/// Base class for granting the actual access token to Authentication Flow clients or Refresh token.
/// </summary>
public abstract class AuthorizationFlowAndRefreshTokenExchangeHelperBase(IDestinationManager destination)
{
   public async Task<IActionResult?> Process(ControllerBase controller, OpenIddictRequest request)
   {
      if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType()) return null;

      var result = await controller.HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

      // Retrieve the user profile corresponding to the authorization code/refresh token.
      if (!result.Succeeded) return controller.ForbidOpenIddict(Errors.InvalidGrant, "The token is no longer valid.");

      var err = await ValidateUserAccount(result.Principal!.GetClaim(Claims.Subject)!);
      if (!string.IsNullOrEmpty(err)) return controller.ForbidOpenIddict(Errors.InvalidGrant, err);

      var existingClaims = result.Principal.Claims.ToList();
      var identity = new ClaimsIdentity(existingClaims, authenticationType: TokenValidationParameters.DefaultAuthenticationType, nameType: Claims.Name, roleType: Claims.Role);

      identity.SetDestinations(destination.GetDestinations);

      // Returning a SignInResult will ask OpenIddict to issue the appropriate access/identity tokens.
      return controller.SignIn(new(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
   }

   /// <summary>
   /// Validate the account is still valid.
   /// This is needed, because the authentication token from OpenIddict is still valid, but the account might have been deleted or disabled.
   /// </summary>
   /// <param name="userId">The authenticated user's id.</param>
   /// <returns>Error or null if no error.</returns>
   protected abstract Task<string?> ValidateUserAccount(string userId);

}
#endif