using BlendoBot.Core.Module;
using BlendoBot.Core.Services;
using System.IO;
using System.Reflection;

namespace BlendoBot.Frontend.Services;

internal class FilePathProvider : IFilePathProvider {
	private readonly ModuleManager moduleManager;

	public FilePathProvider(ModuleManager moduleManager) {
		this.moduleManager = moduleManager;
	}

	public string GetCommonDataDirectoryPath(IModule module) {
		string moduleGuid = module.GetType().GetCustomAttribute<ModuleAttribute>().Guid;
		string intendedPath = Path.Combine(Path.Combine("data", "common"), moduleGuid);
		Directory.CreateDirectory(intendedPath);
		return intendedPath;
	}

	public string GetDataDirectoryPath(IModule module) {
		string moduleGuid = module.GetType().GetCustomAttribute<ModuleAttribute>().Guid;
		ulong guildId = moduleManager.GetGuildIdForModule(module);
		string intendedPath = Path.Combine(Path.Combine("data", guildId.ToString()), moduleGuid);
		Directory.CreateDirectory(intendedPath);
		return intendedPath;
	}

	internal static string GetAdminDatabasePath() {
		string intendedPath = Path.Combine(Path.Combine("data", "common"), "BlendoBot");
		Directory.CreateDirectory(intendedPath);
		return intendedPath;
	}
}
