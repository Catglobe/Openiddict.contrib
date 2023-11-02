using System.Globalization;
using System.Security.Claims;

namespace Openiddict.Contrib.Server.I18nHelpers;

/// <summary>
/// Set the <see cref="OpenIddictConstants.Claims.Locale"/> claim from the user and use that as the culture.
/// <example><code>
/// host.UseRequestLocalization(o =&gt; {
///   var cultures = ...;
///   o.AddSupportedCultures(cultures)
///   .AddSupportedUICultures(cultures)
///   .SetDefaultCulture(cultures[0])
///   .RequestCultureProviders = new List&lt;IRequestCultureProvider&gt; {
///   new QueryStringRequestCultureProvider {Options          = o},
///   new CookieRequestCultureProvider {Options               = o},
///   new SoapClaimsCultureProvider {Options                  = o},
///   new OidcClaimsCultureProvider {Options                  = o},
///   new AcceptLanguageHeaderRequestCultureProvider {Options = o},
///   };
/// });
/// </code></example>
/// </summary>
public class OidcClaimsCultureProviderHelper
{
   /// <summary>
   /// Infer culture from current culture and add appropriate claims to the identity.
   /// You would usually use this if you already have infrastructure in place to set the culture.
   /// </summary>
   /// <param name="identity">Identity to populate</param>
   public static void AddClaimsFromCurrentCulture(ClaimsIdentity identity) => AddClaims(identity, CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name);

   /// <summary>
   /// Add standard claims for the given culture to the identity.
   /// You would usually use this if you are loading the culture info from db or similar.
   /// </summary>
   /// <param name="identity">Identity to populate</param>
   /// <param name="culture">Culture to use in BCP47 [RFC5646] format.</param>
   public static void AddClaims(ClaimsIdentity identity, string? culture)
   {
      if (culture != null) identity.AddClaim(new(OpenIddictConstants.Claims.Locale, culture));
   }

   /// <summary>
   /// Add standard claims for the given culture to the identity, plus the custom "ui-locales".
   /// You would usually use this if you are loading the culture info from db or similar.
   /// </summary>
   /// <param name="identity">Identity to populate</param>
   /// <param name="culture">Culture to use in BCP47 [RFC5646] format.</param>
   /// <param name="uiCulture">UI Culture to use in BCP47 [RFC5646] format. Ignored if same as culture.</param>
   public static void AddClaims(ClaimsIdentity identity, string? culture, string? uiCulture)
   {
      AddClaims(identity, culture);
      if (uiCulture != null && uiCulture != culture) identity.AddClaim(new(OpenIddictConstants.Parameters.UiLocales, uiCulture));
   }
}