using System.Security.Claims;

namespace Openiddict.Contrib.Server;

public interface IDestinationManager
{
    IEnumerable<string> GetDestinations(Claim claim);
}