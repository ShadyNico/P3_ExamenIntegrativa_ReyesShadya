using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using AirportApp.Settings;

namespace AirportApp.Services;

public class GmailEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;

    public GmailEmailSender(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(
            _settings.SenderName,
            _settings.SenderEmail));

        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = htmlMessage
        }.ToMessageBody();

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _settings.SmtpServer,
            _settings.SmtpPort,
            SecureSocketOptions.StartTls);

        await client.AuthenticateAsync(
            _settings.SenderEmail,
            _settings.Password);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}