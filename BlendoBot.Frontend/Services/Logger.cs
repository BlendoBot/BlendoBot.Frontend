using BlendoBot.Core.Entities;
using BlendoBot.Core.Services;
using System;
using System.IO;
using System.Threading;

namespace BlendoBot.Frontend.Services;

/// <summary>
/// This logger writes to both standard output and to a file. The logfile, in the current working directory, is of
/// path log/yyyyMMddHHmmss.log, where yyyyMMddHHmmss is replaced by the start time of the program.
/// </summary>
internal class Logger : ILogger {
	public readonly string LogFilePath;
	public readonly DateTime StartTime;

	public Logger(DateTime startTime) {
		StartTime = startTime;
		LogFilePath = Path.Join("log", $"{startTime:yyyyMMddHHmmss}.log");
	}

	private int numReadErrors = 0;
	private const int MAX_READ_ATTEMPTS = 20;

	public void Log(object o, LogEventArgs e) {
		int attempts = 0;
		bool success = false;
		while (!success && attempts < MAX_READ_ATTEMPTS) {
			string typeString = Enum.GetName(typeof(LogType), e.Type);
			string logMessage = $"({DateTime.Now:yyyy-MM-dd HH:mm:ss}) [{o?.GetType().FullName ?? "null"}] | {e.Message}";
			ConsoleColor oldForegroundColor = Console.ForegroundColor;
			switch (e.Type) {
				case LogType.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					break;
				case LogType.Log:
					Console.ForegroundColor = ConsoleColor.Cyan;
					break;
				case LogType.Warning:
					Console.ForegroundColor = ConsoleColor.Yellow;
					break;
				case LogType.Critical:
					Console.ForegroundColor = ConsoleColor.DarkRed;
					break;
			}
			Console.Write($"[{typeString}] ");
			Console.ForegroundColor = oldForegroundColor;
			Console.WriteLine(logMessage);
			if (!Directory.Exists("log")) Directory.CreateDirectory("log");

			try {
				using FileStream logStream = File.Open(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
				using StreamWriter writer = new(logStream);
				writer.WriteLine($"[{typeString}] {logMessage}");
				success = true;
			} catch (IOException) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Couldn't open the logfile. This has now happend {++numReadErrors} times.");
				Console.ForegroundColor = oldForegroundColor;
				Thread.Sleep(5);
			}
		}
	}
}
