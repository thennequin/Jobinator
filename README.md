# Jobinator
Simple C# distributed job scheduler

## Requirement
Jobinator use:
* **ServiceStack.OrmLite** for database 
* **ServiceStack.Text** for object <=> JSON conversion

So you need a database provider and the right Nuget package for OrmLite.

## How use it?
You need to create a Jobinator server and a Jobinator agent
### Create Server
```C#
Jobinator.Core.Configuration oConfiguration = new Jobinator.Core.Configuration();
// Using Mono SQLite provider
oConfiguration.ConnectionUrl = "./Jobinator.sqlite";
oConfiguration.Provider = ServiceStack.OrmLite.SqliteDialect.Provider;
// Set server mode
oConfiguration.Mode = Core.Configuration.EMode.Server;
// or for server with embedded agent
oConfiguration.Mode = Core.Configuration.EMode.Both;

Core.JobManager.Init(oConfiguration);
```
### Create Agent
If you don't create server with agent, you need to create one
```C#
Jobinator.Core.Configuration oConfiguration = new Jobinator.Core.Configuration();
oConfiguration.MainServer = "localhost";
// Set agent mode
oConfiguration.Mode = Core.Configuration.EMode.Agent;

Core.JobManager.Init(oConfiguration);
```

### Create Jobs
```C#
Jobinator.Core.Job oJob = Jobinator.Core.JobManager.Current.Enqueue(() => Console.Write("Hello"));
Jobinator.Core.JobManager.Current.ContinueWith(() => Console.Write(" World"), oJob);
```

It's all you need
