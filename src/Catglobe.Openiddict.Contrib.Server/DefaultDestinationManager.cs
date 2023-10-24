using System.Security.Claims;

namespace Openiddict.Contrib.Server;

/// <summary>
/// Default class to determine where a claim should be sent.
/// If one of the standard Oidc scopes is set, it will include the corresponding claim(s) in the identity token.
/// You can also use it in the userinfo endpoint to determine which claims should be returned.
/// </summary>
public class DefaultDestinationManager : IDestinationManager
{
    protected static readonly IEnumerable<string> DestinationNone = [];
    protected static readonly IEnumerable<string> DestinationToken = [Destinations.AccessToken];
    protected static readonly IEnumerable<string> DestinationBoth = [Destinations.AccessToken, Destinations.IdentityToken];
    protected static readonly IEnumerable<string> DestinationId = [Destinations.IdentityToken];

    /// <summary>
    /// Determine which info ends up in which tokens.
    /// </summary>
    /// <param name="claim">Current to look at.</param>
    /// <returns>A list of tokens to store the claim in.</returns>
    public virtual IEnumerable<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            not null when claim.Subject is null => DestinationNone, //should not happen, but makes compiler happy
            Claims.Subject => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Name => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Gender => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.GivenName => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.MiddleName => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.FamilyName => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Nickname => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.PreferredUsername => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Birthdate => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Profile => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Picture => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Website => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Locale => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Zoneinfo => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.UpdatedAt => IncludeInIdentityToken(Scopes.Profile) ? DestinationBoth : DestinationToken,
            Claims.Email => IncludeInIdentityToken(Scopes.Email) ? DestinationBoth : DestinationToken,
            Claims.Role => IncludeInIdentityToken(Scopes.Roles) ? DestinationBoth : DestinationToken,
            Claims.PhoneNumber => IncludeInIdentityToken(Scopes.Phone) ? DestinationBoth : DestinationToken,
            // Never include the security stamp in the access and identity tokens, as it's a secret value.
            "AspNet.Identity.SecurityStamp" => DestinationNone,
            _ => DestinationToken
        };

        bool IncludeInIdentityToken(string type) => claim.Subject.HasScope(type);
    }
}