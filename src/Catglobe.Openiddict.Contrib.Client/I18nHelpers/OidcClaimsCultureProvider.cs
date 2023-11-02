#if NET
using Microsoft.AspNetCore.Localization;
using System.Security.Claims;

namespace Openiddict.Contrib.Client.I18nHelpers;

/// <summary>
/// Pull the <see cref="OpenIddictConstants.Claims.Locale"/> claim from the user and use that as the culture.
/// Also respects the non-standard "ui-locales" claim if present, in which case <see cref="OpenIddictConstants.Claims.Locale"/> is interpreted as the culture for formatting numbers, dates etc.
/// <example><code>
/// host.UseRequestLocalization(o =&gt; {
///   var cultures = ...;
///   o.AddSupportedCultures(cultures)
///   .AddSupportedUICultures(cultures)
///   .SetDefaultCulture(cultures[0]);
/// //insert before the final default provider (the AcceptLanguageHeaderRequestCultureProvider)
/// o.RequestCultureProviders.Insert(o.RequestCultureProviders.Count - 1, new OidcClaimsCultureProvider {Options = o});
/// });
/// </code></example>
/// </summary>
public class OidcClaimsCultureProvider : RequestCultureProvider
{
   ///<inheritdoc/>
   public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext) => Task.FromResult(GetCultureFromClaims(httpContext));

   private static ProviderCultureResult? GetCultureFromClaims(HttpContext ctx)
   {
      var userCulture   = ctx.User.GetClaim(OpenIddictConstants.Claims.Locale);
      var userUiCulture = ctx.User.GetClaim(OpenIddictConstants.Parameters.UiLocales) ?? userCulture;
      if (userUiCulture == null) goto noneFound;

      return new(userCulture ?? userUiCulture, userUiCulture);
      noneFound:
      return null;
   }

   /// <summary>
   /// Add appropriate claims for the given culture to the identity.
   /// You probably want to use <see cref="CopyClaims"/> during login callback instead.
   /// </summary>
   /// <param name="identity">Identity to populate</param>
   /// <param name="culture">Culture to use in BCP47 [RFC5646] format.</param>
   public static void AddClaims(ClaimsIdentity identity, string? culture)
   {
      if (culture != null) identity.AddClaim(new Claim(OpenIddictConstants.Claims.Locale, culture.Replace('_', '-')));
   }

   /// <summary>
   /// Add standard claims for the given culture to the identity, plus the custom "ui-locales".
   /// You would usually use this if you are loading the culture info from db or similar.
   /// You probably want to use <see cref="CopyClaims"/> during login callback instead.
   /// </summary>
   /// <param name="identity">Identity to populate</param>
   /// <param name="culture">Culture to use in BCP47 [RFC5646] format.</param>
   /// <param name="uiCulture">UI Culture to use in BCP47 [RFC5646] format. Ignored if same as culture.</param>
   public static void AddClaims(ClaimsIdentity identity, string? culture, string? uiCulture)
   {
      AddClaims(identity, culture);
      if (uiCulture != null && uiCulture != culture) identity.AddClaim(new(OpenIddictConstants.Parameters.UiLocales, uiCulture));
   }

   /// <summary>
   /// Copy appropriate claims for the given culture to the identity from remote.
   /// </summary>
   /// <param name="identity">Identity to populate.</param>
   /// <param name="remote">Remote claims.</param>
   public static void CopyClaims(ClaimsIdentity identity, ClaimsPrincipal remote) => AddClaims(identity, remote.GetClaim(OpenIddictConstants.Claims.Locale), remote.GetClaim(OpenIddictConstants.Parameters.UiLocales));

}
#endif
