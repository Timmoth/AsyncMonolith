## Demo

- Hit `https://localhost:60046/api/spam?count=1000` to see how performant AsyncMonolith is on your system. With 10 message batches and single processor instance I usually process (trivial) messages at <10ms each.
- The demo is setup to run against a PostgreSql database, make sure you've got docker installed
