using BlendoBot.Core.Module;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BlendoBot.Frontend.Helper;

internal class ModuleDependencyTree {
	public List<ModuleDependencyNode> RootModules = new();
	public Dictionary<string, ModuleDependencyNode> Nodes = new();
	
	private ModuleDependencyTree() { }

	public static ModuleDependencyTree Create(Dictionary<string, Type> allModuleTypes) {
		ModuleDependencyTree tree = new();
		Stack<string> seenGuids = new();
		foreach (KeyValuePair<string, Type> type in allModuleTypes) {
			tree.InsertNode(type.Key, type.Value, seenGuids);
		}
		return tree;
	}

	private void InsertNode(string guid, Type type, Stack<string> seenGuids) {
		if (!Nodes.ContainsKey(guid)) {
			if (seenGuids.Contains(guid)) {
				throw new Exception($"Module {guid} depends on a module that eventually depends on itself! Dependency stack goes: [{string.Join(", ", seenGuids)}]");
			}
			seenGuids.Push(guid);
			ModuleDependencyNode node = new() {
				ModuleGuid = guid,
				ModuleType = type
			};
			bool noDependencies = true;
			foreach (ModuleDependencyAttribute dependency in type.GetCustomAttributes<ModuleDependencyAttribute>()) {
				noDependencies = false;
				ModuleAttribute dependencyModuleAttribute = dependency.DependsOn.GetCustomAttribute<ModuleAttribute>();
				if (!Nodes.ContainsKey(dependencyModuleAttribute.Guid)) {
					InsertNode(dependencyModuleAttribute.Guid, dependency.DependsOn, seenGuids);
				}
				node.DependsOn.Add(Nodes[dependencyModuleAttribute.Guid]);
				Nodes[dependencyModuleAttribute.Guid].DependedBy.Add(node);
			}
			Nodes.Add(guid, node);
			if (noDependencies) {
				RootModules.Add(node);
			}
			seenGuids.Pop();
		}
	}

	public List<Type> OrderModulesForInstantiation(List<Type> modulesToInstantiate, List<Type> alreadyInstantiatedModules, out List<Type> skippedModules) {
		skippedModules = new();
		List<Type> foundModulesToInstantiate = new();
		foreach (Type module in modulesToInstantiate) {
			RecursiveOrderModulesForInstantiation(module, modulesToInstantiate, foundModulesToInstantiate, alreadyInstantiatedModules, skippedModules);
		}
		return foundModulesToInstantiate;
	}

	private bool RecursiveOrderModulesForInstantiation(Type currentModule, List<Type> modulesToInstantiate, List<Type> foundModulesToInstantiate, List<Type> alreadyInstantiatedModules, List<Type> skippedModules) {
		if (alreadyInstantiatedModules.Contains(currentModule) || foundModulesToInstantiate.Contains(currentModule)) {
			return true;
		} else if (skippedModules.Contains(currentModule) || !modulesToInstantiate.Contains(currentModule)) {
			return false;
		}
		ModuleAttribute moduleAttribute = currentModule.GetCustomAttribute<ModuleAttribute>();
		ModuleDependencyNode node = Nodes[moduleAttribute.Guid];
		foreach (ModuleDependencyNode dependency in node.DependsOn) {
			if (!RecursiveOrderModulesForInstantiation(dependency.ModuleType, modulesToInstantiate, foundModulesToInstantiate, alreadyInstantiatedModules, skippedModules)) {
				skippedModules.Add(currentModule);
				return false;
			}
		}
		foundModulesToInstantiate.Add(currentModule);
		return true;
	}

	internal class ModuleDependencyNode {
		public string ModuleGuid;
		public Type ModuleType;
		public List<ModuleDependencyNode> DependsOn = new();
		public List<ModuleDependencyNode> DependedBy = new();
	}
}
