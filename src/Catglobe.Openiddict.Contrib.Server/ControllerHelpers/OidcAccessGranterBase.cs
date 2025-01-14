#if NET
using System.Collections.Immutable;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server.AspNetCore;
using System.Diagnostics;
using System.Security.Claims;
using Openiddict.Contrib.Server;
using OpenIddict.Abstractions;

namespace Openiddict.Contrib.Server.ControllerHelpers;

/// <summary>
/// An opinionated implementation of the authentication flow.
/// This is a base class that you can inherit from to implement your own flow.
/// <example>
/// <code>
/// public sealed class MyAccessGranter : OidcAccessGranterBase {...}
/// services.AddScoped&lt;MyAccessGranter&gt;();
/// public sealed class AuthorizationController(MyAccessGranter _accessGranter) : ControllerBase {...}
/// </code></example>
/// </summary>
public abstract class OidcAccessGranterBase(IOpenIddictApplicationManager applicationManager, IOpenIddictAuthorizationManager authorizationManager, IDestinationManager destination)
{
   /// <summary>
   /// Method to call on the initial request of the authentication flow.
   /// <example>
   /// <code>
   /// [HttpGet("~/connect/authorize"), HttpPost("~/connect/authorize"), IgnoreAntiforgeryToken]
   /// public Task&lt;IActionResult&gt; Authorize() =&gt; _accessGranter.HandleAuthenticationFlowRequest(this);
   /// </code></example>
   /// </summary>
   /// <param name="controller">Current controller</param>
   /// <param name="showConsent">Callback to show the consent form.</param>
   /// <returns>Further handling of the request</returns>
   public virtual async Task<IActionResult> HandleAuthenticationFlowRequest(ControllerBase controller, Func<IOpenIddictApplicationManager, object, OpenIddictRequest, Task<IActionResult>> showConsent)
   {
      var request = controller.GetOpenIddictServerRequest();
      var result = await controller.HttpContext.AuthenticateAsync();
      if (result.ReAuthenticateIfNecessary(request, controller) is { } challengeOrError) return challengeOrError;
      return await GrantAccessIfConsent(controller, result.Principal!, request, false, showConsent);
   }

   /// <summary>
   /// Method to call after explicit consent has been given.
   /// <example>
   /// <code>
   /// [Authorize, FormValueRequired("submit.Accept"), HttpPost("~/connect/authorize"), ValidateAntiForgeryToken]
   /// public Task&lt;IActionResult&gt; Accept() =&gt; _accessGranter.HandleConsentGranted(this);
   /// </code></example>
   /// </summary>
   /// <param name="controller">Current controller</param>
   /// <returns>Further handling of the request</returns>
   public virtual Task<IActionResult> HandleConsentGranted(ControllerBase controller) => GrantAccessIfConsent(controller, controller.User, controller.GetOpenIddictServerRequest(), true, null);

