using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Messages;
using BlendoBot.Core.Module;
using BlendoBot.Core.Reactions;
using BlendoBot.Core.Services;
using BlendoBot.Frontend.Database;
using BlendoBot.Frontend.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Services;

internal class ModuleManager : IModuleManager {
	private class GuildModules {
		// Key is the command term (without any prefix).
		public Dictionary<string, ICommand> Commands = new();

		// Key is the message ID.
		public Dictionary<ulong, List<IReactionListener>> ReactionListeners = new();

		// Key is the module GUID.
		public Dictionary<string, ModuleEntities> ModuleEntities = new();

		public string CommandPrefix = "?";

		public bool IsUnknownCommandMessageEnabled = true;
	}

	private class CommandDetails {
		public string Term;
		public ICommand Command;
	}

	private class ModuleEntities {
		public IModule Module;

		// Key is the command GUID.
		public Dictionary<string, CommandDetails> Commands = new();

		public List<IMessageListener> MessageListeners = new();

		public List<IReactionListener> ReactionListeners = new();
	}

	// Key is the module GUID.
	private readonly Dictionary<string, Type> loadedModules = new();

	private ModuleDependencyTree dependencyTree;

	// Key is the guild id.
	private readonly Dictionary<ulong, GuildModules> guildModules = new();

	private readonly Logger logger;
	private readonly ServiceManager serviceManager;

	public ModuleManager(Logger logger, ServiceManager serviceManager) {
		this.logger = logger;
		this.serviceManager = serviceManager;
	}

	internal void LoadModules() {
		List<string> dlls = Directory.GetFiles(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)).ToList().FindAll(s => Path.GetExtension(s) == ".dll");
		dlls.RemoveAll(s => Path.GetFileName(s) == "BlendoBot.Core.dll" || Path.GetFileName(s) == "BlendoBot.Frontend.dll");

