using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using ServiceStack;

namespace Jobinator.Core
{
	class MessageClient
	{
		TcpClient m_oTcpClient;
		private Thread m_oThread;
		private volatile bool m_bShouldStop = false;
		string m_sLastHost;
		ushort m_iLastPort;

		public bool IsConnected
		{
			get
			{
				return (null != m_oTcpClient && m_oTcpClient.Connected);
			}
		}

		class WaitingMessage
		{
			public Message Message { get; private set; }
			public Message Response { get; private set; }

			public WaitingMessage(Message oMessage)
			{
				Message = oMessage;
			}
			public Message WaitResponse()
			{
				lock (this)
				{
					System.Threading.Monitor.Wait(this);
				}
				return Response;
			}
			public void SetResponse(Message oResponse)
			{
				Response = oResponse;
				lock (this)
				{
					System.Threading.Monitor.Pulse(this);
				}
			}
		}
		Queue<WaitingMessage> m_lNextMessages = new Queue<WaitingMessage>();

		public MessageClient()
		{

		}

		public bool Start(string sHost, ushort iPort)
		{
			m_oTcpClient = new TcpClient();
			m_sLastHost = sHost;
			m_iLastPort = iPort;
			bool bReturn = false;
			try
			{
				m_oTcpClient.Connect(m_sLastHost, m_iLastPort);
				bReturn = true;
			}
			catch
			{
			}
			m_oThread = new Thread(Run);
			m_oThread.Start();
			return bReturn;
		}

		public void Stop()
		{
			m_bShouldStop = true;
			if (null != m_oThread)
			{
				m_oThread.Join();
				m_oThread = null;
			}
		}

		void Run()
		{
			while (!m_bShouldStop)
			{
				if (!IsConnected)
				{
					try
					{
						m_oTcpClient = new TcpClient(m_sLastHost, m_iLastPort);
					}
					catch
					{
						JobManager.LogStatic(ELogLevel.Warning, "Can't connect to host '{0}' on port {1}, next try in 2 seconds".Fmt(m_sLastHost, m_iLastPort));
						Thread.Sleep(2000);
					}
				}
				else
				{
					Socket oSocket = m_oTcpClient.Client;
					
					lock (m_lNextMessages)
					{
						if (m_lNextMessages.Count == 0)
							System.Threading.Monitor.Wait(m_lNextMessages);
						while (m_lNextMessages.Count > 0 && IsConnected)
						{
							try
							{
								WaitingMessage oWaitingMessage = m_lNextMessages.Peek();

								oWaitingMessage.Message.Send(oSocket);
								Message oResponse = new Message();
								oResponse.Receive(oSocket);
								oWaitingMessage.SetResponse(oResponse);
								m_lNextMessages.Dequeue();
							}
							catch (Exception e)
							{
								JobManager.LogStatic(ELogLevel.Error, "Exception: " + e.Message);
								break;
							}
						}
					}
				}
			}
			m_oTcpClient.Close();
		}

		public Message SendMessage(Message oMessage)
		{
			if (null == oMessage)
				throw new ArgumentException("oMessage is null");
			if (null == m_oTcpClient || !m_oTcpClient.Connected)
				throw new Exception("Client not connected");

			WaitingMessage oWaitingMessage = new WaitingMessage(oMessage);
			lock (m_lNextMessages)
			{
				m_lNextMessages.Enqueue(oWaitingMessage);
				if (m_lNextMessages.Count == 1)
					System.Threading.Monitor.Pulse(m_lNextMessages);
			}
			return oWaitingMessage.WaitResponse();
		}
	}
}
