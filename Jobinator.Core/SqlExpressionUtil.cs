using ServiceStack;
using ServiceStack.OrmLite;
using System;
using System.Linq.Expressions;

namespace Jobinator.Core
{
	static public class SqlExpressionUtil
	{
		internal class SelfJoinSqlExpression<T> : SqlExpression<T>
		{
			public SelfJoinSqlExpression(IOrmLiteDialectProvider dialectProvider) : base(dialectProvider)
			{
			}

			internal static string Visit(Expression oExpr, SqlExpression<T> oOriginalExpression)
			{
				SelfJoinSqlExpression<T> oNexExpr = new SelfJoinSqlExpression<T>(oOriginalExpression.DialectProvider);
				object oValue = oNexExpr.Visit(oExpr);
				//return oValue;
				return "";
			}
		}
		public static SqlExpression<T> SelfJoin<T>(this SqlExpression<T> oExpression, Expression<Func<T, object>> oKeySelectorA, Expression<Func<T, object>> oKeySelectorB, Expression<Func<T,bool>> oPredicateB)
		{
			string[] sFieldnamesA = oKeySelectorA.GetFieldNames();
			string[] sFieldnamesB = oKeySelectorB.GetFieldNames();
			if (sFieldnamesA == null || sFieldnamesA.Length != 1)
				throw new Exception("Invalid field for oKeySelectorA");
			if (sFieldnamesB == null || sFieldnamesB.Length != 1)
				throw new Exception("Invalid field for sFieldnamesB");

			string sTableName = oExpression.DialectProvider.GetQuotedTableName(typeof(T).Name);
			string sAlias = oExpression.DialectProvider.GetQuotedTableName("TempJoinTable");
			string sKeyA = oExpression.DialectProvider.GetQuotedColumnName(sFieldnamesA[0]);
			string sKeyB = oExpression.DialectProvider.GetQuotedColumnName(sFieldnamesB[0]);
			string sPredicateB = ""/*oPredicateB.SqlValue()*/;
			//VisitJoin(oPredicateB).ToString())
			//var temp = oExpression.Where(oPredicateB);
			sPredicateB = SelfJoinSqlExpression<T>.Visit(oPredicateB, oExpression);

			//return oExpression.CustomJoin("CROSS JOIN {0} {1} ON {2} = {1}.{3} AND {4}".Fmt(sTableName, sAlias, sKeyA, sKeyB, sPredicateB));
			return oExpression.CustomJoin("LEFT JOIN {0} {1} ON {2} = {1}.{3}".Fmt(sTableName, sAlias, sKeyA, sKeyB, sPredicateB));
		}

		public static string GetQuotedColumnName<T>(this IOrmLiteDialectProvider oDialect, Expression<Func<T, object>> oKeySelector)
		{
			string[] sFieldnames = oKeySelector.GetFieldNames();
			if (sFieldnames == null || sFieldnames.Length != 1)
				throw new Exception("Invalid field for oKeySelectorA");
			return oDialect.GetQuotedColumnName(sFieldnames[0]);
		}
	}
}
