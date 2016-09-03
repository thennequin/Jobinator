using System;
using System.Linq;
using System.Linq.Expressions;
using ServiceStack;
using ServiceStack.Text;
using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;

namespace Jobinator.Core
{
	public enum EJobStatus
	{
		Waiting,
		Executing,
		WaitingSubJob,
		Done,
		Fail
	}

	public class Job
	{
		[PrimaryKey, AutoIncrement]
		public long Id { get; set; }

		[References(typeof(Job))]
		public long? AfterJobId { get; set; }

		[References(typeof(Job))]
		public long? ParentJobId { get; set; }

		public EJobStatus Status { get; set; }

		public string CallerAssembly { get; set; }
		public string CallerTypeName { get; set; }
		public string CallerMethod { get; set; }
		public string ReturnedTypeName { get; set; }
		public ExpressionArgument[] Args { get; set; }

		public string[] AssemblyDependencies { get; set; }

		public string Queue { get; set; }

		public string Exception { get; set; }
		public string Returned { get; set; }
		public long ProgressCurrent { get; set; }
		public long ProgressMax { get; set; }

		public object Execute()
		{
			if (null != AssemblyDependencies)
			{
				foreach (string sDependency in AssemblyDependencies)
				{
					JobManager.Current.LoadAssembly(sDependency);
				}
			}
			
			System.Reflection.Assembly oCallerAssembly = JobManager.Current.LoadAssembly(CallerAssembly);
			
			Type oCallerType = oCallerAssembly.GetType(CallerTypeName);
			if (null == oCallerType)
				throw new System.Exception("callerType '" + CallerTypeName + "' not found, wrong Assembly?");

			Type[] vArgsType = new Type[0];
			if (Args != null)
			{
				vArgsType = Args.Select(a => a.sType != null ? Type.GetType(a.sType) : null).ToArray();
			}

			System.Reflection.MethodInfo oMethodInfo = oCallerType.GetMethod(CallerMethod, vArgsType);
			if (null == oMethodInfo)
				throw new System.Exception("Method not found, wrong Assembly?");

			object oCallerInstance = null;
			if (!oMethodInfo.IsStatic)
			{
				oCallerInstance = Activator.CreateInstance(oCallerType);
			}
			object[] vArgsObj = new object[Args != null ? Args.Length : 0];
			int iArg = 0;
			if (Args != null)
			{
				foreach (ExpressionArgument oArg in Args)
				{
					if (oArg.sArg != null && oArg.sType != null)
						vArgsObj[iArg++] = TypeSerializer.DeserializeFromString(oArg.sArg, Type.GetType(oArg.sType));
				}
			}
			//JobProgress.Start();
			return oMethodInfo.Invoke(oCallerInstance, vArgsObj);
			//JobProgress.Stop();
		}

		public static Job CreateJob(LambdaExpression oExpression)
		{
			if (null == oExpression) throw new ArgumentException("oExpression is null");

			if (oExpression.Body.NodeType == ExpressionType.Call)
			{
				MethodCallExpression oCallExpression = oExpression.Body as MethodCallExpression;

				Job oJob = new Job();
				oJob.CallerAssembly = oCallExpression.Method.ReflectedType.Assembly.FullName;
				oJob.CallerTypeName = oCallExpression.Method.ReflectedType.FullName;
				oJob.CallerMethod = oCallExpression.Method.Name;
				oJob.ReturnedTypeName = oCallExpression.Method.ReturnType.FullName;
				oJob.Args = Reflection.GetArguments(oCallExpression);
				System.Collections.Generic.HashSet<string> lReferences = new System.Collections.Generic.HashSet<string>();
				Reflection.GetAssemblyDependencies(lReferences, oCallExpression.Method.ReflectedType.Assembly);
				oJob.AssemblyDependencies = lReferences.ToArray();
				//oJob.AssemblyDependencies = oCallExpression.Method.ReflectedType.Assembly.GetReferencedAssemblies().Select(a => a.FullName).ToArray();
				return oJob;
			}
			else
			{
				throw new Exception("Not supported expression");
			}
		}

		public bool IsWaitingSubTask(System.Data.IDbConnection oConnection)
		{
			return oConnection.Count<Job>(j => j.ParentJobId == Id && j.Status != EJobStatus.Done) > 0;
		}
	}
}
