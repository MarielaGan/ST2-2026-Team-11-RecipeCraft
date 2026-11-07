using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace RecipeCraft.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            var smtpServer = _configuration["Smtp:Server"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
            var senderEmail = _configuration["Smtp:SenderEmail"] ?? "";
            var senderPassword = _configuration["Smtp:Password"] ?? "";

            if (string.IsNullOrWhiteSpace(senderPassword) || string.IsNullOrWhiteSpace(senderEmail))
            {
                System.Diagnostics.Debug.WriteLine("[EMAIL ERROR] Липсва SenderEmail или Password в appsettings.json!");
                return;
            }

            try
            {
                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "RecipeCraft"),
                    Subject = subject,
                    Body = message,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                System.Diagnostics.Debug.WriteLine($"[EMAIL SUCCESS] Успешно изпратен имейл до: {email}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] Грешка при изпращане: {ex.Message}");
            }
        }
    }
}