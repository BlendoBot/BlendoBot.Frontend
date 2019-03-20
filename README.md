## BlendoBot
### A Discord bot + modular framework written in C# for .NET Core 2.1

BlendoBot is a Discord bot intended for fun uses. It has a modular design such that it can dynamically load in any DLL that uses a common framework. The project is split into two major outputs; the BlendoBotLib DLL, which exposes interfaces for modules, and the BlendoBot executable that connects to Discord and operates with the modules.

### Download and run BlendoBot

You will require the ability to compile and run .NET Core 2.1 programs. Please follow [the instructions for your operating system](https://dotnet.microsoft.com/download/dotnet-core/2.2) on how to setup .NET Core (current version is 2.2) for your commandline. Alternatively, Windows and Mac OS users may choose to open the `BlendoBot.sln` file in Visual Studio 2017 or greater and simply run the program from there.

If you can run `dotnet` from the commandline, simply:
- Clone the repository using:
```
git clone https://github.com/Biendeo/BlendoBot.git
```
- Change to the cloned directory with:
```
cd BlendoBot
```
- Create a file called `config.json` and set the fields (more detail in [#Config](###Config)).
- Finally, simply run:
```
dotnet run --project BlendoBot/ -c Release
```

BlendoBot will now begin running. If everything is successful, you should see a message such as:
```
[Log] (2019-03-20 04:19:22) | BlendoBot (0.0.13.0) is up and ready!
```
You will also see an individual message for each module available in the program such as:
```
[Log] (2019-03-20 04:19:21) | Successfully loaded internal module About (?about)
```

This should allow you to know whether any modules are currently in your program. By default, the solution should compile and automatically put all modules into the same folder as the BlendoBot executable.

### Config

In order to run BlendoBot, you will need to include a `config.json` file in your root folder (i.e. with `BlendoBot.sln`). The file should be structured as such:

```json

{
	"Public": {
		"Name": "BlendoBot",
		"Version": "0.0.1.0",
		"Description": "Smelling",
		"Author": "Biendeo",
		"ActivityType": "Watching",
		"ActivityName": "clouds"
	},
	"Private": {
		"Token": "sometoken",
	}
}
```

The fields are as follows:

- `Public` - A category of things that will appear publicly with your bot.
  - `Name` - Determines the name of the bot. This is not the name used on Discord, just the internal program name that displays in `?about`.
  - `Version` - A version number that can be specified, to help identify what version the bot is using. This appears in `?about`.
  - `Description` - A brief description to help describe the version. This appears in `?about`.
  - `Author` - An author for the bot itself. This appears in `?about`.
  - `ActivityType` - The activity that the bot will supposedly be doing. These are the few terms that can be used by a client to indicate that they are doing something on the side. Use whichever one is the most appropriate for your case. Valid terms are: `Playing`, `Streaming`, `ListeningTo`, and `Watching`.
  - `ActivityName` - The name of the activity that is used write after the type.
- `Private` - A category of things that should never be discussed with anyone but your bot.
  - `Token` - Your Discord bot token. This should never be disclosed or anyone can use anything to log in to Discord with your bot!

### Internal uses

BlendoBot comes with a few commands that are labeled as *internal*. These commands are simply baked into the BlendoBot executable and are available to all running instances of BlendoBot.

#### `?about`

The about command simply tells you information about the current BlendoBot instance and individual command modules. Using it without any arguments tells you about the running client (set in `config.json`), and using it with the name of a command afterwards (i.e. `?about help` or `?about ?help`) tells you information about that module.

#### `?help`

The help command tells you how a command can be used. Using it without any arguments tells you a list of available commands. Supply a name of a command afterwards (i.e. `?help about` or `?help ?about`) and it'll tell you the help information about that command.

#### `?admin`

*TODO: I think I'm cleaning this command, come back to it later.*

### Developing your own modules

The neat part of BlendoBot is that you do not need to modify the BlendoBot program itself to add a new command! Simply create your own module in the same way as the existing modules, and compile it to a DLL. Then, if the DLL is in the same folder as the BlendoBot executable, then when the bot launches, the modules will be loaded in!

*TODO: How do you do that?*

### Contributing to the current source code

*TODO*