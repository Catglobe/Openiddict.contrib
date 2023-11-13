# Catglobe.Openiddict.Contrib.Pruning.BackgroundService
Alternative to quartz pruning

Usage:

```csharp
services.AddOpenIddict()
   .AddCore(options =>
   {
      ...
      options.UseBackgroundServicePruning();
   })
```

Defaults will start the prune 1-10 (random) minutes after startup, and then every 1h after this.

The method is just fine for a handful of servers, but if you have a lot of servers, you should use quartz instead to ensure just a single prune process is run for your entire cluster.

# Catglobe.Openiddict.Contrib.Client

Tons of helper classses and extension methods to make implementing a client project trivial.

Usage:

## Login/logout process helpers

### Controller helpers
Makes it trivial to implement a client controller that can handle external logins using the authentication code flow.
```csharp
public class AuthenticationController : Controller
{
   [HttpGet("~/login")]
   public ActionResult LogIn(string returnUrl) => this.InitiateAuthorizationCodeLogin(returnUrl, "mgnt_user");

   [HttpGet("~/callback/login"), HttpPost("~/callback/login"), IgnoreAntiforgeryToken]
   public Task<ActionResult> LogInCallback() => this.StoreRemoteAuthInSchemeAsync(resultPrincipal =>
   {
      var requiredClaim = new HashSet<string>
      {
         "Common",
      };
      requiredClaim.ExceptWith(resultPrincipal.GetClaim(OpenIddictConstants.Claims.Role)?.Split(" ") ?? []);

      if (requiredClaim is { Count: > 0 } missingPermissions)
         return View("NotEnoughPermission",
            new NotEnoughPermissionModel { PermissionsMissing = missingPermissions.ToList() });
      return null;
   });

}
```

and logouts:

```csharp
public class AuthenticationController : Controller
{
   [HttpPost("~/logout"), ValidateAntiForgeryToken]
   public Task<ActionResult> LogOut(string returnUrl) => this.LocalAndRemoteLogOutAsync(returnUrl);

   [HttpGet("~/callback/logout"), HttpPost("~/callback/logout"), IgnoreAntiforgeryToken]
   public Task<ActionResult> LogOutCallback() => this.RemoteLogOutCallbackAsync();
}
```

### Razor page helpers
Makes it trivial to implement a client razor page that can handle external logins using the authentication code flow.

Notice this uses callback url to `/login/callback`, instead of `/callback/login` as the controller example above.

```csharp
[IgnoreAntiforgeryToken]
public class LoginModel : PageModel
{
   public List<string>? PermissionsMissing { get; private set; }

   //as a page
   public void OnGet() {}
   public ChallengeResult OnPost([FromQuery]string? returnUrl) => this.InitiateAuthorizationCodeLogin(returnUrl ?? "/", "my-client");

   //or as a button somewhere else
   //public ChallengeResult OnGet([FromQuery]string? returnUrl) => this.InitiateAuthorizationCodeLogin(returnUrl ?? "/", "my-client");

   public Task<ActionResult> OnGetCallbackAsync() => CallbackAsync();
   public Task<ActionResult> OnPostCallbackAsync() => CallbackAsync();

   private Task<ActionResult> CallbackAsync() => this.StoreRemoteAuthInSchemeAsync(resultPrincipal =>
   {
      var requiredClaim = new HashSet<string>
      {
         "Common",
      };
      requiredClaim.ExceptWith(resultPrincipal.GetClaim(OpenIddictConstants.Claims.Role)?.Split(" ") ?? []);

      if (requiredClaim is not { Count: > 0 } missingPermissions) return null;

      //show the user the problem
      PermissionsMissing = missingPermissions.ToList();
      return Page();
   });
}
```

```html
@page "{handler?}" //<----------------- Notice this
@model LoginModel

@if (Model.PermissionsMissing is null)
{
 ViewData["Title"] = "Login";
 <h1>Welcome to my Example!</h1>
 <form method="post">
     <button class="btn btn-lg" type="submit">Sign in></button>
 </form>
}
else
{
 ViewData["Title"] = "Missing permissions";
<h1 class="text-danger">Missing permissions.</h1>

<p class="text-danger"><span class="text-success">Authentication on the remote site was successful</span>, but you are lacking the required scope to access this site.</p>
<ul>
 @foreach (var item in Model.PermissionsMissing)
 {
  <li>Missing: @item</li>
 }
</ul>
}
```

and logouts:

