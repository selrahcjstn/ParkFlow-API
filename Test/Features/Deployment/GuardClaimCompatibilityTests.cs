using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ParkFlow.API.Security;
using Xunit;

namespace Test.Features.Deployment;

public class GuardClaimCompatibilityTests
{
    [Theory]
    [InlineData("role","Guard","pwd",true,true)]
    [InlineData(ClaimTypes.Role,"Guard","pwd",true,true)]
    [InlineData(ClaimTypes.Role,"Guard","microsoft",true,false)]
    [InlineData(ClaimTypes.Role,"Admin","pwd",true,false)]
    [InlineData("role","Student","pwd",true,false)]
    [InlineData("role","Guard","pwd",false,false)]
    public async Task GuardPolicyAcceptsValidatedRoleMappingsWithoutRelaxingRoleOrPasswordChecks(string type,string role,string method,bool authenticated,bool allowed)
    {
        using var services=new ServiceCollection().AddLogging().AddAuthorization(AuthorizationPolicies.Configure).BuildServiceProvider();
        var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim(type,role),new Claim("parkflow_authentication_method",method)],authenticated?"test":null));
        Assert.Equal(allowed,(await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal,null,"GuardOnly")).Succeeded);
    }
}
