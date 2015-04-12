using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.OData.Query;
using Microsoft.Data.OData.Query;
using Microsoft.Data.OData.Query.SemanticAst;

namespace WebApiBase
{
    /// <summary>
    /// For mapping odata replacement from one column to another
    /// Example usage:
    ///  var pairs = new List<FieldMapper>();
    ///  pairs.Add(new FieldMapper("MyFromColName","MyToColName");
    ///  pairs.add(new FieldMapper("MyFromColName2","toLower(MyToColName2");
    /// </summary>
    public class FieldMapper
    {
        public FieldMapper(string fromFld, string toFld)
        {
            this.FldFrom = fromFld;
            this.FldTo = toFld;
        }
        public string FldFrom { get; set; }
        public string FldTo { get; set; }
    }


    public static class ReverseOdataUriMapper<T>
    {
        /// <summary>
        /// Find the replacement name of column (if any)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pairs"></param>
        /// <returns></returns>
        private static string ReplaceName(string name, IEnumerable<FieldMapper> pairs)
        {
            if (pairs == null)
                return name;
            var fp = pairs.FirstOrDefault(cc => cc.FldFrom == name);
            return fp == null ? name : fp.FldTo;
        }

        private static bool IsDelete(string name, IEnumerable<string> deletes)
        {
            if (deletes == null)
                return false;
            return deletes.Contains(name);
        }

        /// <summary>
        /// Map the BineryOperatorKind enum to string
        /// </summary>
        /// <param name="kind"></param>
        /// <returns></returns>
        private static string BinOpKindToStr(BinaryOperatorKind kind)
        {
            if (kind == BinaryOperatorKind.And) return "and";
            if (kind == BinaryOperatorKind.Or) return "or";
            if (kind == BinaryOperatorKind.Equal) return "eq";
            if (kind == BinaryOperatorKind.NotEqual) return "ne";
            if (kind == BinaryOperatorKind.GreaterThan) return "gt";
            if (kind == BinaryOperatorKind.GreaterThanOrEqual) return "ge";
            if (kind == BinaryOperatorKind.LessThan) return "lt";
            if (kind == BinaryOperatorKind.LessThanOrEqual) return "le";
            if (kind == BinaryOperatorKind.Add) return "add";
            if (kind == BinaryOperatorKind.Subtract) return "sub";
            if (kind == BinaryOperatorKind.Multiply) return "mul";
            if (kind == BinaryOperatorKind.Divide) return "div";
            if (kind == BinaryOperatorKind.Modulo) return "mod";
            return kind.ToString();
        }

        /// <summary>
        /// Parse the Odata-OrderBy part of the query
        /// See the call in the MapToOdataUri-function for usage
        /// </summary>
        /// <param name="cc">object node to parse</param>
        /// <param name="pairs">List of replacement column names</param>
        /// <param name="deleteList">List of column names to omit from the output</param>
        /// <returns></returns>
        public static string ParseOdataOrderByToString(OrderByClause cc, List<FieldMapper> pairs = null, List<string> deleteList = null)
        {
            var ret = "";
            var descStr = "";
            if (cc.Direction == OrderByDirection.Descending)
                descStr = " desc";
            var expr = cc.Expression as SingleValuePropertyAccessNode;
            if (expr != null)
            {
                var colName = expr.Property.Name;
                if (!IsDelete(colName, deleteList))
                {
                    ret = ReplaceName(colName, pairs) + descStr;
                }
                if (cc.ThenBy != null)
                {
                    var subRet = ParseOdataOrderByToString(cc.ThenBy, pairs, deleteList);
                    if (!string.IsNullOrWhiteSpace(subRet))
                        ret += "," + subRet;
                }
            }
            return ret;
        }

