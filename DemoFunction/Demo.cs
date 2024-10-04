using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using DemoFunction.Models;
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
            PersonModel person = JsonSerializer.Deserialize<PersonModel>(jsonString);

            // Complete the message
            await messageActions.CompleteMessageAsync(message);
        }
    }
}
