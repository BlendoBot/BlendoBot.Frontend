using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Messages;
using BlendoBot.Core.Reactions;
using BlendoBot.Core.Services;
using BlendoBot.Core.Utility;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace BlendoBot.Frontend.Services;

internal class DiscordInteractor : IDiscordInteractor {
	private readonly Config config;
	private readonly Logger logger;
	private readonly ModuleManager moduleManager;

	private DiscordClient discordClient;
	private readonly Timer heartbeatCheck = new(120000.0);

	public DiscordInteractor(ModuleManager moduleManager, Config config, Logger logger) {
		this.moduleManager = moduleManager;
		this.config = config;
		this.logger = logger;

		heartbeatCheck.Elapsed += HeartbeatCheck_Elapsed;
		heartbeatCheck.AutoReset = true;

		SetupDiscordClient();
	}

	private void SetupDiscordClient() {
		//! This is very unsafe because other modules can attempt to read the bot API token, and worse, try and
		//! change it.
		discordClient = new DiscordClient(new DiscordConfiguration {
			Token = config.ReadConfig(this, "BlendoBot", "Token"),
			TokenType = TokenType.Bot
		});

		discordClient.Ready += DiscordReady;
		discordClient.MessageCreated += DiscordMessageCreated;
		discordClient.MessageReactionAdded += DiscordReactionAdded;
		discordClient.GuildCreated += DiscordGuildCreated;
		discordClient.GuildAvailable += DiscordGuildAvailable;

		//? These are for debugging in the short-term.
		discordClient.ClientErrored += DiscordClientErrored;
		discordClient.SocketClosed += DiscordSocketClosed;
		discordClient.SocketErrored += DiscordSocketErrored;

		discordClient.Heartbeated += DiscordHeartbeated;
	}

	internal async Task ConnectAsync() {
		await discordClient.ConnectAsync();
		heartbeatCheck.Start();
	}

	private void HeartbeatCheck_Elapsed(object sender, ElapsedEventArgs e) {
		logger.Log(this, new LogEventArgs {
			Type = LogType.Error,
			Message = $"Heartbeat didn't occur for 120 seconds, re-connecting..."
		});
		discordClient.Dispose();
		SetupDiscordClient();
		ConnectAsync().RunSynchronously();
	}

	private async Task DiscordReady(DiscordClient sender, ReadyEventArgs e) {
		if (config.ActivityType.HasValue) {
			await discordClient.UpdateStatusAsync(new DiscordActivity(config.ActivityName, config.ActivityType.Value), UserStatus.Online, DateTime.Now);
		}
		logger.Log(this, new LogEventArgs {
			Type = LogType.Log,
			Message = $"{config.Name} ({config.Version}) is connected to Discord!"
		});
	}

	private async Task DiscordMessageCreated(DiscordClient sender, MessageCreateEventArgs e) {
		// The rule is: don't react to my own messages, and commands need to be triggered with a
		// ? character.
		if (!e.Author.IsCurrent && !e.Author.IsBot) {
			string guildPrefix = moduleManager.GetCommandPrefixForGuild(e.Guild.Id);
			string commandTerm = e.Message.Content.Split(' ')[0].ToLower();
			if (commandTerm.StartsWith(guildPrefix)) {
				commandTerm = commandTerm[guildPrefix.Length..];
				ICommand command = moduleManager.GetCommandByTerm(e.Guild.Id, commandTerm);
				if (command != null) {
					try {
						await command.OnMessage(e, e.Message.Content.Split(' ')[1..]);
					} catch (Exception exc) {
						await Send(this, new SendEventArgs {
							Exception = exc,
							Channel = e.Channel,
							Tag = "GenericExceptionNotCaught"
						});
					}
				} else {
					if (moduleManager.IsUnknownCommandMessageEnabled(e.Guild.Id)) {
						await Send(this, new SendEventArgs {
							Message = $"I didn't know what you meant by that, {e.Author.Username}. Use {moduleManager.GetHelpTerm(e.Guild.Id).Code()} to see what I can do!",
							Channel = e.Channel,
							Tag = "UnknownMessage"
						});
					}
				}
			}

			foreach (IMessageListener messageListener in moduleManager.GetMessageListeners(e.Guild.Id)) {
				try {
					await messageListener.OnMessage(e);
				} catch (Exception exc) {
					logger.Log(this, new LogEventArgs {
						Type = LogType.Error,
						Message = $"Message listener {messageListener.GetType()} in guild {e.Guild.Name} ({e.Guild.Id}) threw exception {exc}"
					});
				}
			}
		}
	}

	private async Task DiscordReactionAdded(DiscordClient sender, MessageReactionAddEventArgs e) {
		if (!e.User.IsCurrent && !e.User.IsBot) {
			List<IReactionListener> reactionListeners = moduleManager.GetReactionListeners(e.Guild.Id, e.Message.Id);
			foreach (IReactionListener listener in reactionListeners) {
				try {
					await listener.OnReactionAdd(e);
				} catch (Exception exc) {
					await Send(this, new SendEventArgs {
						Exception = exc,
						Channel = e.Channel,
						Tag = "GenericExceptionNotCaught"
					});
				}
			}
		}
	}

