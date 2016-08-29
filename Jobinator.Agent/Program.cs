//#define ADVANCED_LOG
using System;
using System.Collections.Generic;

namespace Jobinator.Sample.Agent
{
	public static class StringExtension
	{
		public static IEnumerable<string> SplitByLength(this string str, int maxLength)
		{
			for (int index = 0; index < str.Length; index += maxLength)
			{
				yield return str.Substring(index, Math.Min(maxLength, str.Length - index));
			}
		}
	}

	class Program
	{
		//[DllImport("Kernel32.dll"), SuppressUnmanagedCodeSecurity]
		//public static extern int GetCurrentProcessorNumber();

		static void Main(string[] args)
		{
			Core.Configuration oConfiguration = new Core.Configuration();
			
			oConfiguration.Mode = Core.Configuration.EMode.Agent;
			oConfiguration.MainServer = Properties.Settings.Default.MainServer;
			oConfiguration.MainServer = "cth-orion";
			oConfiguration.MainServerPort = Properties.Settings.Default.MainServerPort;
			oConfiguration.AcceptedQueue = Properties.Settings.Default.Queues.Split(';');
			if (Properties.Settings.Default.MaxThread > 0)
				oConfiguration.MaxThread = Properties.Settings.Default.MaxThread;

#if ADVANCED_LOG
			Console.CursorVisible = false;

			int iColumnWidth = Console.WindowWidth / Environment.ProcessorCount - 1;

			int iPos = iColumnWidth;
			while (iPos < Console.WindowWidth)
			{
				for (int i = 0;i < Console.WindowHeight; ++i)
				{
					Console.SetCursorPosition(iPos, i);
					Console.Write("|");
				}
				iPos += iColumnWidth + 1;
			}
			//List<string>[] lThreadLines = new List<string>[oConfiguration.MaxThread];
			List<int> vProcessorOrder = new List<int>();

			/*oConfiguration.OnLog = (eLevel, sMsg) =>
			{
				lock (vProcessorOrder)
				{
					//int iProcessor = GetCurrentProcessorNumber();
					int iProcessor = 0;
					int iIndex = vProcessorOrder.IndexOf(iProcessor);
					if (iIndex == -1)
					{
						vProcessorOrder.Add(iProcessor);
						iProcessor = vProcessorOrder.Count - 1;
					}
					else
					{
						iProcessor = iIndex;
					}
					int iMin = (iProcessor) * (iColumnWidth + 1);
					int iMax = iMin + iColumnWidth;


					int iLineCount = 0;
					string[] vLines = sMsg.Split('\n');
					foreach (string sLine in vLines)
					{
						iLineCount += (int)Math.Ceiling(sLine.Length / (float)iColumnWidth);
					}

					Console.MoveBufferArea(iMin, iLineCount, iColumnWidth, Console.WindowHeight - iLineCount, iMin, 0);

					int iCurrentLine = 0;
					foreach (string sLine in vLines)
					{
						foreach (string sSplitLine in sLine.SplitByLength(iColumnWidth))
						{
							Console.SetCursorPosition(iMin, Console.WindowHeight - iLineCount + iCurrentLine);
							Console.Write(sSplitLine);
							++iCurrentLine;
						}
					}
					//Console.Out.

					//lock (s_oLogLocker)
					{
						switch (eLevel)
						{
							case Core.ELogLevel.Debug:
								Console.BackgroundColor = ConsoleColor.Black;
								Console.ForegroundColor = ConsoleColor.DarkGray;
								break;
							case Core.ELogLevel.Normal:
								Console.BackgroundColor = ConsoleColor.Black;
								Console.ForegroundColor = ConsoleColor.White;
								break;
							case Core.ELogLevel.Warning:
								Console.BackgroundColor = ConsoleColor.Black;
								Console.ForegroundColor = ConsoleColor.DarkYellow;
								break;
							case Core.ELogLevel.Error:
								Console.BackgroundColor = ConsoleColor.Red;
								Console.ForegroundColor = ConsoleColor.Black;
								break;
						}
						//Console.Write("\n{0} {1} : {2}", DateTime.Now.ToString(), eLevel.ToString().PadRight(8), sMsg);
					}
				}
			};*/
#else
			oConfiguration.OnLog = (eLevel, sMsg) =>
			{
				Console.Write("\n{0} {1} : {2}", DateTime.Now.ToString(), eLevel.ToString().PadRight(8), sMsg);
			};
#endif
			Core.JobManager.Init(oConfiguration);

			while (true)
			{
				ConsoleKeyInfo oInfo = Console.ReadKey();
				if (oInfo.Key == ConsoleKey.Q)
				{
					break;
				}
			}
			Core.JobManager.Current.Stop();
		}
	}
}
