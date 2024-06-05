using AsyncMonolith.Consumers;

namespace Demo.Spam;

public class SpamMessage : IConsumerPayload
{
    public bool Last { get; set; }
}