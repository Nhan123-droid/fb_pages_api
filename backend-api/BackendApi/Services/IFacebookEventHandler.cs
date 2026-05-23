using Page_API.Models;

namespace Page_API.Services
{
    public interface IFacebookEventHandler
    {
        Task HandleAsync(BackendCommandMessage command, CancellationToken cancellationToken);
    }
}
