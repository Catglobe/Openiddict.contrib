#if NET
using OpenIddict.Server.AspNetCore;

namespace Openiddict.Contrib.Server.MinimalApiHelpers;

public static class AuthorizationCodeHelpers
{
   /// <summary>
   /// Helper to return errors to the client.
   /// </summary>
   /// <param name="errorType">The error type, usually a value from <see cref="Errors"/>.</param>
   /// <param name="errorText">The custom text to return.</param>
   /// <returns>Access denied.</returns>
   public static IResult ForbidOpenIddict(string errorType, string errorText) =>
      Results.Forbid(authenticationSchemes: new List<string> {OpenIddictServerAspNetCoreDefaults.AuthenticationScheme},
         properties: new(new Dictionary<string, string?> {
            [OpenIddictServerAspNetCoreConstants.Properties.Error]            = errorType,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = errorText
         }));
}
#endif
