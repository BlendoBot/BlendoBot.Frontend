using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Module;
using BlendoBot.Core.Utility;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.AdminModule;

internal class HelpCommand : ICommand {
	public HelpCommand(Admin module) {
		this.module = module;
	}

	private readonly Admin module;
	public IModule Module => module;

	public string Guid => "admin.help";
	public string DesiredTerm => "help";

	public string Description => "Posts what commands this bot can do, and additional help on how to use a command.";

	public Dictionary<string, string> Usage => new() {
		{ string.Empty, "Lists all commands in the guild." },
		{ "[command]", "Lists the help on a specific command. You're already using this case!" }
	};
			
	public async Task OnMessage(MessageCreateEventArgs e, string[] tokenizedMessage) {
		StringBuilder sb = new();
		string commandPrefix = module.ModuleManager.GetCommandPrefixForGuild(module.GuildId);
		if (tokenizedMessage.Length == 0) {
			sb.AppendLine($"Use {$"{module.ModuleManager.GetCommandTermWithPrefix(this)} [command]".Code()} for specific help.");
			sb.AppendLine("List of available commands:");
			foreach (KeyValuePair<string, ICommand> command in module.ModuleManager.GetCommands(module.GuildId)) {
				sb.AppendLine($"{commandPrefix}{command.Key}".Code());
			}
			DiscordEmbedBuilder embedBuilder = new();
			embedBuilder.Title = $"Use {$"{module.ModuleManager.GetCommandTermWithPrefix(this)} [command]".Code()} for specific help.";
			embedBuilder.AddField("Available commands", string.Join(' ', module.ModuleManager.GetCommands(module.GuildId).Keys.OrderBy(k => k).Select(k => $"{commandPrefix}{k}".Code())));
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Embed = embedBuilder.Build(),
				Channel = e.Channel,
				Tag = "HelpGeneric"
			});
		} else {
			string specifiedCommand = tokenizedMessage[0].ToLower();
			ICommand command = module.ModuleManager.GetCommandByTerm(module.GuildId, specifiedCommand);
			if (command == null && specifiedCommand.StartsWith(commandPrefix)) {
				command = module.ModuleManager.GetCommandByTerm(module.GuildId, specifiedCommand[commandPrefix.Length..]);
			}
			if (command == null) {
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"No command called {specifiedCommand.Code()}",
					Channel = e.Channel,
					Tag = "HelpErrorInvalidCommand"
				});
			} else {
				string termWithPrefix = module.ModuleManager.GetCommandTermWithPrefix(command);
				DiscordEmbedBuilder embedBuilder = new();
				embedBuilder.Title = $"Help for {termWithPrefix.Code()}";
				foreach (KeyValuePair<string, string> usagePair in command.Usage) {
					string key = usagePair.Key;
					if (key == string.Empty) {
						key = termWithPrefix.Code();
					} else if (key[0] >= 'A' && key[0] <= 'Z') {
						// Key is a name.
					} else {
						key = $"{termWithPrefix} {key}".Code();
					}
					embedBuilder.AddField(key, usagePair.Value);
				}
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Embed = embedBuilder.Build(),
					Channel = e.Channel,
					Tag = "HelpSpecific"
				});
			}
		}
	}
}
