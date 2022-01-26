using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Module;
using BlendoBot.Core.Utility;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.AdminModule;

internal class AboutCommand : ICommand {
	public AboutCommand(Admin module) {
		this.module = module;
	}

	private readonly Admin module;
	public IModule Module => module;

	public string Guid => "admin.about";
	public string DesiredTerm => "about";

	public string Description => "Posts information about this version of the bot, or of any loaded module. You probably already know how to use this command by now.";

	public Dictionary<string, string> Usage => new() {
		{ string.Empty, "Describes information about the bot." },
		{ "[command]", "Describes information about a command." }
	};

	public async Task OnMessage(MessageCreateEventArgs e, string[] tokenizedMessage) {
		if (tokenizedMessage.Length == 0) {
			DiscordEmbedBuilder embedBuilder = new();
			embedBuilder.Title = $"{module.Config.Name} {module.Config.Version} by {module.Config.Author}";
			embedBuilder.AddField("MOTD", module.Config.Description);
			embedBuilder.AddField("Running for", $"{(DateTime.Now - module.Logger.StartTime).Days} days, {(DateTime.Now - module.Logger.StartTime).Hours} hours, {(DateTime.Now - module.Logger.StartTime).Minutes} minutes, and {(DateTime.Now - module.Logger.StartTime).Seconds} seconds");
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Embed = embedBuilder.Build(),
				Channel = e.Channel,
				Tag = "About"
			});
		} else {
			string commandPrefix = module.ModuleManager.GetCommandPrefixForGuild(module.GuildId);
			string specifiedCommand = tokenizedMessage[0].ToLower();
			ICommand command = module.ModuleManager.GetCommandByTerm(module.GuildId, specifiedCommand);
			if (command == null && specifiedCommand.StartsWith(commandPrefix)) {
				command = module.ModuleManager.GetCommandByTerm(module.GuildId, specifiedCommand[commandPrefix.Length..]);
			}
			if (command == null) {
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"No command called {specifiedCommand.Code()}",
					Channel = e.Channel,
					Tag = "AboutErrorInvalidCommand"
				});
			} else {
				ModuleAttribute moduleAttribute = command.Module.GetType().GetCustomAttribute<ModuleAttribute>();
				DiscordEmbedBuilder embedBuilder = new();
				embedBuilder.Title = $"About {module.ModuleManager.GetCommandTermWithPrefix(command).Code()}";
				embedBuilder.AddField("Module name", moduleAttribute.Name, true);
				embedBuilder.AddField("Module author", moduleAttribute.Author, true);
				embedBuilder.AddField("Module version", moduleAttribute.Version, true);
				embedBuilder.AddField("Module guid", moduleAttribute.Guid.Code(), false);
				embedBuilder.AddField("Module URL", moduleAttribute.Url, false);
				embedBuilder.AddField("Command guid", command.Guid.Code(), false);
				embedBuilder.AddField("Command description", command.Description);
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Embed = embedBuilder.Build(),
					Channel = e.Channel,
					Tag = "AboutSpecific"
				});
			}
		}
	}
}
