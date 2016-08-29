using System;
using System.Net.Sockets;
using ServiceStack.Text;

namespace Jobinator.Core
{
	public class Message
	{
		enum EType
		{
			Unknown,
			Data, //Byte[]
			String,
			Object, //String to object
		}
		int m_iSize = 0;
		EType m_eType;
		byte[] m_vData;

		public bool IsData
		{
			get
			{
				return m_eType == EType.Data;
			}
		}

		public bool IsObject
		{
			get
			{
				return m_eType == EType.Object;
			}
		}

		public bool IsString
		{
			get
			{
				return m_eType == EType.String;
			}
		}

		public byte[] Data
		{
			get
			{
				if (IsData)
				{
					return m_vData;
				}
				return null;
			}
		}

		public object Object
		{
			get
			{
				if (IsObject)
				{
					char[] vChars = new char[m_iSize / sizeof(char)];
					System.Buffer.BlockCopy(m_vData, 0, vChars, 0, m_vData.Length);
					string sJson = new string(vChars);
					return TypeSerializer.DeserializeFromString<dynamic>(sJson);
				}
				return null;
			}
		}

		public string String
		{
			get
			{
				if (IsString && m_iSize > 0)
				{
					//return m_vData;
					char[] vChars = new char[m_iSize / sizeof(char)];
					System.Buffer.BlockCopy(m_vData, 0, vChars, 0, m_vData.Length);
					return new string(vChars);
				}
				return null;
			}
		}

		public Message()
		{
			m_eType = EType.Unknown;
		}

		public Message(string sMessage)
		{
			m_eType = EType.String;
			Write(sMessage);
		}

		public Message(byte[] vData)
		{
			m_eType = EType.Data;
			Write(vData);
		}

		public Message(object oObject)
		{
			m_eType = EType.Object;
			Write(oObject);
		}

		internal void Send(NetworkStream oStream)
		{
			oStream.WriteInt(m_iSize);
			oStream.WriteByte((byte)m_eType);
			if (m_iSize > 0)
				oStream.Write(m_vData, 0, m_iSize);
			//oBinaryWriter.Flush();
			oStream.Flush();
		}

		internal void Receive(NetworkStream oStream)
		{
			m_iSize = oStream.ReadInt();
			m_eType = (EType)oStream.ReadByte();
			if (m_iSize > 0)
			{
				m_vData = new byte[m_iSize];
				int iRead = 0;
				do
				{
					iRead += oStream.Read(m_vData, iRead, m_iSize - iRead);
				}
				while (iRead < m_iSize) ;
			}
		}

		internal void Send(Socket oSocket)
		{
			oSocket.SendInt(m_iSize);
			oSocket.SendByte((byte)m_eType);
			if (m_iSize > 0)
				oSocket.Send(m_vData, m_iSize, SocketFlags.None);
		}

		internal void Receive(Socket oSocket)
		{
			m_iSize = oSocket.ReceiveInt();
			m_eType = (EType)oSocket.ReceiveByte();
			if (m_iSize > 0)
			{
				m_vData = new byte[m_iSize];
				byte[] vTemp = new byte[m_iSize];
				int iTotalReceived = 0;
				do
				{
					int iOffset = iTotalReceived;
					int iReceived = oSocket.Receive(vTemp, m_iSize - iTotalReceived, SocketFlags.None);
					System.Buffer.BlockCopy(vTemp, 0, m_vData, iOffset, iReceived);
					iTotalReceived += iReceived;
				}
				while (iTotalReceived < m_iSize);
			}
		}

		void WriteData(byte[] vData)
		{
			if (vData == null)
				throw new ArgumentException("vData is null");
			if (m_vData == null)
			{
				m_vData = new byte[vData.Length];
				m_iSize = vData.Length;
				System.Buffer.BlockCopy(vData, 0, m_vData, 0, vData.Length);
			}
			else
			{
				byte[] vNewData = new byte[vData.Length + m_iSize];
				System.Buffer.BlockCopy(m_vData, 0, vNewData, 0, m_iSize);
				System.Buffer.BlockCopy(vData, 0, vNewData, m_iSize, vData.Length);
				m_vData = vNewData;
			}
		}

		public void Write(string sValue)
		{
			int iSize = sValue != null ? (sValue.Length * sizeof(char)) : 0;
			if (iSize > 0)
			{
				//byte[] vData = new byte[iSize];
				//System.Buffer.BlockCopy(sValue.ToCharArray(), 0, vData, 0, vData.Length);
				//WriteData(vData);
				WriteData(System.Text.Encoding.UTF8.GetBytes(sValue));
			}
		}

		public void Write(object oValue)
		{
			string sJson = TypeSerializer.SerializeToString(oValue);
			Write(sJson);
		}

		public T ToObject<T>()
		{
			if (IsObject)
			{
				/*char[] vChars = new char[m_iSize / sizeof(char)];
				System.Buffer.BlockCopy(m_vData, 0, vChars, 0, m_vData.Length);
				string sJson = new string(vChars);*/
				string sJson = System.Text.Encoding.UTF8.GetString(m_vData);
				return TypeSerializer.DeserializeFromString<T>(sJson);
			}
			return default(T);
		}
	}
}