		foreach (string dll in dlls) {
			Assembly assembly = Assembly.LoadFrom(dll);
			IEnumerable<Type> types = assembly.ExportedTypes;
			foreach (Type type in types) {
				ModuleAttribute moduleAttribute = type.GetCustomAttribute<ModuleAttribute>();
				if (moduleAttribute != null) {
					if (loadedModules.ContainsKey(moduleAttribute.Guid)) {
						logger.Log(this, new LogEventArgs {
							Type = LogType.Error,
							Message = $"Detected duplicate module {moduleAttribute.Guid}"
						});
					} else {
						loadedModules.Add(moduleAttribute.Guid, type);
						logger.Log(this, new LogEventArgs {
							Type = LogType.Log,
							Message = $"Detected module {moduleAttribute.Guid}"
						});
					}
				}
			}
		}
		loadedModules.Add(typeof(AdminModule.Admin).GetCustomAttribute<ModuleAttribute>().Guid, typeof(AdminModule.Admin));
		dependencyTree = ModuleDependencyTree.Create(loadedModules);
	}

	internal async Task<bool> InstantiateModulesForGuild(ulong guildId) {
		if (guildModules.ContainsKey(guildId)) {
			return true;
		}
		SetupGuildModulesObject(guildId);

		List<Type> modulesToInstantiate = dependencyTree.OrderModulesForInstantiation(GetModulesToInstantiateForGuild(guildId), new List<Type>(), out List<Type> skippedModules);

		logger.Log(this, new LogEventArgs {
			Type = LogType.Log,
			Message = $"{modulesToInstantiate.Count} modules to instantiate for guild {guildId}: [{string.Join(", ", modulesToInstantiate.Select(m => m.GetCustomAttribute<ModuleAttribute>().Guid))}]"
		});

		if (skippedModules.Count > 0) {
			logger.Log(this, new LogEventArgs {
				Type = LogType.Warning,
				Message = $"{skippedModules.Count} were automatically disabled due to missing dependencies: [{string.Join(", ", skippedModules.Select(m => m.GetCustomAttribute<ModuleAttribute>().Guid))}]"
			});
		}

		bool anyFailures = false;

		foreach (Type moduleType in modulesToInstantiate) {
			anyFailures |= !await InstantiateModuleForGuild(guildId, moduleType);
		}

		// TODO: Clean up any failures.

		return !anyFailures;
	}

	private void SetupGuildModulesObject(ulong guildId) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		Guild guildDbEntity = dbContext.Guilds.FirstOrDefault(g => g.GuildId == guildId);
		if (guildDbEntity == default) {
			dbContext.Guilds.Add(new Guild { GuildId = guildId, CommandTermPrefix = "?", UnknownCommandResponseEnabled = true });
			guildModules.Add(guildId, new());
			dbContext.SaveChanges();
		} else {
			guildModules.Add(guildId, new() {
				CommandPrefix = guildDbEntity.CommandTermPrefix,
				IsUnknownCommandMessageEnabled = guildDbEntity.UnknownCommandResponseEnabled
			});
		}
	}

	private List<Type> GetModulesToInstantiateForGuild(ulong guildId) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();

		List<Database.Module> dbModules = dbContext.Modules.Where(m => m.GuildId == guildId).ToList();

		List<Type> modulesToInstantiate = new();

		ModuleAttribute adminModuleAttribute = typeof(AdminModule.Admin).GetCustomAttribute<ModuleAttribute>();
		if (!dbModules.Exists(m => m.GuildId == guildId && m.Guid == adminModuleAttribute.Guid)) {
			dbModules.Add(new Database.Module {
				GuildId = guildId,
				Guid = adminModuleAttribute.Guid,
				Enabled = true
			});
			dbContext.SaveChanges();
		}
		foreach (KeyValuePair<string, Type> loadedModule in loadedModules) {
			Database.Module dbModule = dbModules.Find(m => m.GuildId == guildId && m.Guid == loadedModule.Key);
			if (dbModule == null) {
				dbModules.Add(new Database.Module {
					GuildId = guildId,
					Guid = loadedModule.Key,
					Enabled = false
				});
			} else if (dbModule.Enabled) {
				modulesToInstantiate.Add(loadedModule.Value);
			}
		}

		return modulesToInstantiate;
	}

	internal async Task<bool> InstantiateModuleForGuild(ulong guildId, Type moduleType) {
		ConstructorInfo constructor = moduleType.GetConstructors().Single();
		List<object> parameters = new();
		foreach (ParameterInfo parameter in constructor.GetParameters()) {
			parameters.Add(serviceManager.GetService(parameter.ParameterType));
		}
		IModule instance = constructor.Invoke(parameters.ToArray()) as IModule;

		string moduleGuid = moduleType.GetCustomAttribute<ModuleAttribute>().Guid;
		guildModules[guildId].ModuleEntities.Add(moduleGuid, new ModuleEntities {
			Module = instance
		});

		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		Database.Module module = dbContext.Modules.FirstOrDefault(m => m.GuildId == guildId && m.Guid == moduleGuid);
		if (module == null) {
			module = new() { Enabled = true, Guid = moduleGuid, GuildId = guildId };
			dbContext.Modules.Add(module);
			await dbContext.SaveChangesAsync();
		}

		if (await instance.Startup(guildId)) {
			logger.Log(this, new LogEventArgs {
				Type = LogType.Log,
				Message = $"Successfully instantiated {moduleGuid} for guild {guildId}."
			});

			module.Enabled = true;
			await dbContext.SaveChangesAsync();

			return true;
		} else {
			logger.Log(this, new LogEventArgs {
				Type = LogType.Log,
				Message = $"Could not successfully instantiate {moduleGuid} for guild {guildId}."
			});

			guildModules[guildId].ModuleEntities.Remove(moduleGuid);
			module.Enabled = false;
			await dbContext.SaveChangesAsync();

			return false;
		}
	}

	internal async Task<bool> UninstantiateModuleForGuild(ulong guildId, Type moduleType, bool persistChanges) {
		string moduleGuid = moduleType.GetCustomAttribute<ModuleAttribute>().Guid;
		IModule instance = guildModules[guildId].ModuleEntities[moduleGuid].Module;
		List<ICommand> commandsToUnregister = new(guildModules[guildId].ModuleEntities[moduleGuid].Commands.Select(kv => kv.Value.Command));
		foreach (ICommand command in commandsToUnregister) {
			UnregisterCommand(instance, command);
		}
		if (instance is IDisposable disposable) {
			disposable.Dispose();
		}
		logger.Log(this, new LogEventArgs {
			Type = LogType.Log,
			Message = $"Successfully uninstantiated {moduleGuid} for guild {guildId}."
		});
		guildModules[guildId].ModuleEntities.Remove(moduleGuid);
		if (persistChanges) {
			using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
			Database.Module module = dbContext.Modules.FirstOrDefault(m => m.GuildId == guildId && m.Guid == moduleGuid);
			if (module != null) {
				module.Enabled = false;
				dbContext.Modules.Update(module);
			}
			await dbContext.SaveChangesAsync();
		}
		return true;
	}

	internal ulong GetGuildIdForModule(IModule module) {
		foreach (KeyValuePair<ulong, GuildModules> modules in guildModules) {
			foreach (ModuleEntities m in modules.Value.ModuleEntities.Values) {
				if (m.Module == module) {
					return modules.Key;
				}
			}
		}
		return 0ul;
	}

	public bool RegisterCommand(IModule module, ICommand command, out string commandTerm) {
		ulong guildId = GetGuildIdForModule(module);
		string moduleGuid = module.GetType().GetCustomAttribute<ModuleAttribute>().Guid;
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		Command commandEntity = dbContext.Commands.FirstOrDefault(c => c.GuildId == guildId && c.ModuleGuid == moduleGuid && c.Guid == command.Guid);
		if (commandEntity == null) {
			commandEntity = new() {
				GuildId = guildId,
				ModuleGuid = moduleGuid,
				Guid = command.Guid,
				Term = command.DesiredTerm
			};
			dbContext.Commands.Add(commandEntity);
			dbContext.SaveChanges();
		}
		
		commandTerm = GetUniqueTermForCommand(guildId, commandEntity.Term);
		guildModules[guildId].ModuleEntities[moduleGuid].Commands.Add(command.Guid, new CommandDetails {
			Term = commandTerm,
			Command = command
		});
		guildModules[guildId].Commands.Add(commandTerm, command);
		return true;
	}

	public bool UnregisterCommand(IModule module, ICommand command) {
		ulong guildId = GetGuildIdForModule(module);
		string moduleGuid = module.GetType().GetCustomAttribute<ModuleAttribute>().Guid;
		CommandDetails commandDetails = guildModules[guildId].ModuleEntities[moduleGuid].Commands.GetValueOrDefault(command.Guid);
		if (commandDetails != null) {
			guildModules[guildId].Commands.Remove(commandDetails.Term);
			guildModules[guildId].ModuleEntities[moduleGuid].Commands.Remove(command.Guid);
			return true;
		}
		return false;
	}

	private string GetUniqueTermForCommand(ulong guildId, string desiredTerm) {
		if (!guildModules[guildId].Commands.ContainsKey(desiredTerm)) {
			return desiredTerm;
		} else {
			int i = 2;
			// This is a bit of an implicit solution since no one will have that many commands instantiated.
			while (guildModules[guildId].Commands.ContainsKey($"{desiredTerm}-{i}")) {
				++i;
			}
			return $"{desiredTerm}-{i}";
		}
	}

	public string GetCommandTerm(ICommand command) {
		ulong guildId = GetGuildIdForModule(command.Module);
		return guildModules[guildId].Commands.Single(c => c.Value == command).Key;
	}

	public string GetCommandTermWithPrefix(ICommand command) {
		return $"{GetCommandPrefixForGuild(GetGuildIdForModule(command.Module))}{GetCommandTerm(command)}";
	}

	internal string GetHelpTerm(ulong guildId) {
		return $"{GetCommandPrefixForGuild(guildId)}{guildModules[guildId].ModuleEntities[typeof(AdminModule.Admin).GetCustomAttribute<ModuleAttribute>().Guid].Commands["admin.help"].Term}";
	}

	public string GetHelpTermForCommand(ICommand command) {
		ulong guildId = GetGuildIdForModule(command.Module);
		return $"{GetHelpTerm(guildId)} {GetCommandTerm(command)}";
	}

	internal ICommand GetCommandByTerm(ulong guildId, string commandTerm) {
		return guildModules[guildId].Commands.GetValueOrDefault(commandTerm);
	}

	internal string GetCommandPrefixForGuild(ulong guildId) {
		return guildModules[guildId].CommandPrefix;
	}

	internal bool IsUnknownCommandMessageEnabled(ulong guildId) {
		return guildModules[guildId].IsUnknownCommandMessageEnabled;
	}

	internal IReadOnlyDictionary<string, ICommand> GetCommands(ulong guildId) {
		return guildModules[guildId].Commands;
	}

	internal void ChangeCommandPrefix(ulong guildId, string newPrefix) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		Guild guildSettings = dbContext.Guilds.Single(g => g.GuildId == guildId);
		guildSettings.CommandTermPrefix = newPrefix;
		guildModules[guildId].CommandPrefix = newPrefix;
		dbContext.SaveChanges();
	}

	internal void SetUnknownCommandResponse(ulong guildId, bool isEnabled) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		Guild guildSettings = dbContext.Guilds.Single(g => g.GuildId == guildId);
		guildSettings.UnknownCommandResponseEnabled = isEnabled;
		guildModules[guildId].IsUnknownCommandMessageEnabled = isEnabled;
		dbContext.SaveChanges();
	}

	internal List<(string Guid, Type Type, bool Enabled)> GetAllModules(ulong guildId) {
		List<(string Guid, Type Type, bool Enabled)> foundModules = new();
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		foreach (KeyValuePair<string, Type> module in loadedModules) {
			foundModules.Add((module.Key, module.Value, dbContext.Modules.Any(m => m.GuildId == guildId && m.Guid == module.Key && m.Enabled)));
		}
		return foundModules;
	}

	internal Type GetModuleTypeByGuid(string guid) {
		return loadedModules.GetValueOrDefault(guid);
	}

	internal bool DoesModuleHaveUninstantiatedDependencies(ulong guildId, Type moduleType, out List<string> missingDependencyGuids) {
		if (dependencyTree.OrderModulesForInstantiation(new List<Type>() { moduleType }, guildModules[guildId].ModuleEntities.Select(e => loadedModules[e.Key]).ToList(), out _).Count == 0) {
			missingDependencyGuids = dependencyTree.Nodes[moduleType.GetCustomAttribute<ModuleAttribute>().Guid].DependsOn.Select(d => d.ModuleGuid).Where(g => !guildModules[guildId].ModuleEntities.ContainsKey(g)).ToList();
			return true;
		} else {
			missingDependencyGuids = new();
			return false;
		}
	}

	internal bool DoesModuleHaveInstantiatedDependants(ulong guildId, Type moduleType, out List<string> liveDependantGuids) {
		liveDependantGuids = new();
		foreach (ModuleDependencyTree.ModuleDependencyNode dependant in dependencyTree.Nodes[moduleType.GetCustomAttribute<ModuleAttribute>().Guid].DependedBy) {
			if (guildModules[guildId].ModuleEntities.ContainsKey(dependant.ModuleGuid)) {
				liveDependantGuids.Add(dependant.ModuleGuid);
			}
		}
		return liveDependantGuids.Count > 0;
	}

	internal bool IsModuleEnabled(ulong guildId, Type moduleType) {
		string moduleGuid = moduleType.GetCustomAttribute<ModuleAttribute>().Guid;
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		return dbContext.Modules.Any(m => m.GuildId == guildId && m.Guid == moduleGuid && m.Enabled);
	}

	public T GetModule<T>(ulong guildId) where T : IModule {
		string moduleGuid = typeof(T).GetCustomAttribute<ModuleAttribute>().Guid;
		if (guildModules[guildId].ModuleEntities.TryGetValue(moduleGuid, out ModuleEntities entity)) {
			return (T)entity.Module;
		} else {
			return default;
		}
	}

	public bool RegisterMessageListener(IModule module, IMessageListener messageListener) {
		string moduleGuid = module.GetType().GetCustomAttribute<ModuleAttribute>().Guid;
		List<IMessageListener> messageListeners = guildModules[GetGuildIdForModule(module)].ModuleEntities[moduleGuid].MessageListeners;
		if (messageListeners.Contains(messageListener)) {
			return false;
		} else {
			messageListeners.Add(messageListener);
			return true;
		}
	}

	public bool UnregisterMessageListener(IModule module, IMessageListener messageListener) {
		string moduleGuid = module.GetType().GetCustomAttribute<ModuleAttribute>().Guid;
		ulong guildId = GetGuildIdForModule(module);
		return guildModules[guildId].ModuleEntities[moduleGuid].MessageListeners.Remove(messageListener);
	}

	public bool RegisterReactionListener(IModule module, IReactionListener reactionListener, ulong messageId) {
		string moduleGuid = module.GetType().GetCustomAttribute<ModuleAttribute>().Guid;
		ulong guildId = GetGuildIdForModule(module);
		List<IReactionListener> reactionListeners = guildModules[GetGuildIdForModule(module)].ModuleEntities[moduleGuid].ReactionListeners;
		if (reactionListeners.Contains(reactionListener)) {
			return false;
		} else {
			reactionListeners.Add(reactionListener);
			guildModules[guildId].ReactionListeners.TryAdd(messageId, new());
			guildModules[guildId].ReactionListeners[messageId].Add(reactionListener);
			return true;
		}
	}

	public bool UnregisterReactionListener(IModule module, IReactionListener reactionListener, ulong messageId) {
		string moduleGuid = module.GetType().GetCustomAttribute<ModuleAttribute>().Guid;
		ulong guildId = GetGuildIdForModule(module);
		return guildModules[guildId].ModuleEntities[moduleGuid].ReactionListeners.Remove(reactionListener) && guildModules[guildId].ReactionListeners.Remove(messageId);
	}

	internal List<IReactionListener> GetReactionListeners(ulong guildId, ulong messageId) {
		if (guildModules[guildId].ReactionListeners.TryGetValue(messageId, out List<IReactionListener> reactionListeners)) {
			return reactionListeners;
		} else {
			return new();
		}
	}

	internal IEnumerable<IMessageListener> GetMessageListeners(ulong guildId) {
		return guildModules[guildId].ModuleEntities.Values.SelectMany(e => e.MessageListeners);
	}

	internal void RenameCommand(ICommand command, string newCommandTerm) {
		ulong guildId = GetGuildIdForModule(command.Module);
		string moduleGuid = command.Module.GetType().GetCustomAttribute<ModuleAttribute>().Guid;
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		Command commandEntity = dbContext.Commands.FirstOrDefault(c => c.GuildId == guildId && c.ModuleGuid == moduleGuid && c.Guid == command.Guid);
		string oldCommandTerm = commandEntity.Term;
		commandEntity.Term = newCommandTerm;

		guildModules[guildId].Commands.Add(newCommandTerm, command);
		guildModules[guildId].Commands.Remove(oldCommandTerm);
		guildModules[guildId].ModuleEntities[moduleGuid].Commands[command.Guid].Term = newCommandTerm;

		dbContext.SaveChanges();
	}
}
