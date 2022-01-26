# BlendoBot.Frontend
## A Discord bot framework for .NET 6.0
![GitHub Workflow Status](https://img.shields.io/github/workflow/status/BlendoBot/BlendoBot.Frontend/Tests)
![Nuget](https://img.shields.io/nuget/v/BlendoBot.Frontend)

BlendoBot is a Discord bot framework intended for easy implementation of new functionalities and commands. It currently uses [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus) to operate with Discord, but it also has its own interfaces and common code functionality to allow front-ends to easily add and remove functionality from the bot.

### What do I do with this?
`BlendoBot.Frontend` is published to the BlendoBot NuGet. To add the package to your project, add the NuGet feed `https://nuget.pkg.github.com/BlendoBot/index.json` to your project, and use the latest stable version of `BlendoBot.Frontend`. This package implements the infrastructure layer described by [BlendoBot.Core](https://github.com/BlendoBot/BlendoBot.Core) into an object called `BlendoBot`, which can be created and run with its `Start()` method. An example of running this can be seen in [BlendoBot.Live](https://github.com/BlendoBot/BlendoBot.Live).

### Services
`BlendoBot.Frontend` implements several services.
- `Config` - Implements `IConfig` using an in-memory INI file read with [Salaros.ConfigParser](https://github.com/salaros/config-parser). `BlendoBot` is constructed with a path to a config file, and if it does not exist, this will make a new template config file that needs to be populated with details. An example config file is below. Importantly, the `Token` must be supplied with your own Discord bot token, which can be created via [Discord's Developer Portal](https://discord.com/developers/applications). `ActivityType` must be a valid type as described in [Discord's Documentation](https://discord.com/developers/docs/game-sdk/activities#data-models-activitytype-enum).
```cfg
[BlendoBot]
Name=BlendoBot
Version=0.0.1.0
Description=Smelling the roses
Author=Biendeo
ActivityType=Watching
ActivityName=clouds
Token=sometoken
```
- `Logger` - Implements `ILogger` as a `tee` style logger which writes logs to `log/LOGFILE.log` where `LOGFILE` is the start date time of the bot.
- `ModuleManager` - Implements `IModuleManager` by storing guild modules in memory, and persisting information between sessions using an SQLite database. Modules are loaded through reflection, so all `IModule` implementing types within the application domain are loaded. It properly instantiates modules in an order that satisfies their dependency tree, and supports renaming commands within guilds.
- `FilePathProvider` - Implements `IFilePathProvider` by pointing to directories located within `data/`.
- `DiscordInteractor` - Implements `IDiscordInteractor` by forwarding and abstracting logic away from DSharpPlus related events.
- `AdminRepository` - Implements `IAdminRepository` by storing information in an SQLite database.

### Admin
`BlendoBot.Frontend` also features an in-built `Admin` module, which allows users to run the following below commands.
- `?help` - Responds to a user with all the commands currently registered, or the usage of a specific command.
- `?about` - Responds to a user with details about the current bot instance, or information about a command and the module associated when specified.
- `?admin` - Allows admins of a Discord guild to manage enabled modules for their guild, as well as promote others to be able to access admin features of BlendoBot.
