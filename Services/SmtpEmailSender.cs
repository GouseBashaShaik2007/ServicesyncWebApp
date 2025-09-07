using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ServicesyncWebApp.Options;

namespace ServicesyncWebApp.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _opts;
        public SmtpEmailSender(IOptions<EmailOptions> opts) => _opts = opts.Value;

        public async Task SendAsync(string toEmail, string subject, string htmlBody, string? plainText = null, CancellationToken ct = default)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_opts.FromName, _opts.FromEmail));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            msg.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = string.IsNullOrWhiteSpace(plainText) ? System.Text.RegularExpressions.Regex.Replace(htmlBody, "<.*?>", string.Empty) : plainText
            };
            msg.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_opts.Host, _opts.Port, _opts.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);

            if (!string.IsNullOrWhiteSpace(_opts.UserName))
                await client.AuthenticateAsync(_opts.UserName, _opts.Password, ct);

            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
        }
    }
}
