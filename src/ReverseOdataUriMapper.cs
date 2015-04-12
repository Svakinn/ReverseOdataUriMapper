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
            var fp = pairs.FirstOrDefault(cc => cc.FldFrom == name);
            return fp == null ? name : fp.FldTo;
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
        /// <param name="cc"></param>
        /// <param name="pairs"></param>
        /// <returns></returns>
        public static string ParseOdataOrderByToString(OrderByClause cc, List<FieldMapper> pairs)
        {
            var ret = "";
            var descStr = "";
            if (cc.Direction == OrderByDirection.Descending)
                descStr = " desc";
            var expr = cc.Expression as SingleValuePropertyAccessNode;
            if (expr != null)
            {
                var colName = expr.Property.Name;
                var fldName = ReplaceName(colName, pairs);
                if (cc.ThenBy != null)
                    ret = fldName + descStr + "," + ParseOdataOrderByToString(cc.ThenBy, pairs);
                else
                    ret = fldName + descStr;
            }
            return ret;
        }

        /// <summary>
        /// Parse the filter part of OdataQueryOptions
        /// See the call in the MapToOdataUri-function for usage
        /// </summary>
        /// <param name="cc"></param>
        /// <param name="pairs"></param>
        /// <returns></returns>
        public static string ParseOdataFilterToString(Object cc, List<FieldMapper> pairs)
        {
            var ret = "";
            var expr = cc as BinaryOperatorNode;
            if (expr != null)
            {
                var andOr = expr.OperatorKind == BinaryOperatorKind.And || expr.OperatorKind == BinaryOperatorKind.Or;
                if (expr.Left != null)
                {
                    ret += (andOr ? "(" : "") + ParseOdataFilterToString(expr.Left, pairs) + (andOr ? ")" : "");
                }
                ret += " " + BinOpKindToStr(expr.OperatorKind) + " ";
                if (expr.Right != null)
                {
                    ret += (andOr ? "(" : "") + ParseOdataFilterToString(expr.Right, pairs) + (andOr ? ")" : "");
                }
            }
            else
            {
                var op = cc as ConvertNode;
                if (op != null)
                {
                    if (op.Source != null)
                        ret += "(" + ParseOdataFilterToString(op.Source, pairs) + ")";

                }
                else
                {
                    var fCall = cc as SingleValueFunctionCallNode;
                    if (fCall != null)
                    {
                        ret += fCall.Name + "(";
                        if (fCall.Arguments != null && fCall.Arguments.Any())
                        {
                            var cntv = 0;
                            foreach (var vno in fCall.Arguments)
                            {
                                if (cntv > 0)
                                    ret += ",";
                                cntv++;
                                ret += ParseOdataFilterToString(vno, pairs);
                            }
                            ret += ")";
                        }
                    }
                    else
                    {
                        var cVal = cc as ConstantNode;
                        if (cVal != null)
                        {
                            ret += cVal.LiteralText;
                        }
                        else
                        {
                            var spName = cc as SingleValuePropertyAccessNode;
                            if (spName != null)
                            {
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
        /// <param name="allowSkip"></param>
        /// <param name="allowTop"></param>
        /// <param name="allowCount"></param>
        /// <returns></returns>
        public static string MapToOdataUri(ODataQueryOptions<T> options, List<FieldMapper> pairs, bool allowSkip = true, bool allowTop = true, bool allowCount = true)
        {
            var ret = "?";
            if (options.Filter != null && options.Filter.FilterClause.Expression != null)
                ret += "$filter=" + ParseOdataFilterToString(options.Filter.FilterClause.Expression, pairs) + "&";
            if (options.OrderBy != null && options.OrderBy.OrderByClause.Expression != null)
                ret += "$orderby=" + ParseOdataOrderByToString(options.OrderBy.OrderByClause, pairs) + "&";
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


