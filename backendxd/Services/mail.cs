using MimeKit;
using MailKit.Net.Smtp;
using MimeKit;
using backendxd.Models;

namespace backendxd.Services
{
    public class mail
    {
        private readonly IConfiguration _config;

        public mail(IConfiguration config)
        {
            _config = config;
        }


        public async Task SendEmailAsync(string email, string code)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress("asdqwe", AppSettings.MailSender));

            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Verify Code";
            message.Body = new TextPart("plain") { Text = $"Code: {code}" };

            using var client = new SmtpClient();

            await client.ConnectAsync("smtp.mail.ru", 465, MailKit.Security.SecureSocketOptions.SslOnConnect);

            await client.AuthenticateAsync(AppSettings.MailSender, AppSettings.MailAppKey);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
   

    }
}
