#if NET
using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Openiddict.Contrib.Server.ControllerHelpers;

/// <summary>
/// Example of a class that uses the userinfo endpoint to return all claims in the identity.
/// <example>
/// <code>
/// public sealed class MyAccessGranter : AllInfoInUserinfoEndpoint {...}
/// services.AddScoped&lt;MyAccessGranter&gt;();
/// public sealed class AuthorizationController(MyAccessGranter _accessGranter) : ControllerBase {...}
/// </code></example>
/// </summary>
public abstract class AllInfoInUserinfoEndpoint(IOpenIddictApplicationManager applicationManager, IOpenIddictAuthorizationManager authorizationManager, IDestinationManager destination) 
   : OidcAccessGranterBase(applicationManager, authorizationManager, destination)
{
   /// <summary>
   /// This simple stores the scopes in the identity, and the userinfo endpoint will return all other claims in the identity.
   /// </summary>
   protected override Task<ImmutableArray<string>> SetClaimsAndGetScopes(ClaimsIdentity identity, OpenIddictRequest request,
      string userId, object application, ControllerBase controller)
   {
      var scopes        = request.GetScopes();
      //A child class can override this method to add additional claims to the identity.
      //e.g. use the scope manager to set specific resources (servers) for the scopes.
      //identity.SetResources(await _scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());
      return Task.FromResult(scopes);
   }
}
#endif