   /// <summary>
   /// Handle explicit consent denied.
   /// <example>
   /// <code>
   /// [Authorize, FormValueRequired("submit.Deny"), HttpPost("~/connect/authorize"), ValidateAntiForgeryToken]
   /// public Task&lt;IActionResult&gt; Deny() =&gt; _accessGranter.HandleConsentDenied(this);
   /// </code></example>
   /// </summary>
   /// <param name="controller">Current controller.</param>
   /// <returns>Access denied back to the client.</returns>
   public virtual IActionResult HandleConsentDenied(ControllerBase controller) => controller.Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);


   /// <summary>
   /// Handle granting access if sufficient consent given (which might be in the form of an implicit consent client).
   /// This will set up the identity and return a sign in result, or a consent form.
   /// </summary>
   /// <param name="controller">Current controller.</param>
   /// <param name="claimsPrincipal">The authenticated claims principal.</param>
   /// <param name="request">The ongoing request.</param>
   /// <param name="hasConsent">True if the <paramref name="request"/> has gone through the explicit consent form.</param>
   /// <param name="showConsent"></param>
   /// <returns>A sign in result, a consent form or forbid errors.</returns>
   protected virtual async Task<IActionResult> GrantAccessIfConsent(ControllerBase controller,
      ClaimsPrincipal claimsPrincipal, OpenIddictRequest request, bool hasConsent,
      Func<IOpenIddictApplicationManager, object, OpenIddictRequest, Task<IActionResult>>? showConsent)
   {
      Debug.Assert(claimsPrincipal.Identity?.Name is not null);
      Debug.Assert(request.ClientId is not null);

      var userId = await GetUserId(claimsPrincipal, controller);

      // Retrieve the application details from the database.
      var application = await applicationManager.FindByClientIdAsync(request.ClientId) ??
                        throw new InvalidOperationException("Details concerning the calling client application cannot be found.");

      var client = await applicationManager.GetIdAsync(application) ?? throw new InvalidOperationException("Details concerning the calling application cannot be found.");
      // Retrieve the permanent authorizations associated with the user and the calling client application.
      var authorization = await authorizationManager.FindAsync(subject: userId,
                                                               client: client,
                                                               status: Statuses.Valid,
                                                               type  : AuthorizationTypes.Permanent,
                                                               scopes: request.GetScopes()).FirstOrDefaultAsync();
      var hasExistingAuth = authorization is not null;
      switch (await applicationManager.GetConsentTypeAsync(application))
      {
      // If the consent is external (e.g. when authorizations are granted by a sysadmin),
      // immediately return an error if no authorization can be found in the database.
      case ConsentTypes.External when (!hasConsent && !hasExistingAuth):
         return controller.ForbidOpenIddict(Errors.ConsentRequired, "The logged in user is not allowed to access this client application.");

      // If the consent is implicit or if an authorization was found,
      // return an authorization response without displaying the consent form.
      case ConsentTypes.Implicit:
      case ConsentTypes.External when hasConsent:
      case ConsentTypes.Explicit when hasConsent:
      case ConsentTypes.External when hasExistingAuth:
      case ConsentTypes.Explicit when hasExistingAuth && !request.HasPromptValue(PromptValues.Consent): {
         var identity = await CreateClaimsIdentity(request, userId, authorization, application, controller);

         return controller.SignIn(new(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
      }
      // At this point, no authorization was found in the database and an error must be returned
      // if the client application specified prompt=none in the authorization request.
      case ConsentTypes.Explicit   when request.HasPromptValue(PromptValues.None):
      case ConsentTypes.Systematic when request.HasPromptValue(PromptValues.None):
         return controller.ForbidOpenIddict(Errors.ConsentRequired, "Interactive user consent is required.");
      case { } when hasConsent:
         return controller.ForbidOpenIddict(Errors.RequestNotSupported, "Detected loop in consent");

      default:
         // In every other case, render the consent form.
         return await showConsent!(applicationManager, application, request);
      }
   }

   /// <summary>
   /// Get the user id from the default scheme claims principal. Default implementation uses the Subject claim.
   /// </summary>
   /// <param name="claimsPrincipal">The authenticated claims principal.</param>
   /// <param name="controller">Current controller.</param>
   /// <returns>User Id from the authenticated user.</returns>
   protected virtual Task<string> GetUserId(ClaimsPrincipal claimsPrincipal, ControllerBase controller) => Task.FromResult(claimsPrincipal.GetClaim(Claims.Subject)!);

   /// <summary>
   /// Map the request to an actual identity.
   /// </summary>
   /// <param name="request">The successful request.</param>
   /// <param name="userId">UserId returned from <see cref="GetUserId"/>.</param>
   /// <param name="existingAuth">The object that contains an existing authorization, if null create a new to store in db. Use with <see cref="IOpenIddictAuthorizationManager"/>.</param>
   /// <param name="clientApplication">The object that contains the client application. Use with <see cref="IOpenIddictApplicationManager"/>.</param>
   /// <param name="controller">Use to extract info from consent, e.g. if user only gave partial consent.</param>
   /// <returns>The final identity that openiddict will store.</returns>
   protected virtual async Task<ClaimsIdentity> CreateClaimsIdentity(OpenIddictRequest request, string userId,
      object? existingAuth, object clientApplication, ControllerBase controller)
   {
      var identity = new ClaimsIdentity(authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                                        nameType: Claims.Name,
                                        roleType: Claims.Role);

      identity.SetClaim(Claims.Subject, userId);

      var scopes = await SetClaimsAndGetScopes(identity, request, userId, clientApplication, controller);
      identity.SetScopes(scopes);

      // Create a permanent authorization to avoid requiring explicit consent for future authorization or token requests containing the same scopes.
      var clientId = await applicationManager.GetIdAsync(clientApplication);
      existingAuth ??= await authorizationManager.CreateAsync(identity: identity,
                                                              subject: userId,
                                                              client  : clientId!,
                                                              type    : AuthorizationTypes.Permanent,
                                                              scopes  : scopes);

      identity.SetAuthorizationId(await authorizationManager.GetIdAsync(existingAuth));
      identity.SetDestinations(destination.GetDestinations);
      return identity;
   }

   /// <summary>
   /// Set the claims, scopes for the identity. Use <see cref="IDestinationManager"/> to set the destinations, ie if they are exported to the client.
   /// You only _need_ to set the information here that is required in your API to determine if the user is allowed to access the resource.
   /// You _can_ set all sorts of personal info here that will be stored in the identity, but the userinfo endpoint is a better solution for that.
   /// </summary>
   /// <param name="identity">The identity to mutate to set claims etc.</param>
   /// <param name="request">The successful request.</param>
   /// <param name="userId">UserId returned from <see cref="GetUserId"/>.</param>
   /// <param name="clientApplication">The object that contains the client application. Use with <see cref="IOpenIddictApplicationManager"/>.</param>
   /// <param name="controller">Use to extract info from consent, e.g. if user only gave partial consent.</param>
   /// <returns>The final scopes granted.</returns>
   protected abstract Task<ImmutableArray<string>> SetClaimsAndGetScopes(ClaimsIdentity identity, OpenIddictRequest request,
      string userId, object clientApplication, ControllerBase controller);
}

#endif
