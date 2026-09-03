// Services/EmailService.cs
using MailKit.Net.Smtp;
using MimeKit;

namespace GameRealmAPI.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendPasswordResetAsync(string toEmail, string username, string token)
    {
        // غير الـ URL ده لـ URL الـ Vue.js بتاعك
        var resetUrl = $"https://yourgame.com/reset-password?token={token}";

        var body = $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; background: #0a0c14; color: #e8dfc8; padding: 40px; border-radius: 8px; border: 1px solid #1e2540;">
              <h1 style="color: #c9a227; font-size: 28px; margin-bottom: 8px;">⚔️ GameRealm</h1>
              <h2 style="color: #e8dfc8; font-size: 20px;">Password Reset Request</h2>
              <p>Hello <strong>{username}</strong>,</p>
              <p>We received a request to reset your password. Click the button below to set a new password:</p>
              <div style="text-align: center; margin: 32px 0;">
                <a href="{resetUrl}" 
                   style="background: linear-gradient(135deg, #c9a227, #a07c18); color: #0a0c14; padding: 14px 32px; border-radius: 6px; text-decoration: none; font-weight: bold; font-size: 16px;">
                  Reset My Password
                </a>
              </div>
              <p style="color: #8a8fa8; font-size: 14px;">This link expires in <strong>2 hours</strong>. If you didn't request this, ignore this email.</p>
              <hr style="border-color: #1e2540; margin: 24px 0;">
              <p style="color: #4a4f68; font-size: 12px;">© GameRealm. All rights reserved.</p>
            </div>
            """;

        await SendEmailAsync(toEmail, "Reset Your GameRealm Password", body);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_config["Email:FromName"], _config["Email:Username"]));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_config["Email:SmtpHost"], int.Parse(_config["Email:SmtpPort"]!), false);
        await client.AuthenticateAsync(_config["Email:Username"], _config["Email:Password"]);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
