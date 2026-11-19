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
## Registering schedules at startup

`Schedule` always inserts a new schedule, so calling it at every startup creates duplicates. Use `EnsureSchedule` with a tag instead. It creates the schedule the first time, and afterwards updates the payload. It only recalculates the next occurrence if the expression or timezone changed, and removes any duplicates with the same tag.

```csharp
await scheduleService.EnsureSchedule(new CleanupExpiredSessions(), "0 0 3 * * *", "Europe/London", tag: "cleanup-sessions");
await dbContext.SaveChangesAsync();
```

Occurrences missed while the app was not running are not caught up: the schedule fires once and moves to its next occurrence.
