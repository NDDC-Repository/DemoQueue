using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using DemoFunction.Models;
using FluentEmail.Razor;
using FluentEmail.Smtp;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DemoFunction
{
    public class Demo
    {
        private readonly ILogger<Demo> _logger;

        public Demo(ILogger<Demo> logger)
        {
            _logger = logger;
        }

        [Function(nameof(Demo))]
        public async Task Run(
            [ServiceBusTrigger("demoqueue", Connection = "ServiceBusConnectionString")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
           var jsonString = Encoding.UTF8.GetString(message.Body);
            MyEmailModel email = JsonSerializer.Deserialize<MyEmailModel>(jsonString);

            //send email
            SendEmail(email.Email, email.Subject, email.HtmlMessage);

            // Complete the message
            await messageActions.CompleteMessageAsync(message);

            
        }

        private async Task SendEmail(string email, string subject, string htmlMessage)
        {
            var credentials = new NetworkCredential()
            {
                UserName = "emailappsmtp.76f497bcd011bb11",  // replace with valid value
                Password = "XFKuxEvmPZ4n"  // replace with valid value
            };
            var sender = new SmtpSender(() => new SmtpClient("smtp.zeptomail.com")
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Port = 587,
                Credentials = credentials,

                //DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                //PickupDirectoryLocation = @"C:\Demos"
            });

            StringBuilder template = new();
            template.AppendLine(htmlMessage);

            FluentEmail.Core.Email.DefaultSender = sender;
            FluentEmail.Core.Email.DefaultRenderer = new RazorRenderer();

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.WriteLine(template.ToString());
            writer.Flush();
            stream.Position = 0;

            var contentType = new System.Net.Mime.ContentType(System.Net.Mime.MediaTypeNames.Application.Pdf);
            var reportAttachment = new Attachment(stream, contentType);
            reportAttachment.ContentDisposition.FileName = "yourFileName.pdf";

            var myEmail = FluentEmail.Core.Email
                .From("no-reply@nddc.gov.ng", "NDDC Notification")
                .Subject(subject)
                .To(email)
                .UsingTemplate(template.ToString(), new { });



            var response = await myEmail.SendAsync();
        }
    }
}
