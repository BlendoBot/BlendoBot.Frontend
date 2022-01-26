using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Utility;
using DSharpPlus.EventArgs;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.AdminModule.Subcommands;

internal class CommandSubcommand {
	private readonly Admin module;
	private readonly AdminCommand command;

	public CommandSubcommand(Admin module, AdminCommand command) {
		this.module = module;
		this.command = command;
	}

	public async Task OnMessage(MessageCreateEventArgs e, string[] tokenizedMessage) {
		if (tokenizedMessage.Length > 0) {
			switch (tokenizedMessage[0].ToLower()) {
				case "rename":
					await OnMessageRename(e, tokenizedMessage[1..]);
					break;
				default:
					await module.DiscordInteractor.SendUnknownArgumentsMessage(this, e.Channel, command);
					break;
			}
		} else {
			await module.DiscordInteractor.SendUnknownArgumentsMessage(this, e.Channel, command);
		}
	}

	public async Task OnMessageRename(MessageCreateEventArgs e, string[] remainingSplitMessage) {
		if (remainingSplitMessage.Length != 2) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"You must specify a command, and the name that you'd like it renamed to!",
				Channel = e.Channel,
				Tag = "AdminCommandRenameTooFewArguments"
			});
			return;
		}

		string commandPrefix = module.ModuleManager.GetCommandPrefixForGuild(module.GuildId);
		string oldCommandName = remainingSplitMessage[0].ToLower();
		string newCommandName = remainingSplitMessage[1].ToLower();

		ICommand command = module.ModuleManager.GetCommandByTerm(module.GuildId, oldCommandName);
		if (command == null) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"There is no command called {$"{commandPrefix}{oldCommandName}".Code()}",
				Channel = e.Channel,
				Tag = "AdminCommandRenameNoCommandFound"
			});
			return;
		}
		
		if (module.ModuleManager.GetCommandByTerm(module.GuildId, newCommandName) == null) {
			module.ModuleManager.RenameCommand(command, newCommandName);
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"Command {$"{commandPrefix}{oldCommandName}".Code()} was successfully renamed to {$"{commandPrefix}{newCommandName}".Code()}",
				Channel = e.Channel,
				Tag = "AdminCommandRenameSuccess"
			});
		} else {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"Couldn't rename command {$"{commandPrefix}{oldCommandName}".Code()} because {$"{commandPrefix}{newCommandName}".Code()} already exists!",
				Channel = e.Channel,
				Tag = "AdminCommandRenameAlreadyExists"
			});
		}
	}
}
