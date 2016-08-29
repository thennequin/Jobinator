using System;
using ServiceStack;

namespace Jobinator.Sample
{
	class Program
	{
		public static void WaitPrint(string sMessage)
		{
			WaitPrint(sMessage, 2000);
		}
		public static void WaitPrint(string sMessage, int iSleepMs)
		{
			/*Console.WriteLine();
			Console.Write(sMessage);
			/*for (int i = 0; i< iSleepMs; i+=100)
			{
				Console.Write(".");
				Thread.Sleep(100);
			}*/
			Core.JobManager.LogStatic(Core.ELogLevel.Debug, sMessage);
			System.Threading.Thread.Sleep(iSleepMs);
		}

		public static void WaitRead(string sMessage)
		{
			Core.JobManager.LogStatic(Core.ELogLevel.Debug, sMessage);
			Console.ReadLine();
		}

		public static void Test1()
		{
			Core.JobManager.LogStatic(Core.ELogLevel.Debug, "Start test1");
			Core.Job oEndJob = Core.JobManager.Current.ContinueWith(() => Core.JobManager.LogStatic(Core.ELogLevel.Debug, "End test1 sub job"));

			Core.JobManager.Current.ContinueWith(() => WaitPrint("start Test2 after Test1"), oEndJob);
			Core.JobManager.Current.ContinueWith(() => WaitPrint("start Test3 after Test1"), oEndJob);
			Core.JobManager.Current.ContinueWith(() => WaitPrint("start Test4 after Test1"), oEndJob);

			Core.JobManager.Current.Enqueue(() => WaitPrint("Test1 sub task1"));
			Core.Job oSubJob = Core.JobManager.Current.Enqueue(() => WaitPrint("Test1 sub task2", 500));
			Core.JobManager.Current.ContinueWith(() => WaitPrint("Test1 sub task2 part 2"), oSubJob);
			Core.JobManager.Current.Enqueue(() => WaitPrint("Test1 sub task4"));
			Core.JobManager.Current.Enqueue(() => WaitPrint("Test1 sub task5"));
			System.Threading.Thread.Sleep(1000);

			//JobManager.Current.WaitSubJobs();
			Core.JobManager.LogStatic(Core.ELogLevel.Debug,"End test1");
		}

		public static void Test2()
		{
			Core.JobManager.LogStatic(Core.ELogLevel.Debug, "Start test2");
			Core.JobManager.Current.Enqueue(() => WaitRead("Test2 wait key"));
			Core.JobManager.Current.ContinueWith(() => WaitPrint("Test2 end"));
		}

		class MaClass
		{
			public void Toto()
			{
				Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, "Toto");
			}

			public void Test(string[] sValues)
			{
				foreach (string sValue in sValues)
					Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, sValue);
			}

			public void Exception()
			{
				throw new System.Exception("toto");
			}

			public void MassJob(int iCount)
			{
				Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, "Creating {0} job".Fmt(iCount));
				for (int i = 0; i < iCount; ++i)
				{
					string sLine = "=> Job {0}".Fmt(i);
					Core.JobManager.Current.Enqueue(() => Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, sLine));
				}
				Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, "Job created");
			}
		}

		class TestClass
		{
			public void Test(int i)
			{
				Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, "TestClass => test " + i);
			}
		}
		static void Main(string[] args)
		{
			Core.Configuration oConfiguration = new Core.Configuration();

			//oConfiguration.ConnectionUrl = @"Server=127.0.0.1;Port=5432;Database=Jobinator;User Id=jobinator;Password=jobinator;";
			//oConfiguration.Provider = ServiceStack.OrmLite.PostgreSqlDialect.Provider;
			oConfiguration.ConnectionUrl = "./Jobinator.sqlite";
			oConfiguration.Provider = ServiceStack.OrmLite.SqliteDialect.Provider;
			oConfiguration.Mode = Core.Configuration.EMode.Server;
			oConfiguration.MainServer = @"localhost";
			oConfiguration.MainServerPort = 56246;
			oConfiguration.AddQueueForType(typeof(TestClass), "test");

			Core.JobManager.Init(oConfiguration);

			/*for (int i = 0; i < 100; ++i)
				Core.JobManager.Current.Enqueue<TestClass>(t => t.Test(i));
			for (int i = 0; i < 100; ++i)
				Core.JobManager.Current.Enqueue<TestClass>(t => t.Test(i));
			for (int i = 0; i < 100; ++i)
				Core.JobManager.Current.Enqueue<TestClass>(t => t.Test(i));*/
			/*
			Core.JobManager.Current.Enqueue(() => Console.WriteLine("console"));

			string[] sValues = new string[] { "test", "test2" };
			Core.JobManager.Current.Enqueue<MaClass>(c => c.Test(sValues));

			Core.JobManager.Current.Enqueue<MaClass>(c => c.Toto());

			long iJobId = Core.JobManager.Current.Enqueue(() => Console.WriteLine("Hello "));
			Core.JobManager.Current.ContinueWith(iJobId, () => Console.WriteLine("World"));

			Core.JobManager.Current.IsJobFinish(iJobId);
			*/
			while (true)
			{
				ConsoleKeyInfo oInfo = Console.ReadKey();
				if (oInfo.Key == ConsoleKey.Q)
				{
					break;
				}
				else if (oInfo.Key == ConsoleKey.H)
				{
					Core.Job oHelloJob = Core.JobManager.Current.Enqueue(() => Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, "Hello "));
					Core.JobManager.Current.Enqueue(() => Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, " trololo "), oHelloJob);
					Core.JobManager.Current.ContinueWith(() => Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, "World"), oHelloJob);
				}
				else if (oInfo.Key == ConsoleKey.C)
				{
					Core.JobManager.Current.Enqueue(() => Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, "console"));
				}
				else if (oInfo.Key == ConsoleKey.V)
				{
					//Core.JobManager.Current.Enqueue(() => Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug,"console (only onqueue test)"), null, "test");
					Core.JobManager.Current.Enqueue<TestClass>(t => t.Test(0));
					for (int i = 0; i < 100; ++i)
						Core.JobManager.Current.Enqueue<TestClass>(t => t.Test(i));
				}
				else if (oInfo.Key == ConsoleKey.M)
				{
					string[] sValues = new string[] { "test", "test2" };
					Core.JobManager.Current.Enqueue<MaClass>(c => c.Test(sValues));

					Core.JobManager.Current.Enqueue<MaClass>(c => c.Toto());
				}
				else if (oInfo.Key == ConsoleKey.T)
				{
					Core.JobManager.Current.Enqueue(() => Test1());
				}
				else if (oInfo.Key == ConsoleKey.Y)
				{
					Core.JobManager.Current.Enqueue(() => Test2());
				}
				else if (oInfo.Key == ConsoleKey.L)
				{
					for (int i = 0; i < 100; ++i)
					{
						string sMsg = "Link " + i;
						Core.JobManager.Current.Enqueue(() => Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, sMsg));
					}
				}
				else if (oInfo.Key == ConsoleKey.K)
				{
					Core.JobManager.Current.Enqueue<MaClass>(t => t.MassJob(10000));
				}
				else if (oInfo.Key == ConsoleKey.J)
				{
					Core.JobManager.Current.Enqueue<MaClass>(t => t.MassJob(100));
				}
				else if (oInfo.Key == ConsoleKey.E)
				{
					//Core.JobManager.Current.Enqueue<MaClass>(c => c.Exception());
					Core.JobManager.Current.Enqueue(() => Jobinator.Core.JobManager.LogStatic(Core.ELogLevel.Debug, "test"));
				}
			}
			Core.JobManager.Current.Stop();
		}
	}
}
