#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Hearthstone_Deck_Tracker.Controls.Error;
using Hearthstone_Deck_Tracker.Utility.Extensions;

#endregion

namespace Hearthstone_Deck_Tracker.Utility.Logging
{
	[DebuggerStepThrough]
	public class Log
	{
		private const int MaxLogFileAge = 2;
		private const int KeepOldLogs = 25;
		private static readonly Queue<string> LogQueue = new Queue<string>();
		private static string? _prevLine;
		private static int _duplicateCount;
		public static bool Initialized { get; private set; }
		public static string? CurrentLogFile { get; private set; }

		internal static void Initialize()
		{
			if(Initialized)
				return;
			Trace.AutoFlush = true;
			var logDir = Path.Combine(Config.Instance.DataDir, "Logs");
			var logFile = Path.Combine(logDir, "hdt_log.txt");
			if(!Directory.Exists(logDir))
				Directory.CreateDirectory(logDir);
			else
			{
				try
				{
					var fileInfo = new FileInfo(logFile);
					if(fileInfo.Exists)
					{
						using(var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.None))
						{
							//can access log file => no other instance of same installation running
						}
						File.Move(logFile, logFile.Replace(".txt", "_" + DateTime.Now.ToUnixTimeSeconds() + ".txt"));
						//keep logs from the last 2 days plus 25 before that
						foreach(var file in
							new DirectoryInfo(logDir).GetFiles("hdt_log*")
													 .Where(x => x.LastWriteTime < DateTime.Now.AddDays(-MaxLogFileAge))
													 .OrderByDescending(x => x.LastWriteTime)
													 .Skip(KeepOldLogs))
						{
							try
							{
								File.Delete(file.FullName);
							}
							catch
							{
							}
						}
					}
					else
						File.Create(logFile).Dispose();
				}
				catch(Exception)
				{
					MessageBox.Show("Another instance of Hearthstone Deck Tracker is already running.",
						"Error starting Hearthstone Deck Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
					Application.Current.Shutdown();
					return;
				}
			}
			try
			{
				Trace.Listeners.Add(new TextWriterTraceListener(new StreamWriter(logFile, false)));
				CurrentLogFile = logFile;
			}
			catch(Exception ex)
			{
				ErrorManager.AddError("Can not access log file.", ex.ToString());
			}

			Initialized = true;
			try
			{
				foreach(var line in LogQueue)
					Trace.WriteLine(line);
			}
			catch(Exception e)
			{
				HandleWriteToTraceException(e);
			}
		}

		public static event Action<string>? OnLogLine;

		public static void WriteLine(string msg, LogType type, [CallerMemberName] string memberName = "",
									 [CallerFilePath] string sourceFilePath = "")
		{
#if (!DEBUG)
			if(type == LogType.Debug && Config.Instance.LogLevel == 0)
				return;
#endif
			var file = sourceFilePath?.Split('/', '\\').LastOrDefault()?.Split('.').FirstOrDefault();
			var line = $"{type}|{file}.{memberName} >> {msg}";

			if(line == _prevLine)
				_duplicateCount++;
			else
			{
				if(_duplicateCount > 0)
					Write($"... {_duplicateCount} duplicate messages");
				_prevLine = line;
				_duplicateCount = 0;
				Write(line);
			}
		}

		private static void Write(string line)
		{
			line = $"{DateTime.Now.ToLongTimeString()}|{line}";
			if(Initialized)
			{
				try
				{
					Trace.WriteLine(line);
					OnLogLine?.Invoke(line);
				}
				catch(Exception e)
				{
					HandleWriteToTraceException(e);
				}
			}
			else
				LogQueue.Enqueue(line);
		}

		private static void HandleWriteToTraceException(Exception e)
		{
			if(e is IOException)
				ErrorManager.AddError("Error writing to disk", e.Message);
		}

		public static void Debug(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
			=> WriteLine(msg, LogType.Debug, memberName, sourceFilePath);

		public static void Info(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
			=> WriteLine(msg, LogType.Info, memberName, sourceFilePath);

		public static void Warn(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
			=> WriteLine(msg, LogType.Warning, memberName, sourceFilePath);

		public static void Error(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
		{
			WriteLine(msg, LogType.Error, memberName, sourceFilePath);
#if DEBUG
			// For visibility of potential issues we are breaking here in debug mode.
			// Take a look at the call stack and see how you got here.
			//
			// You want to ensure this issue does not affect production, and/or fix it.
			// Alternatively consider changing the Log.Error() call to Log.Warn(), if this is not a serious issue.
			if(Debugger.IsAttached)
				Debugger.Break();
#endif
		}

		public static void Error(Exception ex, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
		{
			WriteLine(ex.ToString(), LogType.Error, memberName, sourceFilePath);
#if DEBUG
			// For visibility of potential issues we are breaking here in debug mode.
			// Take a look at the call stack and see how you got here.
			//
			// You want to ensure this issue does not affect production, and/or fix it.
			// Alternatively consider changing the Log.Error() call to Log.Warn(), if this is not a serious issue.
			if(Debugger.IsAttached)
				Debugger.Break();
#endif
		}
	}
}