	private Task DiscordGuildCreated(DiscordClient sender, GuildCreateEventArgs e) {
		logger.Log(this, new LogEventArgs {
			Type = LogType.Log,
			Message = $"Guild created: {e.Guild.Name} ({e.Guild.Id})"
		});

		return Task.CompletedTask;
	}

	private async Task<bool> DiscordGuildAvailable(DiscordClient sender, GuildCreateEventArgs e) {
		logger.Log(this, new LogEventArgs {
			Type = LogType.Log,
			Message = $"Guild available: {e.Guild.Name} ({e.Guild.Id})"
		});

		return await moduleManager.InstantiateModulesForGuild(e.Guild.Id);
	}

	private Task DiscordClientErrored(DiscordClient sender, ClientErrorEventArgs e) {
		logger.Log(this, new LogEventArgs {
			Type = LogType.Error,
			Message = $"ClientErrored triggered: {e.Exception}"
		});

		return Task.CompletedTask;
	}

	private Task DiscordSocketClosed(DiscordClient sender, SocketCloseEventArgs e) {
		logger.Log(this, new LogEventArgs {
			Type = LogType.Error,
			Message = $"SocketClosed triggered: {e.CloseCode} - {e.CloseMessage}"
		});

		return Task.CompletedTask;
	}
	private async Task DiscordSocketErrored(DiscordClient sender, SocketErrorEventArgs e) {
		logger.Log(this, new LogEventArgs {
			Type = LogType.Error,
			Message = $"SocketErrored triggered: {e.Exception}"
		});

		//HACK: This should try and reconnect should something wrong happen.
		await discordClient.ReconnectAsync();
	}

	private Task DiscordHeartbeated(DiscordClient sender, HeartbeatEventArgs e) {
		logger.Log(this, new LogEventArgs {
			Type = LogType.Log,
			Message = $"Heartbeat triggered: handled = {e.Handled}, ping = {e.Ping}, timestamp = {e.Timestamp}"
		});
		heartbeatCheck?.Stop();
		heartbeatCheck?.Start();

		return Task.CompletedTask;
	}

	public async Task<DiscordChannel> GetChannel(object o, ulong channelId) {
		return await discordClient.GetChannelAsync(channelId);
	}

	public async Task<DiscordUser> GetUser(object o, ulong userId) {
		return await discordClient.GetUserAsync(userId);
	}

	public Task<bool> IsUserAdmin(object o, DiscordGuild guild, DiscordChannel channel, DiscordUser user) {
		return Task.FromResult(channel.Users.FirstOrDefault(u => u.Id == user.Id).PermissionsIn(channel).HasFlag(Permissions.Administrator));
	}
	public async Task<DiscordMessage> Send(object o, SendEventArgs e) {
		StringBuilder messageContentBuilder = new();
		logger.Log(o, new LogEventArgs {
			Type = LogType.Log,
			Message = $"Sending message {e.Tag} to channel #{e.Channel.Name} ({e.Channel.Guild.Name})"
		});
		DiscordMessageBuilder messageBuilder = new();
		if (!string.IsNullOrEmpty(e.Message)) {
			messageContentBuilder.AppendLine(e.Message);
		}
		FileStream file = null;
		if (!string.IsNullOrEmpty(e.FilePath)) {
			file = File.Open(e.FilePath, FileMode.Open);
			messageBuilder.WithFile(file);
		}
		if (e.Embed != null && e.Exception == null) {
			messageBuilder.WithEmbed(e.Embed);
		}
		if (e.Exception != null) {
			DiscordEmbedBuilder embedBuilder = new() {
				Title = "An exception was thrown",
				Description = e.Exception.ToString()[..Math.Min(e.Exception.ToString().Length, 2000)].CodeBlock()
			};
			messageBuilder.WithEmbed(embedBuilder.Build());
		}
		// TODO: Something to help reconstruct a message if it relied on formatting tags near its limit.
		messageBuilder.WithContent(messageContentBuilder.ToString()[..Math.Min(messageContentBuilder.ToString().Length, 2000)]);
		DiscordMessage message = await messageBuilder.SendAsync(e.Channel);
		if (file != null) {
			file.Dispose();
		}
		return message;
	}

	public async Task<DiscordMessage> SendUnknownArgumentsMessage(object o, DiscordChannel channel, ICommand command) {
		return await Send(o, new SendEventArgs {
			Message = $"I couldn't determine what you wanted. Make sure your command is handled by {moduleManager.GetHelpTermForCommand(command).Code()}",
			Channel = channel,
			Tag = "UnknownArguments"
		});
	}

	internal async Task<DiscordUser> GetUserAsync(ulong userId) {
		return await discordClient.GetUserAsync(userId);
	}
}
