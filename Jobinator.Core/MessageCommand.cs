using ServiceStack.Text;
namespace Jobinator.Core
{
	class FileContent
	{
		public string Filename { get; set; }
		public byte[] Content { get; set; }
	}

	class JobDone
	{
		public long JobId { get; set; }
		public string ReturnData { get; set; }
	}

	class JobFail
	{
		public long JobId { get; set; }
		public System.Exception Exception { get; set; }
	}

	class MessageCommand
	{
		public enum ECommand
		{
			Unknwon,

			WaitJob,
			AddJob,
			NoWaitingJob,
			NextJob,

			JobAdded,
			JobDone,
			JobException,

			NeedAssembly,
			AssemblyFound,
			AssemblyError,

			Error
		}

		public ECommand Command { get; set; }
		public string Data { get; set; }

		public MessageCommand()
		{
			Command = ECommand.Unknwon;
		}

		public MessageCommand(ECommand eCommand, object oValue = null)
		{
			Command = eCommand;
			if (oValue != null)
				Data = TypeSerializer.SerializeToString(oValue);
		}

		public T ToObject<T>()
		{
			return TypeSerializer.DeserializeFromString<T>(Data);
		}
	}
}