        public static string ParseOdataFilterToString(Object cc, List<FieldMapper> pairs, List<string> deleteList)
        {
            var tmp = "";
            var ret = FilterRecurse(cc, pairs, deleteList, ref tmp);
            return string.IsNullOrWhiteSpace(tmp) ? ret : ""; //Only if this note is not to be deleted
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cc"></param>
        /// <param name="pairs"></param>
        /// <param name="deleteList"></param>
        /// <param name="deletedName">determines if current node contains deleted name and should be skipped from the output</param>
        /// <returns></returns>
        private static string FilterRecurse(Object cc, List<FieldMapper> pairs, List<string> deleteList, ref string deletedName)
        {
            var ret = "";
            var expr = cc as BinaryOperatorNode;
            if (expr != null)
            {
                if (string.IsNullOrWhiteSpace(deletedName))
                {
                    var andOr = expr.OperatorKind == BinaryOperatorKind.And || expr.OperatorKind == BinaryOperatorKind.Or;
                    var leftStr = "";
                    var rightStr = "";
                    if (expr.Left != null)
                    {
                        var nowStr = FilterRecurse(expr.Left, pairs, deleteList, ref deletedName);
                        if (!string.IsNullOrWhiteSpace(deletedName))
                        {
                            if (andOr)
                                deletedName = ""; //We can now continue with parsing other nodes 
                        }
                        else if (!string.IsNullOrWhiteSpace(nowStr))
                            leftStr = (andOr ? "(" : "") + nowStr + (andOr ? ")" : "");
                    }
                    if (expr.Right != null && string.IsNullOrWhiteSpace(deletedName))
                    {
                        var nowStr = FilterRecurse(expr.Right, pairs, deleteList, ref deletedName);
                        if (!string.IsNullOrWhiteSpace(deletedName))
                        {
                            if (andOr)
                                deletedName = ""; //We can now continue with parsing other nodes 
                        }
                        else if (!string.IsNullOrWhiteSpace(nowStr))
                            rightStr = (andOr ? "(" : "") + nowStr + (andOr ? ")" : "");
                    }
                    if (!string.IsNullOrWhiteSpace(leftStr) && !string.IsNullOrWhiteSpace(rightStr))
                        ret += leftStr + " " + BinOpKindToStr(expr.OperatorKind) + " " + rightStr;
                    else
                        ret += leftStr + rightStr;
                }
            }
            else
            {
                var op = cc as ConvertNode;
                if (op != null)
                {
                    if (op.Source != null && string.IsNullOrWhiteSpace(deletedName))
                    {
                        var nowStr = FilterRecurse(op.Source, pairs, deleteList, ref deletedName);
                        if (string.IsNullOrWhiteSpace(deletedName) && !string.IsNullOrWhiteSpace(nowStr))
                            ret += "(" + nowStr + ")";
                    }
                }
                else
                {
                    var fCall = cc as SingleValueFunctionCallNode;
                    if (fCall != null)
                    {
                        if (string.IsNullOrWhiteSpace(deletedName))
                        {
                            var retNow = fCall.Name + "(";
                            if (fCall.Arguments != null && fCall.Arguments.Any())
                            {
                                var cntv = 0;
                                foreach (var vno in fCall.Arguments)
                                {
                                    if (cntv > 0)
                                        retNow += ",";
                                    cntv++;
                                    if (string.IsNullOrWhiteSpace(deletedName))
                                        retNow += FilterRecurse(vno, pairs, deleteList, ref deletedName);
                                }
                                retNow += ")";
                            }
                            if (string.IsNullOrWhiteSpace(deletedName))
                                ret += retNow;
                        }
                    }
                    else
                    {
                        var cVal = cc as ConstantNode;
                        if (cVal != null)
                        {
                            if (string.IsNullOrWhiteSpace(deletedName))
                                ret += cVal.LiteralText;
                        }
                        else
                        {
                            var spName = cc as SingleValuePropertyAccessNode;
                            if (spName != null)
                            {
                                if (IsDelete(spName.Property.Name, deleteList))
                                    deletedName = spName.Property.Name; //Report that this section is not to be used
                                else
                                    ret += ReplaceName(spName.Property.Name, pairs);
                            }
                            else
                            {
                                //Following node types I have not encountered in any odata Query samples I threw at this class
                                //Implemntation for this thus remains unfinished
                                var feCall = cc as SingleEntityFunctionCallNode;
                                if (feCall != null)
                                {
                                    ret += "Todo: SingleEntityFunctionCallNode" + cc;
                                }
                                else
                                {
                                    var cast = cc as SingleEntityCastNode;
                                    if (cast != null)
                                    {
                                        ret += "Todo: SingleEntityCastNode" + cc;
                                    }
                                    else
                                    {
                                        var single = cc as SingleEntityNode;
                                        if (single != null)
                                        {
                                            ret += "Todo: SingleEntityNode " + cc;
                                        }
                                        else
                                        {
                                            var fVal = cc as SingleValueNode;
                                            if (fVal != null)
                                            {
                                                ret += "Todo: SingleValueNode " + cc;
                                            }
                                            else
                                            {
                                                ret += "Todo: Unknown " + cc;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Return the ODATA part of query-string parsed from OdataQueryOptions where column names in the pairs list have been replaced
        /// Note: this is only tested for Odata V3
        /// Example usage:
        /// var newUri = ReverseOdataUriMapper<Customer>(options,new List<FieldMapper>() {new FieldMapper("CustCode", "No")});
        /// </summary>
        /// <param name="options">The ODataQueryOptions for any entytymodel entity.</param>
        /// <param name="pairs">List of from-to column names to replace</param>
        /// <param name="deleteList">List of column names we want to delete from the query</param>
        /// <param name="allowSkip"></param>
        /// <param name="allowTop"></param>
        /// <param name="allowCount"></param>
        /// <returns></returns>
        public static string MapToOdataUri(ODataQueryOptions<T> options, List<FieldMapper> pairs = null, List<string> deleteList = null, bool allowSkip = true, bool allowTop = true, bool allowCount = true)
        {
            var ret = "?";
            if (options.Filter != null && options.Filter.FilterClause.Expression != null)
            {
                var filt = ParseOdataFilterToString(options.Filter.FilterClause.Expression, pairs, deleteList);
                if (!string.IsNullOrWhiteSpace(filt))
                    ret += "$filter=" + filt + "&";
            }
            if (options.OrderBy != null && options.OrderBy.OrderByClause.Expression != null)
            {
                var ord = ParseOdataOrderByToString(options.OrderBy.OrderByClause, pairs, deleteList);
                if (!string.IsNullOrWhiteSpace(ord))
                    ret += "$orderby=" + ord + "&";
            }
            if (allowSkip && options.Skip != null)
                ret += "$skip=" + Convert.ToString(options.Skip.Value) + "&";
            if (allowTop && options.Top != null)
                ret += "$top=" + Convert.ToString(options.Top.Value) + "&";
            if (allowCount && options.InlineCount != null)
                ret += "$inlinecount=" + options.InlineCount.RawValue + "&";
            return ret;
        }
    }
}


