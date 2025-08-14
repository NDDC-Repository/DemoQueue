using Azure.Messaging.ServiceBus;
using DemoFunction.Models;
using FluentEmail.Core;
using FluentEmail.Razor;
using FluentEmail.Smtp;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DemoFunction
{
	

	public class Demo
	{
		private readonly ILogger<Demo> _logger;

		public Demo(ILogger<Demo> logger) => _logger = logger;

		[Function(nameof(Demo))]
		public async Task Run(
			[ServiceBusTrigger("demoqueue", Connection = "ServiceBusConnectionString")]
		ServiceBusReceivedMessage message,
			ServiceBusMessageActions messageActions)
		{
			try
			{
				var jsonString = Encoding.UTF8.GetString(message.Body);
				var email = JsonSerializer.Deserialize<MyEmailModel>(jsonString);

				if (email is null || string.IsNullOrWhiteSpace(email.Email))
					throw new InvalidOperationException("Invalid email payload.");

				await SendEmailAsync(email.Email, email.Subject, email.HtmlMessage);

				await messageActions.CompleteMessageAsync(message);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Email send failed. Moving message to DLQ.");
				// dead-letter so you can inspect later; or use Abandon for retry logic
				await messageActions.DeadLetterMessageAsync(
	message,
	new Dictionary<string, object>
	{
		{ "DeadLetterReason", "EmailSendFailed" },
		{ "DeadLetterErrorDescription", ex.Message }
	});
			}
		}

		private async Task SendEmailAsync(string to, string subject, string htmlBody)
		{
			// Read secure settings from environment (App Settings)
			string host = Environment.GetEnvironmentVariable("ZEPTO_SMTP_HOST") ?? "smtp.zeptomail.com";
			int port = int.TryParse(Environment.GetEnvironmentVariable("ZEPTO_SMTP_PORT"), out var p) ? p : 587;
			string user = Environment.GetEnvironmentVariable("ZEPTO_SMTP_USER") ?? "emailapikey";
			string token = Environment.GetEnvironmentVariable("ZEPTO_SMTP_TOKEN"); // <-- secure token
			string from = Environment.GetEnvironmentVariable("MAIL_FROM") ?? "no-reply@nddc.gov.ng";

			if (string.IsNullOrWhiteSpace(token))
				throw new InvalidOperationException("SMTP token not configured.");

			var credentials = new NetworkCredential(user, token);

			var sender = new FluentEmail.Smtp.SmtpSender(() =>
				new SmtpClient(host)
				{
					EnableSsl = true,            // STARTTLS on 587
					DeliveryMethod = SmtpDeliveryMethod.Network,
					Port = port,
					Credentials = credentials
				});

			Email.DefaultSender = sender;
			Email.DefaultRenderer = new RazorRenderer();

			var email = Email
				.From(from, "NDDC Notification")
				.To(to)
				.Subject(subject ?? string.Empty)
				.Body(htmlBody ?? string.Empty, isHtml: true);

			var result = await email.SendAsync();
			if (!result.Successful)
				throw new InvalidOperationException("FluentEmail failed: " + string.Join("; ", result.ErrorMessages));
		}
	}
}
