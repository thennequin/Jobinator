using System;
using System.Linq.Expressions;
using System.Linq;
using ServiceStack.OrmLite;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using ServiceStack;

namespace Jobinator.Core
{
	public class JobManager
	{
		static public JobManager Current { get; private set; }
		public bool IsServer { get; private set; }
		public bool IsAgent { get; private set; }

		bool m_bShouldStop;
		Action<ELogLevel, string> m_oActionLog { get; set; }

		//Server
		OrmLiteConnectionFactory m_oDbFactory;
		MessageServer m_oMessageServer;
		Dictionary<string, string> m_mQueueForType;
		//Agent
		MessageClient m_oMessageClient;
		Thread m_oAgentThread;
		int m_iMaxThread;
		string[] m_vAcceptedQueue;
		Dictionary<Thread, Job> m_mCurrentJobs;
		string m_sDependenciesPath;

		public void Log(ELogLevel eLevel, string sMessage)
		{
			if (null != m_oActionLog)
				m_oActionLog.Invoke(eLevel, sMessage);
		}

		static public void LogStatic(ELogLevel eLevel, string sMessage)
		{
			if (null != Current)
			{
				Current.Log(eLevel, sMessage);
			}
		}

		public Job CurrengJob
		{
			get
			{
				if (m_mCurrentJobs.Keys.Contains(Thread.CurrentThread))
				{
					return m_mCurrentJobs[Thread.CurrentThread];
				}
				throw new Exception("No running job on this thread");
			}
		}

		class LockedConnection : IDisposable
		{
			static object s_oConnectionLocker = new object();

			public IDbConnection Connection { get; private set; }
			public LockedConnection(IDbConnection oConnection)
			{
				if (oConnection == null)
					throw new ArgumentException("oSession is null");
				Connection = oConnection;
				Monitor.Enter(s_oConnectionLocker);
				Connection.Open();
			}

			public void Dispose()
			{
				Connection.Close();
				Connection.Dispose();
				Connection = null;
				Monitor.Exit(s_oConnectionLocker);
			}
		}

		private JobManager()
		{

		}

		public static void Init(Configuration oConfiguration)
		{
			if (Current != null)
				throw new Exception("JobManager instance already exist");

			if (oConfiguration.MaxThread <= 0)
				throw new ArgumentException("MaxThread must be positive");

			JobManager oJobManager = new JobManager();
			oJobManager.IsServer = oConfiguration.Mode == Configuration.EMode.Server || oConfiguration.Mode == Configuration.EMode.Both;
			oJobManager.IsAgent = oConfiguration.Mode == Configuration.EMode.Agent || oConfiguration.Mode == Configuration.EMode.Both;
			oJobManager.m_oActionLog = oConfiguration.OnLog;

			//Server
			if (oJobManager.IsServer)
			{
				oJobManager.m_mQueueForType = oConfiguration.m_mQueueForType;
				oJobManager.m_oDbFactory = new OrmLiteConnectionFactory(oConfiguration.ConnectionUrl, oConfiguration.Provider);

				IDbConnection oConnection = oJobManager.m_oDbFactory.CreateDbConnection();
				oConnection.Open();
#if DEBUG
				oConnection.DropAndCreateTable<Job>();
#else
				oConnection.CreateTableIfNotExists<Job>();
#endif
				oConnection.Close();

				oJobManager.m_oMessageServer = new MessageServer();
				oJobManager.m_oMessageServer.Start(oConfiguration.MainServerPort);
				oJobManager.m_oMessageServer.OnMessage = oJobManager.OnServerMessage;
			}

			//Agent
			if (oJobManager.IsAgent)
			{
				//Scan dependencies folder
				oJobManager.m_sDependenciesPath = System.IO.Path.GetFullPath(oConfiguration.DependenciesFolder);

				if (!System.IO.Directory.Exists(oJobManager.m_sDependenciesPath))
				{
					System.IO.Directory.CreateDirectory(oJobManager.m_sDependenciesPath);
				}
				else
				{
					if (oConfiguration.CleanDependencies)
					foreach (string sFile in System.IO.Directory.GetFiles(oJobManager.m_sDependenciesPath))
					{
						System.IO.File.Delete(sFile);
					}
				}
				/*else
				{
					foreach (string sFile in System.IO.Directory.GetFiles(oJobManager.m_sDependenciesPath))
					{
						try
						{
							string sFullPath = System.IO.Path.GetFullPath(sFile);
							System.Reflection.Assembly.LoadFile(sFullPath);
							oJobManager.Log(ELogLevel.Normal, "Dependency loaded: " + System.IO.Path.GetFileName(sFile));
						}
						catch (Exception e)
						{
							oJobManager.Log(ELogLevel.Error, "Dependency loading error: " + System.IO.Path.GetFileName(sFile) + " => " + e.Message);
						}
					}
				}*/

				AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(oJobManager.CustomResolveEventHandler);

				oJobManager.m_iMaxThread = oConfiguration.MaxThread;
				oJobManager.m_vAcceptedQueue = oConfiguration.AcceptedQueue != null ? oConfiguration.AcceptedQueue.Where(q => !string.IsNullOrWhiteSpace(q)).ToArray() : null;
				oJobManager.m_mCurrentJobs = new Dictionary<Thread, Job>();
				oJobManager.m_oMessageClient = new MessageClient();
				oJobManager.m_oMessageClient.Start(oConfiguration.MainServer, oConfiguration.MainServerPort);
				oJobManager.m_oAgentThread = new Thread(oJobManager.AgentThread);
				oJobManager.m_oAgentThread.Start();
			}

			Current = oJobManager;
		}

