using BlendoBot.Core.Entities;
using BlendoBot.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Services {
	/// <summary>
	/// This logger writes to both standard output and to a file. The logfile, in the current working directory, is of
	/// path log/yyyyMMddHHmmss.log, where yyyyMMddHHmmss is replaced by the start time of the program.
	/// </summary>
	public class Logger : ILogger {
		public readonly string LogFilePath;

		public Logger(DateTime startTime) {
			LogFilePath = Path.Join("log", $"{startTime:yyyyMMddHHmmss}.log");
		}

		public void Log(object o, LogEventArgs e) {
			string typeString = Enum.GetName(typeof(LogType), e.Type);
			string logMessage = $"[{typeString}] ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) [{o?.GetType().FullName ?? "null"}] | {e.Message}";
			Console.WriteLine(logMessage);
			if (!Directory.Exists("log")) Directory.CreateDirectory("log");
			using var logStream = File.Open(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
			using var writer = new StreamWriter(logStream);
			writer.WriteLine(logMessage);
		}
	}
}