```csharp

[IgnoreAntiforgeryToken]
public class LogoutModel : PageModel
{
 public Task<ActionResult> OnGetAsync([FromQuery]string? returnUrl) => this.LocalAndRemoteLogOutAsync(returnUrl ?? "/");

 public Task<ActionResult> OnGetCallbackAsync() => this.RemoteLogOutCallbackAsync();
 public Task<ActionResult> OnPostCallbackAsync() => this.RemoteLogOutCallbackAsync();
}
```

```html
@page "{handler?}"
@model LogoutModel
```

### Minimal API helpers

Makes it trivial to implement a client minimal api that can handle external logins using the authentication code flow.

```csharp
//call from login page, if you require user is logged out before logging in again
host.MapGet("/logoutAndRedirect", (Delegate)([AllowAnonymous](HttpContext httpContext) => httpContext.EnsureLoggedOut($"/login{httpContext.Request.QueryString.Value ?? ""}")));

//post to this endpoint from the login page
host.MapPost("/login-oidc", [AllowAnonymous](HttpContext httpContext, string returnUrl) => httpContext.InitiateAuthorizationCodeLogin(returnUrl, providerName));
//OR
//navigate to this endpoint from the login page
host.MapGet("/login-oidc", [AllowAnonymous](HttpContext httpContext, string returnUrl) => httpContext.InitiateAuthorizationCodeLogin(returnUrl, providerName));

//the configured callback url of the oidc client
host.MapGet("/login/callback", (Delegate)((HttpContext httpContext) => httpContext.StoreRemoteAuthInSchemeAsync(customCheck, storeRemoteInfo)));

//post to this endpoint from the logout page
host.MapPost("/logout-oidc", [AllowAnonymous](HttpContext httpContext, string returnUrl = "/") => httpContext.LocalAndRemoteLogOutAsync(returnUrl));
//the configured callback url of the oidc client
host.MapGet("/logout/callback", (Delegate)((HttpContext httpContext) => httpContext.RemoteLogOutCallbackAsync()));
```

You can also add a dummy parameter to get rid of the cast to `Delegate`:

```csharp
host.MapGet("/logoutAndRedirect", [AllowAnonymous](HttpContext httpContext, string? _ = null) => httpContext.EnsureLoggedOut($"/login{httpContext.Request.QueryString.Value ?? ""}"));
```

Blazor login page example:

```csharp
@inject NavigationManager NavigationManager
<EditForm OnValidSubmit="Submit">...</EditForm>
@code {
 [SupplyParameterFromQuery]
 private string ReturnUrl { get; set; } = "/";
 private void Submit() => NavigationManager.NavigateTo($"/login-oidc?returnUrl={Uri.EscapeDataString(ReturnUrl)}", forceLoad: true);
}

//and force logout before re-login:
 [CascadingParameter]
 private Task<AuthenticationState> AuthenticationState { get; set; } = default!;
 protected override async Task OnInitializedAsync()
 {
  if ((await AuthenticationState).User?.Identity?.IsAuthenticated == true) { NavigationManager.NavigateTo($"/LogoutAndRedirect{new Uri(NavigationManager.Uri).Query}", new NavigationOptions() {ForceLoad = true, ReplaceHistoryEntry = true}); }
 }
}
```

# I18n helpers

Helpers to make it trivial to implement a client that pulls the language and culture info from the server.

```csharp
host.UseRequestLocalization(o => {
   var cultures = ...;
   o.AddSupportedCultures(cultures)
    .AddSupportedUICultures(cultures)
    .SetDefaultCulture(cultures[0]);
   //insert before the final default provider (the AcceptLanguageHeaderRequestCultureProvider)
   o.RequestCultureProviders.Insert(o.RequestCultureProviders.Count - 1, new OidcClaimsCultureProvider {Options = o});
});
```

Notice, this requires the server sets the culture info in the claims.
See below for example, or see `OidcClaimsCultureProviderHelper.AddClaims` for details.

You also need to copy it from the server given claims to the local claims. E.g. if using `StoreRemoteAuthInSchemeAsync`:

```csharp
yourContext.StoreRemoteAuthInSchemeAsync(..., (identity, remote)=>OidcClaimsCultureProviderHelper.CopyClaims(identity, remote))))
```

## Claims helpers

https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter specifies how to request claims from the request.

Notice, you still have to manually check they are present in the response.