		private System.Reflection.Assembly CustomResolveEventHandler(object sender, ResolveEventArgs args)
		{
			Log(ELogLevel.Debug, "Resolving " + args.Name);
			string sAssemblyPath = System.IO.Path.Combine(m_sDependenciesPath, new System.Reflection.AssemblyName(args.Name).Name + ".dll");
			if (!System.IO.File.Exists(sAssemblyPath))
				return null;
			System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(sAssemblyPath);
			return assembly;
		}

		public void Stop()
		{
			m_bShouldStop = true;
			if (IsAgent)
			{
				m_oMessageClient.Stop();
				m_oAgentThread.Join();
			}
			if (IsServer)
			{
				m_oMessageServer.Stop();
			}
		}

		LockedConnection OpenLockedSession()
		{
			return new LockedConnection(m_oDbFactory.CreateDbConnection());
		}

		IDbConnection OpenDBConnection()
		{
			IDbConnection oConnection = m_oDbFactory.CreateDbConnection();
			oConnection.Open();
			return oConnection;
		}

		Job AddJob(LambdaExpression oExpression, Job oParentJob, Job oAfterJob, bool bContinueWith, string sQueue)
		{
			Job oJob = Job.CreateJob(oExpression);
			oJob.Queue = sQueue;

			if (IsServer)
			{
				long? iParentJobId = oParentJob != null ? oParentJob.Id : (long?)null;
				long? iAfterJobId = oAfterJob != null ? oAfterJob.Id : (long?)null;
				using (IDbConnection oSession = OpenDBConnection())
				using (IDbTransaction oTransaction = oSession.OpenTransaction())
				{
					//oJob.ParentJob = oParentJob;
					//oJob.AfterJob = oAfterJob;
					oJob.ParentJobId = iParentJobId;
					oJob.AfterJobId = iAfterJobId;
					BeforeAddJob(oJob);
					oJob.Id = oSession.Insert(oJob, selectIdentity: true);
					long iJobId = oJob.Id;
					oTransaction.Commit();
					return oJob;
				}
			}
			else
			{
				long? iParentJobId = oParentJob != null ? oParentJob.Id : CurrengJob.Id;
				long? iAfterJobId = oAfterJob != null ? oAfterJob.Id : CurrengJob.Id;
				if (bContinueWith)
				{
					//oJob.AfterJob = oAfterJob != null ? oAfterJob : CurrengJob;
					oJob.AfterJobId = iAfterJobId;
				}
				else
				{
					//oJob.ParentJob = oParentJob != null ? oParentJob : CurrengJob;
					oJob.ParentJobId = iParentJobId;
				}
				//oJob.AfterJobId = (oJob.AfterJob != null) ? oJob.AfterJob.Id : (long?)null;
				//oJob.ParentJobId = (oJob.ParentJob != null) ? oJob.ParentJob.Id : (long?)null;
				Log(ELogLevel.Debug, "Adding job message...");
				Message oResponse = m_oMessageClient.SendMessage(new Message(new MessageCommand(MessageCommand.ECommand.AddJob, oJob)));
				MessageCommand oResponseCommand = oResponse.ToObject<MessageCommand>();
				
				if (oResponseCommand.Command == MessageCommand.ECommand.JobAdded)
				{
					Log(ELogLevel.Debug, "Job added");
					return oResponseCommand.ToObject<Job>();
				}
				else
				{
					throw new Exception("Server return error: " + oResponseCommand.Data);
				}
			}
			throw new Exception("Can't create job");
		}

