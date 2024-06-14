Make sure to check this table before updating the nuget package in your solution, you may be required to add an `dotnet ef migration`.

| Version | Description                                            | Migration Required |
|---------|--------------------------------------------------------|--------------------|
| 8.0.0   | Use Producer & Schedule service interfaces             | No                 |
| 1.0.9.1 | Added consumer timeout                                 | No                 |
| 1.0.9   | Split out Ef, PostgreSql, MySql into seperate packages | Yes                |
| 1.0.8   | Added scheduled message batching                       | No                 |
| 1.0.7   | Added consumer message batching                        | No                 |
| 1.0.6   | Added concurrent processors                            | No                 |
| 1.0.5   | Added OpenTelemetry support                            | No                 |
| 1.0.4   | Added poisoned message table                           | Yes                |
| 1.0.3   | Added mysql support                                    | Yes                |
| 1.0.2   | Scheduled messages use Chron expressions               | Yes                |
| 1.0.1   | Added Configurable settings                            | No                 |
| 1.0.0   | Initial                                                | Yes                |
