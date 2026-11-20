- **Frequency**: Scheduled messages are produced each time their cron expression occurs, in their time zone.
- **Transactional Persistence**: Schedule messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring your domain changes and the messages they produce are persisted transactionally.
- **Processing**: When a schedule is due, a message is produced for each consumer of its payload and the schedule moves to its next occurrence. Each due schedule is handled on its own, so one that fails doesn't affect the others.

To produce your message you'll need to inject a `IScheduleService`

```csharp
 // Publish 'CacheRefreshScheduled' every Monday at 12pm (UTC) with a tag that can be used to modify / delete related scheduled messages.
 _scheduleService.Schedule(new CacheRefreshScheduled
   {
       Id = id
   }, "0 0 12 * * MON", "UTC", "id:{id}");
 await _dbContext.SaveChangesAsync(cancellationToken);
```
## Cron expressions

Expressions can have 5 fields (`minute hour day month weekday`, for example `0 9 * * *` for 09:00 daily) or 6 fields with seconds first (`30 0 9 * * *` for 09:00:30). They are evaluated in the time zone you pass, using an IANA id such as `Europe/London` or `UTC`.

## Registering schedules at startup

`Schedule` always inserts a new schedule, so calling it at every startup creates duplicates. Use `EnsureSchedule` with a tag instead. It creates the schedule the first time, and afterwards updates the payload. It only recalculates the next occurrence if the expression or timezone changed, and removes any duplicates with the same tag.

```csharp
await scheduleService.EnsureSchedule(new CleanupExpiredSessions(), "0 0 3 * * *", "Europe/London", tag: "cleanup-sessions");
await dbContext.SaveChangesAsync();
```

Occurrences missed while the app was not running are not caught up: the schedule fires once and moves to its next occurrence.
