#if NET
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Routing;

namespace Openiddict.Contrib.Server.ControllerHelpers;

public sealed class FormValueRequiredAttribute(string name) : ActionMethodSelectorAttribute
{
   public override bool IsValidForRequest(RouteContext context, ActionDescriptor action)
   {
      var request = context.HttpContext.Request;
      if (string.Equals(request.Method, "GET",    StringComparison.OrdinalIgnoreCase) ||
          string.Equals(request.Method, "HEAD",   StringComparison.OrdinalIgnoreCase) ||
          string.Equals(request.Method, "DELETE", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(request.Method, "TRACE",  StringComparison.OrdinalIgnoreCase))
      {
         return false;
      }

      if (string.IsNullOrEmpty(request.ContentType))
      {
         return false;
      }

      if (!request.ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
      {
         return false;
      }

      return !string.IsNullOrEmpty(request.Form[name]);
   }
}
#endif
