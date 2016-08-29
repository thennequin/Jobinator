using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ServiceStack.Text;

namespace Jobinator.Core
{
	static class Reflection
	{
		public static void GetAssembliesForType(HashSet<string> vOutReferencePath, Type oType)
		{
			vOutReferencePath.Add(oType.Assembly.Location);
			GetAssemblyDependencies(vOutReferencePath, oType.Assembly);
		}

		public static void GetAssemblyDependencies(HashSet<string> vOutReferencePath, System.Reflection.Assembly oAssembly)
		{
			System.Reflection.AssemblyName[] vRefs = oAssembly.GetReferencedAssemblies();
			if (null != vRefs)
			{
				foreach (System.Reflection.AssemblyName oRef in vRefs)
				{
					System.Reflection.Assembly oRefAssembly = System.Reflection.Assembly.Load(oRef);
					if (null != oRefAssembly && !vOutReferencePath.Contains(oRefAssembly.Location))
					{
						if (!oRefAssembly.GlobalAssemblyCache)
						{
							vOutReferencePath.Add(oRefAssembly.FullName);
							//vOutReferencePath.Add(oRefAssembly.Location);
							GetAssemblyDependencies(vOutReferencePath, oRefAssembly);
						}
					}
				}
			}
		}

		public static ExpressionArgument GetArgument(Expression oExpression, string sField = null)
		{
			while (oExpression.CanReduce)
				oExpression = oExpression.Reduce();
			if (oExpression.NodeType == System.Linq.Expressions.ExpressionType.Constant)
			{
				ConstantExpression oConstantExpression = oExpression as ConstantExpression;
				if (null != oConstantExpression.Value)
				{
					if (null == sField)
					{
						ExpressionArgument oArg = new ExpressionArgument();
						oArg.sArg = TypeSerializer.SerializeToString(oConstantExpression.Value);
						oArg.sType = oConstantExpression.Type.FullName;
						return oArg;
					}
					else
					{
						Type oType = oConstantExpression.Value.GetType();
						System.Reflection.FieldInfo oFieldInfo = oType.GetField(sField);
						object oFieldValue = oFieldInfo.GetValue(oConstantExpression.Value);
						if (null != oFieldValue)
						{
							ExpressionArgument oArg = new ExpressionArgument();
							oArg.sArg = TypeSerializer.SerializeToString(oFieldValue);
							oArg.sType = null;
							return oArg;
						}
					}
				}
			}
			else if (oExpression.NodeType == System.Linq.Expressions.ExpressionType.MemberAccess)
			{
				MemberExpression oMemberExpression = oExpression as MemberExpression;
				ExpressionArgument oArg = GetArgument(oMemberExpression.Expression, oMemberExpression.Member.Name);
				oArg.sType = oMemberExpression.Type.FullName;
				return oArg;
			}
			else
			{
				throw new Exception("Not supported expression: " + oExpression.NodeType);
			}
			return new ExpressionArgument();
		}

		public static ExpressionArgument[] GetArguments(MethodCallExpression oCallExpression)
		{
			ExpressionArgument[] vArgs = new ExpressionArgument[oCallExpression.Arguments.Count];
			int iArg = 0;
			foreach (dynamic arg in oCallExpression.Arguments)
			{
				vArgs[iArg++] = GetArgument(arg);
			}
			return vArgs;
		}
	}
}
