- **Backwards Compatibility**: When modifying consumer payload schemas, ensure changes are backwards compatible so that existing messages with the old schema can still be processed.
- **Schema Migration**:
  - If changes are not backwards compatible, make the changes in a copy of the `ConsumerPayload` (with a different class name) and update all consumers to operate on the new payload.
  - Once all messages with the old payload schema have been processed, you can safely delete the old payload schema and its associated consumers.

## Stored names

Messages are stored with the name of their payload type (`payload_type`) and consumer type (`consumer_type`). By default that is the class name, so **renaming a payload or consumer class breaks messages and schedules that are already stored**.

Give a type a stable name to decouple it from the class name:

```csharp
[ConsumerPayloadName("minicms.email.send")]
public class SendEmail : IConsumerPayload { ... }

[ConsumerName("minicms.email.sender")]
public class SendEmailConsumer : BaseConsumer<SendEmail> { ... }
```

To rename a class that already has stored messages, add the attribute with the old class name first.

Names must be unique across everything registered. Two payloads or consumers with the same stored name, such as `SendEmail` classes in two modules, fail at startup with an error naming both types.
