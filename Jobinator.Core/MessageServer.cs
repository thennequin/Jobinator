using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Jobinator.Core
{
	class MessageServer : IDisposable
	{
		private TcpListener m_oTcpListener;
		private Thread m_oThread;
		private List<Thread> m_lClientThread;
		private volatile bool m_bShouldStop = false;

		public delegate Message OnMessageDelegate(Message oMessage);
		public OnMessageDelegate OnMessage { get; set; }

		public MessageServer()
		{
			m_lClientThread = new List<Thread>();
		}

		public void Start(int iPort)
		{
			m_oTcpListener = new TcpListener(IPAddress.Any, iPort);
			m_oThread = new Thread(Run);
			m_oThread.Start();
		}

		public void Stop()
		{
			if (null != m_oThread)
			{
				m_bShouldStop = true;
			}
		}

		public void Wait()
		{
			if (null != m_oThread)
			{
				m_oThread.Join();
			}
		}

		public void Dispose()
		{
			Stop();
			Wait();
		}

		void Run()
		{
			m_oTcpListener.Start();
			while (!m_bShouldStop)
			{
				TcpClient oClient = m_oTcpListener.AcceptTcpClient();
				if (oClient != null)
				{
					Thread oClientThread = new Thread(new ParameterizedThreadStart(RunClient));
					oClientThread.Start(oClient);
				}
			}
			foreach (Thread oClientThread in m_lClientThread)
			{
				oClientThread.Join();
			}
			m_oTcpListener.Stop();
		}

		void RunClient(object oClient)
		{
			JobManager.LogStatic(ELogLevel.Debug, "New agent");
			TcpClient oTcpClient = (TcpClient)oClient;
			Socket oSocket = oTcpClient.Client;
			while (!m_bShouldStop)
			{
				try
				{
					Message oMessage = new Message();
					oMessage.Receive(oSocket);
					Message oResponse = null;
					if (OnMessage != null)
						oResponse = OnMessage(oMessage);
					if (oResponse == null)
						oResponse = new Message();
					oResponse.Send(oSocket);
				}
				catch(Exception e)
				{
					if (!oTcpClient.Connected)
						break;
					else
						JobManager.LogStatic(ELogLevel.Error, "Unhandled exception: " + e.Message);
				}
			}
			JobManager.LogStatic(ELogLevel.Debug, "Agent disconnected");
			oTcpClient.Close();
		}
	}
}
