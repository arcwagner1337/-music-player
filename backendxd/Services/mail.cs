using MimeKit;
using MailKit.Net.Smtp;
using MimeKit;

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
            message.From.Add(new MailboxAddress("asdqwe", "arcwagner666@mail.ru"));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Verify Code";
            message.Body = new TextPart("plain") { Text = $"Code: {code}" };

            using var client = new SmtpClient();
            //await client.ConnectAsync(_config["SENDER_HOST"], int.Parse(_config["SENDER_PORT"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
            await client.ConnectAsync("smtp.mail.ru", 465, MailKit.Security.SecureSocketOptions.SslOnConnect);
            //await client.AuthenticateAsync(_config["SENDER_ADDR"], _config["SENDER_PASS"]);
            await client.AuthenticateAsync("arcwagner666@mail.ru", "JxwO5fV0ljkZDSDjUiBR");
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        //JxwO5fV0ljkZDSDjUiBR

    }
}