		Job GetJobById(long iId)
		{
			return null;
		}

		public Job Enqueue(Expression<Action> oExpression, Job oParent = null, string sQueue = "default")
		{
			return AddJob(oExpression as LambdaExpression, null, null, false, sQueue);
		}
		
		public Job Enqueue<T>(Expression<Action<T>> oExpression, Job oParent = null, string sQueue = "default")
		{
			return AddJob(oExpression as LambdaExpression, null, null, false, sQueue);
		}

		public Job ContinueWith(Expression<Action> oExpression, Job oJob = null, string sQueue = "default")
		{
			return AddJob(oExpression as LambdaExpression, null, oJob, true, sQueue);
		}

		public Job ContinueWith<T>(Expression<Action<T>> oExpression, Job oJob = null, string sQueue = "default")
		{
			return AddJob(oExpression as LambdaExpression, null, oJob, true, sQueue);
		}

		public bool IsJobFinish(long iJobId)
		{
			using (IDbConnection oConnection = OpenDBConnection())
			{
				Job oJobData = oConnection.Single<Job>(iJobId);

				return oJobData.Status == EJobStatus.Done;
			}
		}

		void UpdateJobParent(Job oJob, IDbConnection oConnection)
		{
			if (oJob.ParentJobId != null)
			{
				Job oParentJob = oConnection.SingleById<Job>(oJob.ParentJobId);
				if (null != oParentJob 
					&& oParentJob.Status == EJobStatus.WaitingSubJob
					&& !oParentJob.IsWaitingSubTask(oConnection))
				{
					oParentJob.Status = EJobStatus.Done;
					oConnection.Update(oParentJob);
					UpdateJobParent(oParentJob, oConnection);
				}
			}
		}

		void OnJobFinish(JobDone oJobDone)
		{
			if (IsServer)
			{
				using (IDbConnection oConnection = OpenDBConnection())
				using (IDbTransaction oTransaction = oConnection.OpenTransaction())
				{
					Job oJobData = oConnection.SingleById<Job>(oJobDone.JobId);

					if (null != oJobData)
					{
						if (oJobData.Status != EJobStatus.Done)
						{
							bool bHasWaitingSubJob = oJobData.IsWaitingSubTask(oConnection);
							if (bHasWaitingSubJob)
							{
								oJobData.Status = EJobStatus.WaitingSubJob;
							}
							else
							{
								oJobData.Status = EJobStatus.Done;
							}
							oJobData.Returned = oJobDone.ReturnData;
							oConnection.Update(oJobData);
							UpdateJobParent(oJobData, oConnection);
							oTransaction.Commit();
						}
						else
						{
							Log(ELogLevel.Error, "Job already done: " + oJobDone.JobId);
						}
					}
					else
					{
						Log(ELogLevel.Error, "Job not exist: " + oJobDone.JobId);
					}
				}
			}
			else
			{
				throw new Exception("OnJobFinish can't be call by agent");
			}
		}

