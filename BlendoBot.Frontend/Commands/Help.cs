using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Interfaces;
using BlendoBot.Core.Utility;
using BlendoBot.Frontend;
using BlendoBot.Frontend.Services;
using DSharpPlus.EventArgs;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Commands {
	/// <summary>
	/// The help command, which simply prints out the <see cref="BaseCommand.Usage"/> property of a
	/// <see cref="BaseCommand"/>.
	/// </summary>
	[Command(Guid = "blendobot.frontend.commands.help", Name = "Help", Author = "Biendeo", DefaultTerm = "help")]
	public class Help : BaseCommand {
		public Help(ulong guildId, BlendoBot blendobot) : base(guildId, blendobot) {
			discordInteractor = blendobot.GetService<IDiscordInteractor>();
			commandManager = blendobot.CommandManager;
		}
		public override string Description => "Posts what commands this bot can do, and additional help on how to use a command.";
		public override string Usage => $"Use {commandManager.GetCommandTerm(this, this).Code()} to see a list of all commands on the server.\nUse {$"{commandManager.GetCommandTerm(this, this)} [command]".Code()} to see help on a specific command, but you probably already know how to do that!";

		private readonly IDiscordInteractor discordInteractor;
		private readonly CommandManager commandManager;

		public override Task<bool> Startup() {
			return Task.FromResult(true);
		}

		public override async Task OnMessage(MessageCreateEventArgs e) {
			// The help command definitely prints out a string. Which string will be determined by the arguments.
			var sb = new StringBuilder();
			if (e.Message.Content.Length == commandManager.GetCommandTerm(this, this).Length) {
				// This block runs if the ?help is run with no arguments (fortunately Discord trims whitespace).
				// All the commands are iterated through and their terms are printed out so the user knows which
				// commands are available.
				sb.AppendLine($"Use {$"{commandManager.GetCommandTerm(this, this)} [command]".Code()} for specific help.");
				sb.AppendLine("List of available commands:");
				foreach (var command in commandManager.GetCommands(this, GuildId)) {
					sb.AppendLine(commandManager.GetCommandTerm(this, command).Code());
				}
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = sb.ToString(),
					Channel = e.Channel,
					LogMessage = "HelpGeneric"
				});
			} else {
				// This block runs if the ?help is run with an argument. The relevant command is searched and its usage
				// is printed out, or an error message if that command doesn't exist.
				string specifiedCommand = e.Message.Content[(commandManager.GetCommandTerm(this, this).Length + 1)..];
				var command = commandManager.GetCommandByTerm(this, GuildId, specifiedCommand);
				if (command == null && specifiedCommand.StartsWith(commandManager.GetCommandPrefix(this, GuildId))) {
					command = commandManager.GetCommandByTerm(this, GuildId, specifiedCommand[(commandManager.GetCommandPrefix(this, GuildId).Length + 1)..]);
				}
				if (command == null) {
					await discordInteractor.SendMessage(this, new SendMessageEventArgs {
						Message = $"No command called {specifiedCommand.Code()}",
						Channel = e.Channel,
						LogMessage = "HelpErrorInvalidCommand"
					});
				} else {
					sb.AppendLine($"Help for {commandManager.GetCommandTerm(this, command).Code()}:");
					if (command.Usage != null && command.Usage.Length != 0) {
						sb.AppendLine(command.Usage);
					} else {
						sb.AppendLine("No help found for this command".Italics());
					}
					await discordInteractor.SendMessage(this, new SendMessageEventArgs {
						Message = sb.ToString(),
						Channel = e.Channel,
						LogMessage = "HelpSpecific"
					});
				}
			}
		}
	}
}
