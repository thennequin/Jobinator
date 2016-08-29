using System;
using System.Net.Sockets;

namespace Jobinator.Core
{
	public static class NetworkStreamExtension
	{
		//Int
		public static void WriteInt(this NetworkStream oStream, int iValue)
		{
			byte[] vByte = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(iValue));
			oStream.Write(vByte, 0, vByte.Length);
		}

		public static int ReadInt(this NetworkStream oStream)
		{
			int iBufferSize = sizeof(int);
			byte[] vBuffer = new byte[iBufferSize];
			oStream.Read(vBuffer, 0, iBufferSize);
			return System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(vBuffer, 0));
		}

		public static void SendInt(this Socket oSocket, int iValue)
		{
			byte[] vByte = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(iValue));
			oSocket.Send(vByte, vByte.Length, SocketFlags.None);
		}

		public static int ReceiveInt(this Socket oSocket)
		{
			int iBufferSize = sizeof(int);
			byte[] vBuffer = new byte[iBufferSize];
			oSocket.Receive(vBuffer, iBufferSize, SocketFlags.None);
			return System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(vBuffer, 0));
		}

		public static void SendByte(this Socket oSocket, byte iValue)
		{
			byte[] vByte = new byte[] { iValue };
			oSocket.Send(vByte, vByte.Length, SocketFlags.None);
		}

		public static byte ReceiveByte(this Socket oSocket)
		{
			byte[] vBuffer = new byte[1];
			oSocket.Receive(vBuffer, 1, SocketFlags.None);
			return vBuffer[0];
		}
	}
}
