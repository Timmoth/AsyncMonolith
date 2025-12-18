using AsyncMonolith.Consumers;

namespace Example.Api.Spam;

public class SpamMessage : IConsumerPayload
{
    public bool Last { get; set; }
}