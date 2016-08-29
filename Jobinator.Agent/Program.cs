//Only for windows
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
#if ADVANCED_LOG
		[System.Runtime.InteropServices.DllImport("Kernel32.dll"), System.Security.SuppressUnmanagedCodeSecurity]
		public static extern int GetCurrentProcessorNumber();
#endif

		static void Main(string[] args)
		{
			Core.Configuration oConfiguration = new Core.Configuration();
			
			oConfiguration.Mode = Core.Configuration.EMode.Agent;
			oConfiguration.MainServer = Properties.Settings.Default.MainServer;
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
			List<int> vProcessorOrder = new List<int>();

			oConfiguration.OnLog = (eLevel, sMsg) =>
			{
				lock (vProcessorOrder)
				{
					int iProcessor = GetCurrentProcessorNumber();
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
				}
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