		void OnJobException(long iJobId, Exception e)
		{
			if (IsServer)
			{
				Log(ELogLevel.Warning, "Job {0} fail: {1}".Fmt(iJobId, e.Message));
				using (IDbConnection oSession = OpenDBConnection())
				using (IDbTransaction oTransaction = oSession.OpenTransaction())
				{
					Job oJobData = oSession.SingleById<Job>(iJobId);

					if (null != oJobData && oJobData.Status != EJobStatus.Done)
					{
						oJobData.Status = EJobStatus.Fail;
						oJobData.Exception = e.Message;
						oSession.Update(oJobData);
						UpdateJobParent(oJobData, oSession);
						oTransaction.Commit();
					}
					else
					{
						Log(ELogLevel.Error, "Job not exist: " + iJobId);
					}
				}
			}
			else
			{
				throw new Exception("OnJobException can't be call by agent");
			}
		}

		void BeforeAddJob(Job oJob)
		{
			if (m_mQueueForType.ContainsKey(oJob.CallerTypeName))
			{
				oJob.Queue = m_mQueueForType[oJob.CallerTypeName];
			}
		}

		Message OnServerMessage(Message oMessage)
		{
			//Log(ELogLevel.Debug, "OnMessage");
			if (oMessage.IsObject)
			{
				MessageCommand oCommand = oMessage.ToObject<MessageCommand>();
				if (oCommand != null)
				{
					//Log(ELogLevel.Debug, oCommand.Command.ToString() + " => " + oCommand.Data);
					if (oCommand.Command == MessageCommand.ECommand.AddJob)
					{
						Job oToAddJob = oCommand.ToObject<Job>();
						BeforeAddJob(oToAddJob);
						using (IDbConnection oSession = OpenDBConnection())
						using (IDbTransaction oTransaction = oSession.OpenTransaction())
						{
							oToAddJob.Id = oSession.Insert(oToAddJob, selectIdentity: true);
							oTransaction.Commit();

							return new Message(new MessageCommand(MessageCommand.ECommand.JobAdded, oToAddJob));
						}
					}
					else if (oCommand.Command == MessageCommand.ECommand.WaitJob)
					{
						string[] vAcceptedQueue = oCommand.ToObject<string[]>();
						Message oReturnMessage = null;
						using (LockedConnection oLockedConnection = OpenLockedSession())
						using (IDbTransaction oTransaction = oLockedConnection.Connection.OpenTransaction())
						{
							//TODO: find better way to self join
							IOrmLiteDialectProvider oDialect = oLockedConnection.Connection.GetDialectProvider();
							//SqlExpression<Job> oQuery = oLockedConnection.Connection
							/*
								 * {0} : TableName
								 * {1} : Alias A
								 * {2} : Alias B
								 * {3} : Job.Id
								 * {4} : Job.AfterJobId
								 * {5} : Job.Status
								 * {6} : EJobStatus.Done
								 * {7} : EJobStatus.Waiting
								 * {8} : Job.Queue
								 * {9} : AcceptedQueue list
								 * */
							
							string sAcceptedQueue = oDialect.GetQuotedValue("default");
							if (null != vAcceptedQueue && vAcceptedQueue.Length > 0)
							{
								sAcceptedQueue = "";
								foreach (string sQueue in vAcceptedQueue)
								{
									if (!string.IsNullOrEmpty(sAcceptedQueue))
										sAcceptedQueue += ",";
									sAcceptedQueue += oDialect.GetQuotedValue(sQueue);
								}
							}
							string sQuery = @"SELECT {1}.*
									FROM {0} AS {1}
									JOIN {0} {2} ON {1}.{5} = {7} AND (({1}.{4} IS NULL AND {1}.{3} = {2}.{3}) OR ({1}.{4} = {2}.{3} AND {2}.{5} = {6})) AND {1}.{8} IN ({9})
									ORDER BY {1}.{4} ASC, {1}.{3} ASC
									LIMIT 1
									".Fmt(
										oDialect.GetQuotedTableName(typeof(Job).Name),
										oDialect.GetQuotedName("jobA"),
										oDialect.GetQuotedName("jobB"),
										oDialect.GetQuotedColumnName<Job>(j => j.Id),
										oDialect.GetQuotedColumnName<Job>(j => j.AfterJobId),
										oDialect.GetQuotedColumnName<Job>(j => j.Status),
										oDialect.GetQuotedValue(EJobStatus.Done, typeof(EJobStatus)),
										oDialect.GetQuotedValue(EJobStatus.Waiting, typeof(EJobStatus)),
										oDialect.GetQuotedColumnName<Job>(j => j.Queue),
										sAcceptedQueue
									);
							Job oNextJob = oLockedConnection.Connection.Single<Job>(sQuery);
							/*List<Job> lNextJobs = oLockedConnection.Connection.Select<Job>(sQuery);
							if (null != vAcceptedQueue && vAcceptedQueue.Length > 0)
								lNextJobs = lNextJobs.Where(j => vAcceptedQueue.Contains(j.Queue as string)).ToList();

							Job oNextJob = lNextJobs.FirstOrDefault();
							*/
							/*if (oNextJob == null)
							{
								oNextJob = oLockedConnection.Connection.Select<Job>(j => j.Status == EJobStatus.Waiting && j.AfterJobId == null).FirstOrDefault();
							}*/
							if (oNextJob != null)
							{
								oNextJob.Status = EJobStatus.Executing;
								oLockedConnection.Connection.Update(oNextJob);
								oTransaction.Commit();
								oReturnMessage = new Message(new MessageCommand(MessageCommand.ECommand.NextJob, oNextJob));
							}
							else
							{
								oReturnMessage = new Message(new MessageCommand(MessageCommand.ECommand.NoWaitingJob));
							}
						}
						return oReturnMessage;
					}
					else if (oCommand.Command == MessageCommand.ECommand.JobDone)
					{
						OnJobFinish(oCommand.ToObject<JobDone>());
					}
					else if (oCommand.Command == MessageCommand.ECommand.JobException)
					{
						JobFail oJobFail = oCommand.ToObject<JobFail>();
						OnJobException(oJobFail.JobId, oJobFail.Exception);
					}
					else if (oCommand.Command == MessageCommand.ECommand.NeedAssembly)
					{
						string sAssemblyName = oCommand.ToObject<string>();
						System.Reflection.Assembly oAssembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.FullName == sAssemblyName);
						if (null != oAssembly)
						{
							try
							{
								string sFilename = System.IO.Path.GetFileName(oAssembly.Location);
								byte[] vBytes = System.IO.File.ReadAllBytes(oAssembly.Location);
								FileContent oResObj = new FileContent{ Filename = sFilename, Content = vBytes };
								return new Message(new MessageCommand(MessageCommand.ECommand.AssemblyFound, oResObj));
							}
							catch
							{
								return new Message(new MessageCommand(MessageCommand.ECommand.AssemblyError, "Assembly reading exception: " + sAssemblyName));
							}
						}
						else
						{
							return new Message(new MessageCommand(MessageCommand.ECommand.AssemblyError,"Assembly not found: " + sAssemblyName));
						}
					}
				}
			}

