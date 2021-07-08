using BlendoBot.Commands;
using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Interfaces;
using BlendoBot.Core.Utility;
using BlendoBot.Frontend.Commands;
using BlendoBot.Frontend.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Services {
	public class CommandManager : ICommandManager {
		private Dictionary<string, Type> loadedCommands = new();
		private Dictionary<ulong, Dictionary<string, BaseCommand>> guildCommands = new();
		private Dictionary<ulong, List<IMessageListener>> guildMessageListeners = new();
		private Dictionary<ulong, Dictionary<ulong, List<IReactionListener>>> messageReactionListeners = new();
		private Dictionary<ulong, string> commandPrefixes = new();

		private readonly Logger logger;
		private readonly BlendoBot blendobot;

		public CommandManager(Logger logger, BlendoBot blendobot) {
			this.logger = logger;
			this.blendobot = blendobot;
		}

		public void AddMessageListener(object o, ulong guildId, IMessageListener messageListener) {
			if (!guildMessageListeners.ContainsKey(guildId)) {
				guildMessageListeners.Add(guildId, new List<IMessageListener> { messageListener });
			} else {
				guildMessageListeners[guildId].Add(messageListener);
			}
		}

		public void AddReactionListener(object o, ulong guildId, ulong messageId, IReactionListener reactionListener) {
			if (!messageReactionListeners.ContainsKey(guildId)) {
				messageReactionListeners.Add(guildId, new Dictionary<ulong, List<IReactionListener>>());
			}
			if (!messageReactionListeners[guildId].ContainsKey(messageId)) {
				messageReactionListeners[guildId].Add(messageId, new List<IReactionListener> { reactionListener });
			} else {
				messageReactionListeners[guildId][messageId].Add(reactionListener);
			}
		}

		public T GetCommand<T>(object o, ulong guildId) where T : BaseCommand {
			if (guildCommands.ContainsKey(guildId)) {
				return guildCommands[guildId].FirstOrDefault(c => c.Value is T).Value as T;
			}
			return null;
		}

		public BaseCommand GetCommandByGuid(object o, ulong guildId, string guid) {
			if (guildCommands.ContainsKey(guildId)) {
				return guildCommands[guildId].Values.FirstOrDefault(c => c.Guid == guid);
			}
			return null;
		}

		public BaseCommand GetCommandByTerm(object o, ulong guildId, string term) {
			string prefix = GetCommandPrefix(this, guildId);
			if (guildCommands.ContainsKey(guildId)) {
				if (guildCommands[guildId].ContainsKey(term)) {
					return guildCommands[guildId][term];
				} else if (term.StartsWith(prefix) && guildCommands[guildId].ContainsKey(term[(prefix.Length + 1)..])) {
					return guildCommands[guildId][term[(prefix.Length + 1)..]];
				}
			}
			return null;
		}

		public string GetCommandCommonDataPath(object o, BaseCommand command) {
			if (!Directory.Exists(Path.Combine(Path.Combine("data", "common"), command.Name))) {
				Directory.CreateDirectory(Path.Combine(Path.Combine("data", "common"), command.Name));
			}
			return Path.Combine(Path.Combine("data", "common"), command.Name);
		}

		public string GetCommandInstanceDataPath(object o, BaseCommand command) {
			if (!Directory.Exists(Path.Combine(Path.Combine("data", command.GuildId.ToString()), command.Name))) {
				Directory.CreateDirectory(Path.Combine(Path.Combine("data", command.GuildId.ToString()), command.Name));
			}
			return Path.Combine(Path.Combine("data", command.GuildId.ToString()), command.Name);
		}

		public string GetCommandPrefix(object o, ulong guildId) {
			using var dbContext = BlendoBotDbContext.Get();
			return dbContext.GuildSettings.SingleOrDefault(gs => gs.GuildId == guildId)?.CommandTermPrefix ?? "?";
		}

		public string GetCommandTerm(object o, BaseCommand command) {
			using var dbContext = BlendoBotDbContext.Get();
			return dbContext.GuildSettings.Single(gs => gs.GuildId == command.GuildId)?.CommandTermPrefix + dbContext.CommandSettings.Single(cs => cs.GuildId == command.GuildId && cs.CommandGuid == command.Guid).Term;
		}

		public string GetHelpCommandTerm(object o, ulong guildId) {
			return GetCommand<Help>(this, guildId).Term;
		}

		public void RemoveMessageListener(object o, ulong guildId, IMessageListener messageListener) {
			if (guildMessageListeners.ContainsKey(guildId)) {
				guildMessageListeners[guildId].Remove(messageListener);
			}
			if (messageListener is IDisposable disposable) {
				disposable.Dispose();
			}
		}

		public void RemoveReactionListener(object o, ulong guildId, ulong messageId, IReactionListener reactionListener) {
			if (messageReactionListeners.ContainsKey(guildId) && messageReactionListeners[guildId].ContainsKey(messageId)) {
				messageReactionListeners[guildId][messageId].Remove(reactionListener);
				if (messageReactionListeners[guildId][messageId].Count == 0) {
					messageReactionListeners[guildId].Remove(messageId);
				}
				if (messageReactionListeners[guildId].Count == 0) {
					messageReactionListeners.Remove(guildId);
				}
			}
			if (reactionListener is IDisposable disposable) {
				disposable.Dispose();
			}
		}

		internal List<BaseCommand> GetCommands(object _, ulong guildId) {
			if (guildCommands.ContainsKey(guildId)) {
				return guildCommands[guildId].Values.ToList();
			} else {
				return new List<BaseCommand>();
			}
		}

		internal async Task<bool> AddCommand(object _, ulong guildId, string commandGuid) {
			var commandType = loadedCommands[commandGuid];
			try {
				var commandInstance = Activator.CreateInstance(commandType, new object[] { guildId, this }) as BaseCommand;
				if (await commandInstance.Startup()) {
					guildCommands[guildId].Add(commandInstance.Term, commandInstance);
					logger.Log(this, new LogEventArgs {
						Type = LogType.Log,
						Message = $"Successfully loaded external module {commandInstance.Name} ({commandInstance.Term}) for guild {guildId}"
					});
					return true;
				} else {
					logger.Log(this, new LogEventArgs {
						Type = LogType.Error,
						Message = $"Could not load module {commandInstance.Name} ({commandInstance.Term}), startup failed"
					});
				}
			} catch (Exception exc) {
				logger.Log(this, new LogEventArgs {
					Type = LogType.Error,
					Message = $"Could not load module {commandType.FullName}, instantiation failed and exception thrown\n{exc}"
				});
			}
			return false;
		}

		internal async Task RemoveCommand(object o, ulong guildId, string classTerm) {
			var command = guildCommands[guildId][classTerm];
			int messageListenerCount = 0;
			int reactionListenerCount = 0;
			if (guildMessageListeners.ContainsKey(guildId)) {
				foreach (var messageListener in guildMessageListeners[guildId].Where(ml => ml.Command == command).ToList()) {
					RemoveMessageListener(o, guildId, messageListener);
					++messageListenerCount;
				}
			}
			if (messageReactionListeners.ContainsKey(guildId)) {
				foreach (var messageId in messageReactionListeners[guildId].Keys.ToList()) {
					foreach (var reactionListener in messageReactionListeners[guildId][messageId].Where(rl => rl.Command == command).ToList()) {
						RemoveReactionListener(o, guildId, messageId, reactionListener);
						++reactionListenerCount;
					}
				}
			}
			guildCommands[guildId].Remove(classTerm);
			if (command is IDisposable disposable) {
				disposable.Dispose();
			}
			logger.Log(this, new LogEventArgs {
				Type = LogType.Log,
				Message = $"Successfully unloaded module {command.GetType().FullName}, {messageListenerCount} message listener{(messageListenerCount == 1 ? string.Empty : "s")}, and {reactionListenerCount} reaction listener{(reactionListenerCount == 1 ? string.Empty : "s")}"
			});
			await Task.Delay(0);
		}

		internal void RenameCommand(object o, ulong guildId, string commandTerm, string newTerm) {
			var command = guildCommands[guildId][commandTerm];
			guildCommands[guildId].Remove(commandTerm);
			guildCommands[guildId].Add(newTerm, command);
			logger.Log(this, new LogEventArgs {
				Type = LogType.Log,
				Message = $"Successfully renamed module {command.GetType().FullName} from {commandTerm} to {newTerm}"
			});
		}

		internal void LoadCommands() {
			var dlls = Directory.GetFiles(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)).ToList().FindAll(s => Path.GetExtension(s) == ".dll");
			dlls.RemoveAll(s => Path.GetFileName(s) == "BlendoBot.Core.dll" || Path.GetFileName(s) == "BlendoBot.Frontend.dll");

			foreach (string dll in dlls) {
				var assembly = Assembly.LoadFrom(dll);
				var types = assembly.ExportedTypes.ToList().FindAll(t => t.IsSubclassOf(typeof(BaseCommand)));
				foreach (var type in types) {
					var commandAttribute = type.GetCustomAttribute<CommandAttribute>();
					if (commandAttribute != null) {
						if (loadedCommands.ContainsKey(commandAttribute.Guid)) {
							logger.Log(this, new LogEventArgs {
								Type = LogType.Error,
								Message = $"Detected duplicate command {commandAttribute.Guid}"
							});
						} else {
							loadedCommands.Add(commandAttribute.Guid, type);
							logger.Log(this, new LogEventArgs {
								Type = LogType.Log,
								Message = $"Detected command {commandAttribute.Guid}"
							});
						}
					}
				}
			}
		}

		internal async Task InstantiateCommandsForGuild(ulong guildId) {
			if (guildCommands.ContainsKey(guildId)) {
				return;
			} else {
				guildCommands.Add(guildId, new Dictionary<string, BaseCommand>());
			}
			if (guildMessageListeners.ContainsKey(guildId)) {
				guildMessageListeners[guildId].Clear();
			} else {
				guildMessageListeners.Add(guildId, new List<IMessageListener>());
			}

			var adminCommand = new Admin(guildId, blendobot);
			var systemCommands = new BaseCommand[] { adminCommand, new Help(guildId, blendobot), new About(guildId, blendobot) };

			foreach (var command in systemCommands) {
				await command.Startup();
			}

			foreach (var command in systemCommands) {
				string term = adminCommand.RenameCommandTermFromDatabase(command);
				guildCommands[guildId].Add(term, command);
				logger.Log(this, new LogEventArgs {
					Type = LogType.Log,
					Message = $"Successfully loaded internal module {command.Name} ({term}) for guild {guildId}"
				});
			}

			foreach (var commandType in loadedCommands.Values) {
				if (!adminCommand.IsCommandNameDisabled(commandType.FullName)) {
					try {
						var commandInstance = Activator.CreateInstance(commandType, new object[] { guildId, this }) as BaseCommand;
						if (await commandInstance.Startup()) {
							commandInstance.Term = adminCommand.RenameCommandTermFromDatabase(commandInstance);
							guildCommands[guildId].Add(commandInstance.Term, commandInstance);
							logger.Log(this, new LogEventArgs {
								Type = LogType.Log,
								Message = $"Successfully loaded external module {commandInstance.Name} ({commandInstance.Term}) for guild {guildId}"
							});
						} else {
							logger.Log(this, new LogEventArgs {
								Type = LogType.Error,
								Message = $"Could not load module {commandInstance.Name}, startup failed"
							});
						}
					} catch (Exception exc) {
						logger.Log(this, new LogEventArgs {
							Type = LogType.Error,
							Message = $"Could not load module {commandType.FullName}, instantiation failed and exception thrown\n{exc}"
						});
					}
				} else {
					logger.Log(this, new LogEventArgs {
						Type = LogType.Log,
						Message = $"Module {commandType.FullName} is disabled and will not be instatiated"
					});
				}
			}

			logger.Log(this, new LogEventArgs {
				Type = LogType.Log,
				Message = $"All modules have finished loading for guild {guildId}"
			});
		}

		internal List<IMessageListener> GetMessageListeners(ulong guildId) {
			if (!guildMessageListeners.ContainsKey(guildId)) {
				return new();
			} else {
				return new(guildMessageListeners[guildId]);
			}
		}

		internal List<IReactionListener> GetReactionListeners(ulong guildId, ulong messageId) {
			if (!messageReactionListeners.ContainsKey(guildId) || !messageReactionListeners[guildId].ContainsKey(messageId)) {
				return new();
			} else {
				return new(messageReactionListeners[guildId][messageId]);
			}
		}

		internal List<CommandSettings> GetEnabledLoadedComamnds(ulong guildId) {
			using var dbContext = BlendoBotDbContext.Get();
			return dbContext.CommandSettings.Where(cs => cs.GuildId == guildId && cs.Enabled && loadedCommands.ContainsKey(cs.CommandGuid)).ToList();
		}

		internal List<CommandSettings> GetDisabledLoadedComamnds(ulong guildId) {
			using var dbContext = BlendoBotDbContext.Get();
			return dbContext.CommandSettings.Where(cs => cs.GuildId == guildId && !cs.Enabled && loadedCommands.ContainsKey(cs.CommandGuid)).ToList();
		}

		private (bool Result, string Reason) DisableCommand(ulong guildId, string argument, BlendoBotDbContext dbContext, CommandSettings commandSettings) {
			if (commandSettings == null) {
				return (false, $"Command {argument.Code()} does not exist");
			} else if (commandSettings.Enabled) {
				return (false, $"Command {argument.Code()} is already enabled");
			} else if (!loadedCommands.ContainsKey(commandSettings.CommandGuid)) {
				return (false, $"Command {argument.Code()} has not been loaded and cannot be enabled");
			}
			var command = guildCommands[guildId][commandSettings.Term];
			if (command is IDisposable disposable) {
				disposable.Dispose();
			}
			guildCommands[guildId].Remove(commandSettings.Term);
			dbContext.CommandSettings.Update(commandSettings with { Enabled = false });
			dbContext.SaveChanges();
			return (true, "Success");
		}

		internal async Task<(bool Result, string Reason)> EnableCommandByGuid(ulong guildId, string commandGuid) {
			using var dbContext = BlendoBotDbContext.Get();
			if (!loadedCommands.ContainsKey(commandGuid)) {
				return (false, $"Command {commandGuid.Code()} is not loaded by BlendoBot.");
			}
			var commandType = loadedCommands[commandGuid];
			var commandSettings = dbContext.CommandSettings.SingleOrDefault(cs => cs.GuildId == guildId && cs.CommandGuid == commandGuid);

			if (commandSettings == null) {
				commandSettings = new CommandSettings {
					GuildId = guildId,
					CommandGuid = commandGuid,
					Term = commandType.GetCustomAttribute<CommandAttribute>().DefaultTerm,
					Enabled = false
				};
			}

			if (commandSettings.Enabled && guildCommands[guildId].ContainsKey(commandSettings.Term)) {

			}

			if (commandSettings == null) {
				return (false, $"Command {commandGuid.Code()} does not exist");
			} else if (commandSettings.Enabled) {
				return (false, $"Command {commandGuid.Code()} is already enabled");
			} else if (!loadedCommands.ContainsKey(commandSettings.CommandGuid)) {
				return (false, $"Command {commandGuid.Code()} has not been loaded and cannot be enabled");
			}
			var command = Activator.CreateInstance(loadedCommands[commandSettings.CommandGuid], new object[] { guildId, this }) as BaseCommand;
			bool result = await command.Startup();

			if (result) {
				guildCommands[guildId].Add(commandSettings.Term, command);
				dbContext.CommandSettings.Update(commandSettings with { Enabled = true });
				dbContext.SaveChanges();
				return (true, "Success");
			} else {
				return (false, $"Command {commandSettings.Term.Code()} couldn't be started up");
			}
		}

		internal (bool Result, string Reason) DisableCommandByGuid(ulong guildId, string commandGuid) {
			using var dbContext = BlendoBotDbContext.Get();
			return DisableCommand(guildId, commandGuid, dbContext, dbContext.CommandSettings.SingleOrDefault(cs => cs.GuildId == guildId && cs.CommandGuid == commandGuid));
		}
	}
}
