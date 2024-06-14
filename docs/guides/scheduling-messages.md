- **Frequency**: Scheduled messages will be produced periodically by the `chron_expression` in the given `chron_timezone`
- **Transactional Persistence**: Schedule messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring your domain changes and the messages they produce are persisted transactionally.
- **Processing**: Schedule messages will be processed sequentially after they are made available by their chron job, at which point they will be turned into Consumer Messages and inserted into the `consumer_messages` table to be handled by their respective consumers.

To produce your message you'll need to inject a `IScheduleService`

```csharp
 // Publish 'CacheRefreshScheduled' every Monday at 12pm (UTC) with a tag that can be used to modify / delete related scheduled messages.
 _scheduleService.Schedule(new CacheRefreshScheduled
   {
       Id = id
   }, "0 0 12 * * MON", "UTC", "id:{id}");
 await _dbContext.SaveChangesAsync(cancellationToken);
```