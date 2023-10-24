#if NET
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Primitives;
using OpenIddict.Server.AspNetCore;

namespace Openiddict.Contrib.Server.RazorPageHelpers;

public static class AuthorizationCodeHelpers
{
   /// <summary>
   /// A client can use the prompt parameter to force the user to login, or the MaxAge parameter to ensure a recent authentication.
   /// </summary>
   /// <param name="result">Result from authentication.</param>
   /// <param name="request">The ongoing request.</param>
   /// <param name="controller">Current controller.</param>
   /// <returns>Action to take, or null to continue processing.</returns>
   public static IActionResult? ReAuthenticateIfNecessary(this AuthenticateResult result, OpenIddictRequest request, PageBase controller)
   {
      // Try to retrieve the user principal stored in the authentication cookie and redirect
      // the user agent to the login page (or to an external provider) in the following cases:
      //
      //  - If the user principal can't be extracted or the cookie is too old.
      //  - If prompt=login was specified by the client application.
      //  - If a max_age parameter was provided and the authentication cookie is not considered "fresh" enough.
      if (result is {Succeeded: true}       &&
          !request.HasPrompt(Prompts.Login) &&
          (request.MaxAge == null || result.Properties?.IssuedUtc == null || !(DateTimeOffset.UtcNow - result.Properties.IssuedUtc > TimeSpan.FromSeconds(request.MaxAge.Value))))
         return null;
      // If the client application requested promptless authentication, return an error indicating that the user is not logged in.
      if (request.HasPrompt(Prompts.None))
         return controller.ForbidOpenIddict(Errors.LoginRequired, "The user is not logged in.");

      // To avoid endless login -> authorization redirects, the prompt=login flag
      // is removed from the authorization request payload before redirecting the user.
      var prompt = new StringValues(request.GetPrompts().Where(x => x != Prompts.Login).ToArray());

      var httpRequest = controller.Request;
      var parameters = httpRequest.HasFormContentType
         ? httpRequest.Form.Where(parameter => parameter.Key  != Parameters.Prompt).ToList()
         : httpRequest.Query.Where(parameter => parameter.Key != Parameters.Prompt).ToList();

      parameters.Add(KeyValuePair.Create(Parameters.Prompt, prompt));

      return controller.Challenge(new AuthenticationProperties {RedirectUri = httpRequest.PathBase + httpRequest.Path + QueryString.Create(parameters)});
   }

   /// <summary>
   /// Helper to return errors to the client.
   /// </summary>
   /// <param name="controller">Current controller.</param>
   /// <param name="errorType">The error type, usually a value from <see cref="Errors"/>.</param>
   /// <param name="errorText">The custom text to return.</param>
   /// <returns>Access denied.</returns>
   public static ForbidResult ForbidOpenIddict(this PageBase controller, string errorType, string errorText) =>
      controller.Forbid(authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
         properties: new(new Dictionary<string, string?> {
            [OpenIddictServerAspNetCoreConstants.Properties.Error]            = errorType,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = errorText
         }));

   /// <summary>
   /// Get current request with error check.
   /// </summary>
   /// <param name="controller">Current controller.</param>
   /// <returns>The request.</returns>
   /// <exception cref="InvalidOperationException"></exception>
   public static OpenIddictRequest GetOpenIddictServerRequest(this PageBase controller) =>
      controller.HttpContext.GetOpenIddictServerRequest() ??
      throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");


}
#endif
