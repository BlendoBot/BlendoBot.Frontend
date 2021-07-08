using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Interfaces;
using BlendoBot.Core.Utility;
using BlendoBot.Frontend.Services;
using DSharpPlus.EventArgs;
using System;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Commands {
	/// <summary>
	/// The about command, which simply prints out the <see cref="BaseCommand.Description"/> property of a
	/// <see cref="BaseCommand"/>, or on its own details about the bot.
	/// </summary>
	[Command(Guid = "blendobot.frontend.commands.about", Name = "About", Author = "Biendeo", DefaultTerm = "about")]
	public class About : BaseCommand {
		public About(ulong guildId, BlendoBot blendobot) : base(guildId, blendobot) {
			this.blendobot = blendobot;
			discordInteractor = blendobot.GetService<IDiscordInteractor>();
			commandManager = blendobot.GetService<ICommandManager>();
			config = blendobot.Config;
		}

		public override string Description => "Posts information about this version of the bot, or of any loaded module. You probably already know how to use this command by now.";
		public override string Usage => $"Use {Term.Code()} to see the information about the bot.\nUse {$"{Term} [command]".Code()} to see information about another command.";

		private readonly BlendoBot blendobot;
		private readonly IDiscordInteractor discordInteractor;
		private readonly ICommandManager commandManager;
		private readonly Config config;

		public override Task<bool> Startup() {
			return Task.FromResult(true);
		}

		public override async Task OnMessage(MessageCreateEventArgs e) {
			var sb = new StringBuilder();

			if (e.Message.Content.Length == commandManager.GetCommandTerm(this, this).Length) {
				// No arguments behaviour.
				sb.AppendLine($"{config.Name} {config.Version} ({config.Description}) by {config.Author}\nBeen running for {(DateTime.Now - blendobot.StartTime).Days} days, {(DateTime.Now - blendobot.StartTime).Hours} hours, {(DateTime.Now - blendobot.StartTime).Minutes} minutes, and {(DateTime.Now - blendobot.StartTime).Seconds} seconds.");
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = sb.ToString(),
					Channel = e.Channel,
					LogMessage = "About"
				});
			} else {
				// Specified with one argument.
				string specifiedCommand = e.Message.Content[(commandManager.GetCommandTerm(this, this).Length + 1)..];
				var command = commandManager.GetCommandByTerm(this, GuildId, specifiedCommand);
				if (command == null && specifiedCommand.StartsWith(commandManager.GetCommandPrefix(this, GuildId))) {
					command = commandManager.GetCommandByTerm(this, GuildId, specifiedCommand[(commandManager.GetCommandPrefix(this, GuildId).Length + 1)..]);
				}
				if (command == null) {
					await discordInteractor.SendMessage(this, new SendMessageEventArgs {
						Message = $"No command called {specifiedCommand.Code()}",
						Channel = e.Channel,
						LogMessage = "AboutErrorInvalidCommand"
					});
				} else {
					sb.AppendLine($"{command.Name.Bold()} ({command.Version?.Italics()}) by {command.Author?.Italics()}");
					sb.AppendLine(command.Description);
					await discordInteractor.SendMessage(this, new SendMessageEventArgs {
						Message = sb.ToString(),
						Channel = e.Channel,
						LogMessage = "AboutSpecific"
					});
				}
			}
		}
	}
}
