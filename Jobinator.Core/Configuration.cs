using ServiceStack.OrmLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jobinator.Core
{
	public enum ELogLevel
	{
		Debug,
		Normal,
		Warning,
		Error
	}

	public class Configuration
	{
		public enum EMode
		{
			Server,
			Agent,
			Both
		}

		public string ConnectionUrl { get; set; }
		public IOrmLiteDialectProvider Provider { get; set; }
		public EMode Mode { get; set; }

		//For Agent
		public string MainServer { get; set; } = "localhost";
		public int MaxThread { get; set; } = 0;
		public string[] AcceptedQueue { get; set; }
		public string DependenciesFolder { get; set; } = "dependencies";
		public bool CleanDependencies { get; set; } = true;

		//For Server & Agent
		public ushort MainServerPort { get; set; } = 56246;
		public Action<ELogLevel, string> OnLog { get; set; }

		//For Server
		internal Dictionary<string, string> m_mQueueForType = new Dictionary<string, string>();
		public void AddQueueForType(Type oType, string sQueue)
		{
			if (string.IsNullOrWhiteSpace(sQueue))
				throw new Exception("sQueue is empty");
			if (m_mQueueForType.ContainsKey(oType.FullName))
				throw new Exception("Type already has setted queue");
			m_mQueueForType.Add(oType.FullName, sQueue);
		}
		

		static object s_oLogLocker = new object();
		public Configuration()
		{
			MaxThread = Environment.ProcessorCount;
			OnLog = (eLevel, sMsg) =>
			{
				lock (s_oLogLocker)
				{
					switch (eLevel)
					{
						case ELogLevel.Debug:
							Console.BackgroundColor = ConsoleColor.Black;
							Console.ForegroundColor = ConsoleColor.DarkGray;
							break;
						case ELogLevel.Normal:
							Console.BackgroundColor = ConsoleColor.Black;
							Console.ForegroundColor = ConsoleColor.White;
							break;
						case ELogLevel.Warning:
							Console.BackgroundColor = ConsoleColor.Black;
							Console.ForegroundColor = ConsoleColor.DarkYellow;
							break;
						case ELogLevel.Error:
							Console.BackgroundColor = ConsoleColor.Red;
							Console.ForegroundColor = ConsoleColor.Black;
							break;
					}
					Console.Write("\n{0} {1} : {2}", DateTime.Now.ToString(), eLevel.ToString().PadRight(8), sMsg);
				}
			};
		}
	}
}
