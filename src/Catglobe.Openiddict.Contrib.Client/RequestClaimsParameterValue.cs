#if NET
using System.Text.Json.Nodes;

namespace Openiddict.Contrib.Client;

/// <summary>
/// Helper class for setting parameters matching https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter.
/// </summary>
public class RequestClaimsParameterValue
{
   private readonly Dictionary<string, JsonNode?> _userinfo = new();
   private readonly Dictionary<string, JsonNode?> _claims = new();

   /// <summary>
   /// Specify a user info claim that should be returned from the authentication request.
   /// </summary>
   /// <param name="claim">See <see cref="Claims"/>.</param>
   /// <param name="essential">Indicates whether the Claim being requested is an Essential Claim. If the value is true, this indicates that the Claim is an Essential Claim.</param>
   /// <returns>This class for chaining.</returns>
   public RequestClaimsParameterValue UserInfoClaim(string claim, bool essential = false)
   {
      _userinfo.Add(claim, AsNode(essential, null, null));
      return this;
   }

   /// <summary>
   /// Specify a user info claim that should be returned from the authentication request.
   /// </summary>
   /// <param name="claim">See <see cref="Claims"/>.</param>
   /// <param name="value">Requests that the Claim be returned with a particular value.</param>
   /// <param name="essential">Indicates whether the Claim being requested is an Essential Claim. If the value is true, this indicates that the Claim is an Essential Claim.</param>
   /// <returns>This class for chaining.</returns>
   public RequestClaimsParameterValue UserInfoClaim(string claim, string value, bool essential = false)
   {
      _userinfo.Add(claim, AsNode(essential, value, null));
      return this;
   }

   /// <summary>
   /// Specify a user info claim that should be returned from the authentication request.
   /// </summary>
   /// <param name="claim">See <see cref="Claims"/>.</param>
   /// <param name="values">Requests that the Claim be returned with one of a set of values, with the values appearing in order of preference.</param>
   /// <param name="essential">Indicates whether the Claim being requested is an Essential Claim. If the value is true, this indicates that the Claim is an Essential Claim.</param>
   /// <returns>This class for chaining.</returns>
   public RequestClaimsParameterValue UserInfoClaim(string claim, IReadOnlyCollection<string> values, bool essential)
   {
      _userinfo.Add(claim, AsNode(essential, null, values));
      return this;
   }

   private static JsonObject? AsNode(bool essential, string? value, IReadOnlyCollection<string?>? values)
   {
      var json = new JsonObject(GetJsonNode(essential, value, values));
      return json.Count == 0 ? default : json;
   }

   /// <summary>
   /// Specify a claim that should be returned from the authentication request.
   /// </summary>
   /// <param name="claim">See <see cref="Claims"/>.</param>
   /// <param name="essential">Indicates whether the Claim being requested is an Essential Claim. If the value is true, this indicates that the Claim is an Essential Claim.</param>
   /// <returns>This class for chaining.</returns>
   public RequestClaimsParameterValue IdTokenClaim(string claim, bool essential = false)
   {
      _claims.Add(claim, AsNode(essential, null, null));
      return this;
   }

   /// <summary>
   /// Specify a claim that should be returned from the authentication request.
   /// </summary>
   /// <param name="claim">See <see cref="Claims"/>.</param>
   /// <param name="value">Requests that the Claim be returned with a particular value.</param>
   /// <param name="essential">Indicates whether the Claim being requested is an Essential Claim. If the value is true, this indicates that the Claim is an Essential Claim.</param>
   /// <returns>This class for chaining.</returns>
   public RequestClaimsParameterValue IdTokenClaim(string claim, string value, bool essential = false)
   {
      _claims.Add(claim, AsNode(essential, value, null));
      return this;
   }

   /// <summary>
   /// Specify a claim that should be returned from the authentication request.
   /// </summary>
   /// <param name="claim">See <see cref="Claims"/>.</param>
   /// <param name="values">Requests that the Claim be returned with one of a set of values, with the values appearing in order of preference.</param>
   /// <param name="essential">Indicates whether the Claim being requested is an Essential Claim. If the value is true, this indicates that the Claim is an Essential Claim.</param>
   /// <returns>This class for chaining.</returns>
   public RequestClaimsParameterValue IdTokenClaim(string claim, IReadOnlyCollection<string> values, bool essential)
   {
      _claims.Add(claim, AsNode(essential, null, values));
      return this;
   }

   /// <summary>
   /// Convert the current settings to a <see cref="OpenIddictParameter"/> that can be used in a request.
   /// <example>
   /// <code>
   /// new AuthenticationProperties().SetParameter(Parameters.Claims, new RequestClaimsParameterValue().IdTokenClaim(Claims.AuthenticationTime, true).AsOpenIddictParameter());
   /// </code></example>
   /// </summary>
   /// <returns></returns>
   public OpenIddictParameter? AsOpenIddictParameter()
   {
      if (_userinfo.Count == 0 && _claims.Count == 0)
         return null;
      var lst = new List<KeyValuePair<string, JsonNode?>>();

      if (_userinfo.Count != 0)
         lst.Add(new("userinfo", new JsonObject(_userinfo)));
      if (_claims.Count != 0)
         lst.Add(new(Parameters.IdToken, new JsonObject(_claims)));
      return new OpenIddictParameter(new JsonObject(lst));
   }

   private static IEnumerable<KeyValuePair<string, JsonNode?>> GetJsonNode(bool essential, string? value, IReadOnlyCollection<string?>? values)
   {
      if (essential)
         yield return new("essential", true);
      if (value is not null)
         yield return new("value", value!);
      if (values is not null)
         yield return new("values", new JsonArray(values.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()));
   }


}
#endif