			return null;
		}

		//Agent
		void RunJob(Job oJob)
		{
			StartJob(oJob);
			try
			{
				object oReturn = oJob.Execute();
				string sReturn = ServiceStack.Text.TypeSerializer.SerializeToString(oReturn, Type.GetType(oJob.ReturnedTypeName));
				EndJob();
				Message oMessage = new Message(new MessageCommand(MessageCommand.ECommand.JobDone, new JobDone { JobId = oJob.Id, ReturnData = sReturn }));
				Message oResponse = m_oMessageClient.SendMessage(oMessage);
			}
			catch (Exception e)
			{
				EndJob();
				Exception oBaseException = e;
				while (oBaseException.InnerException != null)
					oBaseException = oBaseException.InnerException;
				JobFail oResObj = new JobFail{ JobId = oJob.Id, Exception = oBaseException };
				Message oMessage = new Message(new MessageCommand(MessageCommand.ECommand.JobException, oResObj));
				Message oResponse = m_oMessageClient.SendMessage(oMessage);
			}
		}

		void StartJob(Job oJob)
		{
			//lock (m_mCurrentJobs)
			{
				m_mCurrentJobs.Add(Thread.CurrentThread, oJob);
			}
			lock (oJob)
			{
				Monitor.Pulse(oJob);
			}
		}

		void EndJob()
		{
			lock (m_mCurrentJobs)
			{
				m_mCurrentJobs.Remove(Thread.CurrentThread);
				Monitor.Pulse(m_mCurrentJobs);
			}
		}

		void AgentThread()
		{
			while (!m_bShouldStop)
			{
				if (m_oMessageClient.IsConnected)
				{
					//Log(ELogLevel.Debug,"Send wait message");
					lock (m_mCurrentJobs)
					{
						if (m_mCurrentJobs.Count >= m_iMaxThread)
						{
							Monitor.Wait(m_mCurrentJobs);
						}
						Message oMessage = new Message(new MessageCommand(MessageCommand.ECommand.WaitJob, m_vAcceptedQueue ));
						Message oResponse = m_oMessageClient.SendMessage(oMessage);
						if (null != oResponse)
						{
							MessageCommand oCommand = oResponse.ToObject<MessageCommand>();
							if (oCommand != null)
							{
								if (oCommand.Command == MessageCommand.ECommand.NextJob)
								{
									Job oNextJob = oCommand.ToObject<Job>();
									if (oNextJob.Id == 0)
										throw new Exception("Invalid Job Id");

									lock(oNextJob)
									{
										Task.Run(() => RunJob(oNextJob));
										Monitor.Wait(oNextJob);
									}
								}
								else if (oCommand.Command == MessageCommand.ECommand.NoWaitingJob)
								{
									//Nothing to do
									Thread.Sleep(50);
								}
								else
								{
									Log(ELogLevel.Error, "Error " + oCommand.Command);
								}
							}
							else
							{
								Log(ELogLevel.Error, "Error no reponse command");
							}
						}
					}
				}
			}
		}

		public System.Reflection.Assembly LoadAssembly(string sAssemblyName)
		{
			lock(m_sDependenciesPath)
			{
				var vAssemblies = AppDomain.CurrentDomain.GetAssemblies();

				foreach (System.Reflection.Assembly oAssembly in vAssemblies)
				{
					if (oAssembly.FullName == sAssemblyName)
					{
						return oAssembly;
					}
				}

				if (JobManager.Current.IsServer)
					throw new Exception("Assembly not found on server");

				Message oMessage = new Message(new MessageCommand(MessageCommand.ECommand.NeedAssembly, sAssemblyName));
				Message oResponse = m_oMessageClient.SendMessage(oMessage);
				MessageCommand oCommand = oResponse.ToObject<MessageCommand>();
				if (oCommand != null)
				{
					if (oCommand.Command == MessageCommand.ECommand.AssemblyFound)
					{
						FileContent oResObj = oCommand.ToObject<FileContent>();
						if (null == oResObj || string.IsNullOrEmpty(oResObj.Filename) || null == oResObj.Content || oResObj.Content.Length == 0)
							throw new Exception("Bad command response for AssemblyFound");

						string sAssemblyPath = System.IO.Path.Combine(m_sDependenciesPath, oResObj.Filename);
						using (System.IO.FileStream oStream = System.IO.File.Create(sAssemblyPath))
						{
							oStream.Write(oResObj.Content, 0, oResObj.Content.Length);
						}
						System.Reflection.Assembly oAssembly = System.Reflection.Assembly.LoadFrom(sAssemblyPath);
						Log(ELogLevel.Normal, "Assembly loaded: " + oAssembly.GetName().Name);
						return oAssembly;
					}
					else
					{
						throw new Exception(oCommand.ToObject<string>());
					}
				}
				else
				{
					throw new Exception("Bad response From MessageClient");
				}
			}
		}
	}
}
