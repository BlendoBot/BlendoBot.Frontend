using BlendoBot.Core.Interfaces;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Salaros.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlendoBot.Frontend.Services {

	public class Config : IConfig {
		public Config(string configPath) {
			ConfigPath = configPath;
		}

		private readonly Dictionary<string, Dictionary<string, string>> Values = new();
		public string ConfigPath { get; private set; }

		public string ReadConfig(object o, string configHeader, string configKey) {
			if (Values.ContainsKey(configHeader) && Values[configHeader].ContainsKey(configKey)) {
				return Values[configHeader][configKey];
			} else {
				return null;
			}
		}

		public bool DoesConfigKeyExist(object o, string configHeader, string configKey) {
			return Values.ContainsKey(configHeader) && Values[configHeader].ContainsKey(configKey);
		}

		public string ReadConfigOrDefault(object o, string configHeader, string configKey, string defaultValue) {
			if (DoesConfigKeyExist(o, configHeader, configKey)) {
				return ReadConfig(o, configHeader, configKey);
			} else {
				return defaultValue;
			}
		}

		public void WriteConfig(object o, string configHeader, string configKey, string configValue) {
			if (!Values.ContainsKey(configHeader)) {
				Values.Add(configHeader, new Dictionary<string, string>());
			}
			if (!Values[configHeader].ContainsKey(configKey)) {
				Values[configHeader].Add(configKey, configValue);
			} else {
				Values[configHeader][configKey] = configValue;
			}
			// For efficiency, this should be a separate call on a background task. For now, it simplifies the
			// implementation a bit.
			SaveToFile();
		}

		private void SaveToFile() {
			var parser = new ConfigParser();
			foreach (var section in Values) {
				foreach (var key in section.Value) {
					parser.SetValue(section.Key, key.Key, key.Value);
				}
			}
			parser.Save(ConfigPath);
		}

		public string Name => ReadConfig(this, "BlendoBot", "Name");
		public string Version => ReadConfig(this, "BlendoBot", "Version");
		public string Description => ReadConfig(this, "BlendoBot", "Description");
		public string Author => ReadConfig(this, "BlendoBot", "Author");
		public string ActivityName => ReadConfigOrDefault(this, "BlendoBot", "ActivityName", null);
		public ActivityType? ActivityType {
			get {
				try {
					return (ActivityType)Enum.Parse(typeof(ActivityType), ReadConfig(this, "BlendoBot", "ActivityType"));
				} catch (ArgumentException) {
					return null;
				} catch (KeyNotFoundException) {
					//TODO: Double check whether this is necessary.
					return null;
				}
			}
		}

		/// <summary>
		/// Invokes loading the config from its specified file. This only needs to be invoked once as the config is
		/// first start, or if the underlying config file changes, but commands should programmatically invoke
		/// <see cref="WriteConfig(object, string, string, string)"/>.
		public bool Reload() {
			if (!File.Exists(ConfigPath)) {
				return false;
			}
			Values.Clear();
			var parser = new ConfigParser(ConfigPath);
			foreach (var section in parser.Sections) {
				if (!Values.ContainsKey(section.SectionName)) {
					Values.Add(section.SectionName, new Dictionary<string, string>());
				}
				foreach (var pair in section.Keys) {
					if (!Values[section.SectionName].ContainsKey(pair.Name)) {
						Values[section.SectionName].Add(pair.Name, pair.Content);
					} else {
						Values[section.SectionName][pair.Name] = pair.Content;
					}
				}
			}
			return true;
		}

		public void CreateDefaultConfig() {
			Values.Clear();
			WriteConfig(this, "BlendoBot", "Name", "YOUR BLENDOBOT NAME HERE");
			WriteConfig(this, "BlendoBot", "Version", "YOUR BLENDOBOT VERSION HERE");
			WriteConfig(this, "BlendoBot", "Description", "YOUR BLENDOBOT DESCRIPTION HERE");
			WriteConfig(this, "BlendoBot", "Author", "YOUR BLENDOBOT AUTHOR HERE");
			WriteConfig(this, "BlendoBot", "ActivityName", "YOUR BLENDOBOT ACTIVITY NAME HERE");
			WriteConfig(this, "BlendoBot", "ActivityType", "Please replace this with Playing, ListeningTo, Streaming, or Watching.");
			WriteConfig(this, "BlendoBot", "Token", "YOUR BLENDOBOT TOKEN HERE");
		}
	}
}