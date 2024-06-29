Make sure to check this table before updating the nuget package in your solution, you may be required to add an `dotnet ef migration`.

| Version | Description                                | Migration Required |
|---------|--------------------------------------------|--------------------|
| 8.0.6   | Added ConsumerAttempts override attribute  | No                 |
| 8.0.5   | Bundle debug symbols with nuget package    | No                 |
| 8.0.4   | Bundle XML docs with nuget package         | No                 |
| 8.0.3   | Optimised Sql                              | No                 |
| 8.0.2   | Added distributed trace_id and span_id     | No                 |
| 8.0.1   | Added OpenTelemetry support                | Yes                |
| 8.0.0   | Use Producer & Schedule service interfaces | No                 |
| 1.0.9   | Initial                                    | Yes                |

***If you're not using ef migrations check out the sql to configure your database [here](https://github.com/Timmoth/AsyncMonolith/tree/main/Schemas)***
