#if NET
using System.Collections.Immutable;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;

namespace Openiddict.Contrib.Server.MinimalApiHelpers;

/// <summary>
/// Base class for granting the actual access token to Client Credentials clients.
/// For refresh tokens, use <seealso cref="AuthorizationFlowAndRefreshTokenExchangeHelperBase"/>
/// </summary>
public abstract class ClientCredentialsTokenExchangeHelperBase(IOpenIddictApplicationManager applicationManager, IDestinationManager destination)
{
   /// <summary>
   /// Process the request if it is a client credentials grant.
   /// </summary>
   /// <param name="request">Ongoing request.</param>
   /// <returns>Further handling of the process, or null if not a Client Credentials request</returns>
   public async Task<IResult?> Process(OpenIddictRequest request)
   {
      if (!request.IsClientCredentialsGrantType()) return null;

      var application = await applicationManager.FindByClientIdAsync(request.ClientId!) ??
                        throw new InvalidOperationException("The application details cannot be found in the database.");

      // Create the claims-based identity that will be used by OpenIddict to generate tokens.
      var identity = new ClaimsIdentity(authenticationType: TokenValidationParameters.DefaultAuthenticationType, nameType: Claims.Name, roleType: Claims.Role);

      identity.SetClaim(Claims.Name, await applicationManager.GetDisplayNameAsync(application));

      identity.SetClaim(Claims.Subject, await MapClientCredentialToUserId(application));

      identity.SetScopes(await SetClaimsAndScopes(identity, request, application));

      identity.SetDestinations(destination.GetDestinations);

      return Results.SignIn(new(identity), null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
   }

   /// <summary>
   /// Set the claims, scopes for the identity. Use <see cref="IDestinationManager"/> to set the destinations, ie if they are exported to the client.
   /// You only _need_ to set the information here that is required in your API to determine if the user is allowed to access the resource.
   /// You _can_ set all sorts of personal info here that will be stored in the identity, but the userinfo endpoint is a better solution for that.
   /// </summary>
   /// <param name="identity">The identity to mutate to set claims etc.</param>
   /// <param name="request">The successful request.</param>
   /// <param name="clientApplication">The object that contains the client application. Use with <see cref="IOpenIddictApplicationManager"/>.</param>
   /// <returns>The final scopes granted.</returns>
   protected abstract Task<ImmutableArray<string>> SetClaimsAndScopes(ClaimsIdentity identity, OpenIddictRequest request, object clientApplication);

   /// <summary>
   /// Map the client credential to the ClaimPrincipal subject. Used in <seealso cref="AuthorizationFlowAndRefreshTokenExchangeHelperBase.ValidateUserAccount"/>.
   /// </summary>
   /// <param name="application">Current client application</param>
   /// <returns>The user the client will be identified as.</returns>
   protected abstract Task<string?> MapClientCredentialToUserId(object application);
}

#endif
