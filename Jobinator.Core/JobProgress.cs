using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jobinator.Core
{
	public static class JobProgress
	{
		class Progress
		{
			//public long JobId;
			public long Current;
			public long Max;
		}

		static Dictionary<System.Threading.Thread, Progress> s_mProgress = new Dictionary<System.Threading.Thread, Progress>();

		static public void Start()
		{
			s_mProgress.Add(System.Threading.Thread.CurrentThread, new Progress() { Current = 0, Max = 0 });
		}

		static public void Stop()
		{
			s_mProgress.Remove(System.Threading.Thread.CurrentThread);
		}

		public static long Current
		{
			get
			{
				return s_mProgress[System.Threading.Thread.CurrentThread].Current;
			}
			set
			{
				s_mProgress[System.Threading.Thread.CurrentThread].Current = value;
			}
		}

		public static long Max
		{
			get
			{
				return s_mProgress[System.Threading.Thread.CurrentThread].Current;
			}
			set
			{
				s_mProgress[System.Threading.Thread.CurrentThread].Max = value;
			}
		}
	}
}