```csharp
//in the login controller
var claims = new RequestClaimsParameterValue()
                 .IdTokenClaim(Claims.AuthenticationTime, true);
this.InitiateAuthorizationCodeLogin(returnUrl, ..., claims.AsOpenIddictParameter());
...
//in the callback controller
this.StoreRemoteAuthInSchemeAsync(..., (principal)=>principal.GetClaim(Claims.AuthenticationTime)...)
```

## Http helpers

### HttpClient for authentication code flow

```csharp
services.AddSingleton<RefreshTokenAuthorizationCodeHandler>();
services.AddHttpContextAccessor();
services.AddHttpClient<ClientDemoApiService>(client => client.BaseAddress = new(serverUrl))
   .SetHandlerLifetime(TimeSpan.FromMinutes(120))
   .AddPolicyHandler(GetRetryPolicy())
   .AddHttpMessageHandler<RefreshTokenAuthorizationCodeHandler>();
```

```csharp
public class ClientDemoApiService(HttpClient http)
{
   public async Task<Dictionary<string, string>> GetAsync(CancellationToken cancellationToken) => 
      await http.GetFromJsonAsync<Dictionary<string,string>>("api/demo", cancellationToken: cancellationToken) ?? throw new();
}
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DemoApiController(ClientDemoApiService remote) : Controller
{
  public Task<Dictionary<string, string>> Remote(CancellationToken cancellationToken) => remote.GetAsync(cancellationToken);
}
```

WARNING: If an error occurs on the remote in the same request that also handled a successful token refresh the updated cookie might get lost and future requests will get a 401 from remote because old token is invalid.

### HttpClient for client credentials flow

```csharp
services.AddTransient<MyRefreshTokenClientAuthenticationHandler>();
services.AddHttpClient<AdminDemoApiService>(client => client.BaseAddress = new(serverUrl))
   .SetHandlerLifetime(TimeSpan.FromMinutes(120))
   .AddPolicyHandler(GetRetryPolicy())
   .AddHttpMessageHandler<MyRefreshTokenClientAuthenticationHandler>();
internal class MyRefreshTokenClientAuthenticationHandler(OpenIddictClientService openIddict) : RefreshTokenClientAuthenticationHandler(openIddict, "my-server-2-server-client");
```

```csharp
public class AdminDemoApiService(HttpClient http)
{
   public async Task<Dictionary<string, string>> GetAsync(CancellationToken cancellationToken) => 
      await http.GetFromJsonAsync<Dictionary<string,string>>("api/demo", cancellationToken: cancellationToken) ?? throw new();
}
[ApiController]
[Route("api/[controller]")]
public class DemoApiController(AdminDemoApiService remote) : Controller
{
  public Task<Dictionary<string, string>> Remote(CancellationToken cancellationToken) => remote.GetAsync(cancellationToken);
}
```

### Cookie helper

Setup API calls to return 401/403 instead of redirecting to login page.

```csharp
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
   .AddCookie(o =>
   {
      ...
      o.ReturnStatusCodesOnAuthFailuresForApiCalls();
   });
```

# Catglobe.Openiddict.Contrib.Server

Tons of helper classses and extension methods to make implementing a server project trivial.

### Helper Base class for Authentication flow

Various helper classes to make implementing a server trivial.

```csharp
public sealed class MyAccessGranter : OidcAccessGranterBase {...}
```
or 
```csharp
public sealed class MyAccessGranter : AllInfoInUserinfoEndpoint {...}
```

Use the base classes from the namespace `ControllerHelpers` or `RazorPageHelpers` depending on your needs.

and then register `services.AddScoped<MyAccessGranter>();` and use it:

```csharp
public sealed class AuthorizationController(MyAccessGranter accessGranter) : ControllerBase {
   [HttpGet("~/connect/authorize"), HttpPost("~/connect/authorize"), IgnoreAntiforgeryToken]
   public Task<IActionResult> Authorize() => _accessGranter.HandleAuthenticationFlowRequest(this, async (manager,client,request) => {
     return View(new MyConsentViewModel(){ApplicationName = await manager.GetDisplayNameAsync(application), Scope = request.GetScopes()});
   });

   [Authorize, FormValueRequired("submit.Accept"), HttpPost("~/connect/authorize"), ValidateAntiForgeryToken]
   public Task<IActionResult> Accept() => _accessGranter.HandleConsentGranted(this);

   [Authorize, FormValueRequired("submit.Deny"), HttpPost("~/connect/authorize"), ValidateAntiForgeryToken]
   public Task<IActionResult> Deny() => _accessGranter.HandleConsentDenied(this);
}
```

