using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;

namespace Openiddict.Contrib.Client.Tests.HttpHelpers;

[TestSubject(typeof(RefreshTokenAuthorizationCodeHandler))]
public class RefreshTokenAuthorizationCodeHandlerTest
{
   /*
   cannot figure out how to mock OpenIddictClientService

   [Fact]
   public async Task WhenLoggedInWithValidToken_ShouldNotRefreshToken()
   {
      // Arrange
      var authenticationProperties = new AuthenticationProperties()
      {
         ExpiresUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(10))
      };
      authenticationProperties.StoreTokens([new AuthenticationToken()
         { Name = OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken, Value = "secret" }]);
      var authresult = AuthenticateResult.Success(new(new(), authenticationProperties, ""));
      var httpcontext = new Mock<HttpContext>();
      var authService = new Mock<IAuthenticationService>();
      httpcontext.Setup(x=>x.RequestServices.GetService(typeof(IAuthenticationService))).Returns(authService);
      authService.Setup(x=>x.AuthenticateAsync(httpcontext.Object, It.IsAny<string>())).ReturnsAsync(authresult);
      var accessor = Mock.Of<IHttpContextAccessor>(x => x.HttpContext == httpcontext.Object);
      var openIddict = Mock.Of<OpenIddictClientService>();


      // Act
      var sut = new SuT(accessor, openIddict);
      var s = await sut.Test();

      // Assert
      Assert.Equal("secret", s);
   }

   [Fact]
   public async Task WhenLoggedInWithCloseToExpireToken_ShouldRefreshToken()
   {
      // Arrange
      var authenticationProperties = new AuthenticationProperties()
      {
         ExpiresUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1))
      };
      authenticationProperties.StoreTokens([new AuthenticationToken()
         { Name = OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken, Value = "secret" }]);
      var authresult = AuthenticateResult.Success(new(new(), authenticationProperties, ""));
      var httpcontext = new Mock<HttpContext>();
      var authService = new Mock<IAuthenticationService>();
      httpcontext.Setup(x=>x.RequestServices.GetService(typeof(IAuthenticationService))).Returns(authService);
      authService.Setup(x=>x.AuthenticateAsync(httpcontext.Object, It.IsAny<string>())).ReturnsAsync(authresult);
      var accessor = Mock.Of<IHttpContextAccessor>(x => x.HttpContext == httpcontext.Object);
      var openIddict = new Mock<OpenIddictClientService>();
      openIddict.Setup(x =>
            x.AuthenticateWithRefreshTokenAsync(It.IsAny<OpenIddictClientModels.RefreshTokenAuthenticationRequest>()))
         .ReturnsAsync(new OpenIddictClientModels.RefreshTokenAuthenticationResult()
         {
            AccessToken = "newtoken",
            IdentityToken = "null",
            IdentityTokenPrincipal = null,
            Principal = null!,
            Properties = null!,
            RefreshToken = "null",
            TokenResponse = new OpenIddictResponse(),
            UserinfoToken = null,
            UserinfoTokenPrincipal = null
         });

      // Act
      var sut = new SuT(accessor, openIddict.Object);
      var s = await sut.Test();

      // Assert

      Assert.Equal("newtoken", s);

   }
   private class SuT
      (IHttpContextAccessor httpContextAccessor, OpenIddictClientService openIddict) :
         RefreshTokenAuthorizationCodeHandler(httpContextAccessor, openIddict)
   {
      public ValueTask<string?> Test(CancellationToken cancellationToken = default) => GetAccessTokenAsync(cancellationToken);
   }
   */
}