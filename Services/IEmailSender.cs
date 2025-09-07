using System.Threading;
using System.Threading.Tasks;

namespace ServicesyncWebApp.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string htmlBody, string? plainText = null, CancellationToken ct = default);
    }
}