or if using razor pages:
```csharp
[IgnoreAntiforgeryToken]
public sealed class AuthorizeModel(MyAccessGranter accessGranter) : PageModel {
   public string ApplicationName { get; private set; } = default!;
   public IEnumerable<string> Scope { get; private set; } = default!;
   public Task<IActionResult> GetAsync() => Handle();
   public Task<IActionResult> PostAsync() => Handle();
   private Task<IActionResult> Handle() => _accessGranter.HandleAuthenticationFlowRequest(this, async (manager,client,request) => {
     ApplicationName = (await manager.GetDisplayNameAsync(client))!;;
     Scope = request.GetScopes();
     return Page();
   });
   public Task<IActionResult> PostConsentAsync(string action) => 
     action == "submit.Accept" ? _accessGranter.HandleConsentGranted(this) : Task.FromResult(_accessGranter.HandleConsentDenied(this));
}
```
```html
@page "{handler?}" //<----------------- Notice this
@model AuthorizeModel
@{
   Layout = "_Layout";
}
<div class="jumbotron">
 <h1>Authorization</h1>
 <p class="lead text-left">Do you want to grant <strong>@Model.ApplicationName</strong> access to your data?</p>
 <h2>Scopes requested</h2>
 <ul>
  @foreach (var scope in Model.Scope)
  {
     <li>@scope</li>
  }
 </ul>
 <form method="post">
  @Html.AntiForgeryToken()

  @foreach (var parameter in Request.HasFormContentType ? Request.Form.AsEnumerable() : Request.Query)
  {
   <input type="hidden" name="@parameter.Key" value="@parameter.Value" />
  }

  <button class="btn btn-lg btn-success" asp-page-handler="Accept">Yes</button>
  <button class="btn btn-lg btn-danger" asp-page-handler="Deny">No</button>
 </form>
</div>
```

### Token exchange helpers

Use the base classes from the namespace `ControllerHelpers` or `MinimalApiHelpers` depending on your needs.

```csharp
internal sealed class MyClientCredentialsTokenExchangeHelper : ClientCredentialsTokenExchangeHelperBase {...}
internal sealed class MyAuthFlowAndRefreshTokenHelper : AuthorizationFlowAndRefreshTokenExchangeHelperBase {...}
services.AddScoped<MyClientCredentialsTokenExchangeHelper>();
services.AddScoped<MyAuthFlowAndRefreshTokenHelper>();
```

And use:

```csharp
public sealed class TokenController(MyClientCredentialsTokenExchangeHelper enableClientCreds,
                                    MyAuthFlowAndRefreshTokenHelper enableRefreshTokenAndAuthFlow) : ControllerBase {
   [HttpPost("~/connect/token"), AllowAnonymous, IgnoreAntiforgeryToken, Produces("application/json")]
   public async Task<IActionResult> Exchange() {
     var request = this.GetOpenIddictServerRequest();
     if (await enableClientCreds.Process(this, request) is {} result) return result;
     if (await enableRefreshTokenAndAuthFlow.Process(this, request) is {} result) return result;

     return this.ForbidOpenIddict(Errors.UnsupportedGrantType, "The specified grant type is not supported.");
   }
}
```

or if using minimal api:
```csharp
app.MapPost("/connect/token", [AllowAnonymous] async (HttpContext context,
                               MyClientCredentialsTokenExchangeHelper enableClientCreds,
                               MyAuthFlowAndRefreshTokenHelper enableRefreshTokenAndAuthFlow) => {
     var request = context.GetOpenIddictServerRequest() ?? throw new();
     if (await enableClientCreds.Process(request) is {} result) return result;
     if (await enableRefreshTokenAndAuthFlow.Process(context, request) is {} result) return result;

     return AuthorizationCodeHelpers.ForbidOpenIddict(Errors.UnsupportedGrantType, "The specified grant type is not supported.");
   }
}
```

### Cookie helper

Setup API calls to return 401/403 instead of redirecting to login page.

```csharp
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
   .AddCookie(o =>
   {
      ...
      o.ReturnStatusCodesOnAuthFailuresForApiCalls();
   });
```

### I18n helper

Add OIDC standard claims for culture (`locale`) and the non-standard `ui-locales` for the ui-culture.

```csharp
public abstract class ApplyCultureToClaims(...) : OidcAccessGranterBase(...)
{
   protected override Task<ImmutableArray<string>> SetClaimsAndGetScopes(...)
   {
      OidcClaimsCultureProviderHelper.AddClaimsFromCurrentCulture(identity);
      return base.SetClaimsAndGetScopes(...);
   }
}
```