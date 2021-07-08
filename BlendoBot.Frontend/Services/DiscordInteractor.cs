using BlendoBot.Core.Entities;
using BlendoBot.Core.Interfaces;
using BlendoBot.Core.Utility;
using BlendoBot.Frontend.Commands;
using BlendoBot.Frontend.Database;
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

namespace BlendoBot.Frontend.Services {
	public class DiscordInteractor : IDiscordInteractor {
		private readonly CommandManager commandManager;
		private readonly Config config;
		private readonly Logger logger;
		private DiscordClient discordClient;
		private Timer HeartbeatCheck = new(120000.0);
		private int ClientRestarts = 0;

		public DiscordInteractor(CommandManager commandManager, Config config, Logger logger) {
			this.commandManager = commandManager;
			this.config = config;
			this.logger = logger;

			HeartbeatCheck.Elapsed += HeartbeatCheck_Elapsed;
			HeartbeatCheck.AutoReset = true;

			SetupDiscordClient();
		}

		public async Task ConnectAsync() {
			await discordClient.ConnectAsync();
			HeartbeatCheck.Start();
		}

		private void SetupDiscordClient() {
			//! This is very unsafe because other modules can attempt to read the bot API token, and worse, try and
			//! change it.
			discordClient = new DiscordClient(new DiscordConfiguration {
				Token = config.ReadConfig(this, "BlendoBot", "token"),
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

		private void HeartbeatCheck_Elapsed(object sender, ElapsedEventArgs e) {
			logger.Log(this, new LogEventArgs {
				Type = LogType.Error,
				Message = $"Heartbeat didn't occur for 120 seconds, re-connecting..."
			});
			discordClient.Dispose();
			SetupDiscordClient();
			ConnectAsync().RunSynchronously();
		}

		public async Task<DiscordChannel> GetChannel(object o, ulong channelId) {
			return await discordClient.GetChannelAsync(channelId);
		}

		public async Task<DiscordUser> GetUser(object o, ulong userId) {
			return await discordClient.GetUserAsync(userId);
		}

		public Task<bool> IsUserAdmin(object o, DiscordGuild guild, DiscordChannel channel, DiscordUser user) {
			using var dbContext = BlendoBotDbContext.Get();
			return Task.FromResult(channel.Users.FirstOrDefault(u => u.Id == user.Id).PermissionsIn(channel).HasFlag(Permissions.Administrator) || dbContext.AdminUsers.Contains(new AdminUser { GuildId = guild.Id, UserId = user.Id }));
		}

		public async Task<DiscordMessage> SendException(object sender, SendExceptionEventArgs e) {
			logger.Log(sender, new LogEventArgs {
				Type = LogType.Error,
				Message = $"{e.LogExceptionType}\n{e.Exception}"
			});
			string messageHeader = $"A {e.LogExceptionType} occurred. Alert the authorities!\n```\n";
			string messageFooter = "\n```";
			string exceptionString = e.Exception.ToString();
			if (exceptionString.Length + messageHeader.Length + messageFooter.Length > 2000) {
				int oldLength = exceptionString.Length;
				exceptionString = exceptionString.Substring(0, 2000 - messageHeader.Length - messageFooter.Length);
				logger.Log(sender, new LogEventArgs {
					Type = LogType.Warning,
					Message = $"Last message was {oldLength} characters long, truncated to {exceptionString.Length}"
				});
			}
			return await e.Channel.SendMessageAsync(messageHeader + exceptionString + messageFooter);
		}

		public async Task<DiscordMessage> SendFile(object sender, SendFileEventArgs e) {
			logger.Log(sender, new LogEventArgs {
				Type = LogType.Log,
				Message = $"Sending file {e.LogMessage} to channel #{e.Channel.Name} ({e.Channel.Guild.Name})"
			});
			using var file = File.Open(e.FilePath, FileMode.Open);
			return await new DiscordMessageBuilder().WithFile(file).SendAsync(e.Channel);
		}

		public async Task<DiscordMessage> SendMessage(object sender, SendMessageEventArgs e) {
			logger.Log(sender, new LogEventArgs {
				Type = LogType.Log,
				Message = $"Sending message {e.LogMessage} to channel #{e.Channel.Name} ({e.Channel.Guild.Name})"
			});
			if (e.LogMessage.Length > 2000) {
				int oldLength = e.Message.Length;
				e.LogMessage = e.LogMessage.Substring(0, 2000);
				logger.Log(sender, new LogEventArgs {
					Type = LogType.Warning,
					Message = $"Last message was {oldLength} characters long, truncated to 2000"
				});
			}
			return await e.Channel.SendMessageAsync(e.Message);
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
				string commandTerm = e.Message.Content.Split(' ')[0].ToLower();
				var command = commandManager.GetCommandByTerm(this, e.Guild.Id, commandTerm);
				if (command != null) {
					try {
						await command.OnMessage(e);
					} catch (Exception exc) {
						// This should hopefully make it such that the bot never crashes (although it hasn't stopped it).
						await SendException(this, new SendExceptionEventArgs {
							Exception = exc,
							Channel = e.Channel,
							LogExceptionType = "GenericExceptionNotCaught"
						});
					}
				} else {
					if (commandManager.IsUnknownCommandEnabled && commandTerm.StartsWith(adminCommand.UnknownCommandPrefix)) {
						await SendMessage(this, new SendMessageEventArgs {
							Message = $"I didn't know what you meant by that, {e.Author.Username}. Use {"?help".Code()} to see what I can do!",
							Channel = e.Channel,
							LogMessage = "UnknownMessage"
						});
					}
				}
				foreach (var listener in commandManager.GetMessageListeners(e.Guild.Id)) {
					try {
						await listener.OnMessage(e);
					} catch (Exception exc) {
						await SendException(this, new SendExceptionEventArgs {
							Exception = exc,
							Channel = e.Channel,
							LogExceptionType = "GenericExceptionNotCaught"
						});
					}
				}
			}
		}

		private async Task DiscordReactionAdded(DiscordClient sender, MessageReactionAddEventArgs e) {
			if (!e.User.IsCurrent && !e.User.IsBot) {
				foreach (var listener in commandManager.GetReactionListeners(e.Guild.Id, e.Message.Id)) {
					try {
						await listener.OnReactionAdd(e);
					} catch (Exception exc) {
						await SendException(this, new SendExceptionEventArgs {
							Exception = exc,
							Channel = e.Channel,
							LogExceptionType = "GenericExceptionNotCaught"
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

		private async Task DiscordGuildAvailable(DiscordClient sender, GuildCreateEventArgs e) {
			logger.Log(this, new LogEventArgs {
				Type = LogType.Log,
				Message = $"Guild available: {e.Guild.Name} ({e.Guild.Id})"
			});

			await commandManager.InstantiateCommandsForGuild(e.Guild.Id);
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
			HeartbeatCheck?.Stop();
			HeartbeatCheck?.Start();

			return Task.CompletedTask;
		}

		internal bool IsUserBlendoBotAdmin(ulong guildId, ulong userId) {
			using var dbContext = BlendoBotDbContext.Get();
			return dbContext.AdminUsers.Any(u => u.GuildId == guildId && u.UserId == userId);
		}

		internal async Task<IReadOnlyList<DiscordUser>> GetBlendoBotAdmins(ulong guildId) {
			using var dbContext = BlendoBotDbContext.Get();
			return await Task.WhenAll(dbContext.AdminUsers.Where(u => u.GuildId == guildId).Select(u => discordClient.GetUserAsync(u.UserId)));
		}

		internal bool AddBlendoBotAdmin(ulong guildId, ulong userId) {
			if (IsUserBlendoBotAdmin(guildId, userId)) {
				return false;
			} else {
				using var dbContext = BlendoBotDbContext.Get();
				dbContext.AdminUsers.Add(new AdminUser { GuildId = guildId, UserId = userId });
				dbContext.SaveChanges();
				return true;
			}
		}

		internal bool RemoveBlendoBotAdmin(ulong guildId, ulong userId) {
			if (!IsUserBlendoBotAdmin(guildId, userId)) {
				return false;
			} else {
				using var dbContext = BlendoBotDbContext.Get();
				dbContext.AdminUsers.Remove(new AdminUser { GuildId = guildId, UserId = userId });
				dbContext.SaveChanges();
				return true;
			}
		}
	}
}
