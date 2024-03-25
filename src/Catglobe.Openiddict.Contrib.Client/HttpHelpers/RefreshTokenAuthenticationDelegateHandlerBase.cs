using System.Net.Http;

namespace Openiddict.Contrib.Client.HttpHelpers;

/// <summary>
/// Base class for all Delegate handler to set the auth header for remote api calls using various authentication flows.
/// </summary>
public abstract class RefreshTokenAuthenticationDelegateHandlerBase : DelegatingHandler
{
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
   {
      var accessToken = await GetAccessTokenAsync(cancellationToken);

      //if it is missing we will get a 401 most likely, which will bubble up
      if (!string.IsNullOrEmpty(accessToken))
         request.Headers.Authorization = new("Bearer", accessToken);
      else
         await HandleMissingToken(request, cancellationToken);
      request.Headers.Accept.Add(new("application/json"));

      return await base.SendAsync(request, cancellationToken);
   }
   /// <summary>
   /// Default behaviour is to continue with the request, but you can override this to handle the missing token differently
   /// </summary>
   /// <param name="request">Request</param>
   /// <param name="cancellationToken">Token</param>
   protected virtual Task HandleMissingToken(HttpRequestMessage request, CancellationToken cancellationToken) => Task.CompletedTask;

   /// <summary>
   /// Logic to retrieve the current OIDC access token, and refresh if necessary
   /// </summary>
   /// <returns>Valid token or null</returns>
   protected abstract ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}