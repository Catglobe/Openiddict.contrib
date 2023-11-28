#if NET
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Net.Http.Headers;

namespace Openiddict.Contrib.Server;

public static class CookieSchemeHelpers
{
   /// <summary>
   /// The default cookie settings only care about XHR requests, but use this if you want to return 401/403 for API-like calls too.
   /// Technically this will see if the client requested json (via Accept header), or if the client is sending json or xml data (via Content-type header).
   /// <example>For example:
   /// <code>
   /// services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =&gt;
   /// {
   ///   ...
   ///   o.ReturnStatusCodesOnAuthFailuresForApiCalls();
   /// });
   /// </code></example>
   /// </summary>
   /// <param name="o">The options object</param>
   /// <returns>The options object</returns>
   public static CookieAuthenticationOptions ReturnStatusCodesOnAuthFailuresForApiCalls(this CookieAuthenticationOptions o)
   {
      var origLogin = o.Events.OnRedirectToLogin;
      o.Events.OnRedirectToLogin = (ctx) => {
         if (!IsApiCall(ctx.Request)) return origLogin(ctx);
         ctx.Response.StatusCode = 401;
         return Task.CompletedTask;
      };
      var origAccessDenied = o.Events.OnRedirectToAccessDenied;
      o.Events.OnRedirectToAccessDenied = (ctx) => {
         if (!IsApiCall(ctx.Request)) return origAccessDenied(ctx);
         ctx.Response.StatusCode = 403;
         return Task.CompletedTask;
      };
      return o;
      static bool IsApiCall(HttpRequest req) => req.HasJsonContentType() || 
                                                (MediaTypeHeaderValue.TryParseList(req.Headers.Accept, out var lst) && 
                                                 lst.Any(x=>x.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) || 
                                                            x.Suffix.Equals("json", StringComparison.OrdinalIgnoreCase)) || 
                                                 req.ContentType?.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase) == true);

   }
}
#endif
