// SimpleJSON.cs (public domain / MIT-like). Minimal version.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SimpleJSON
{
    public enum JSONNodeType { Array, Object, String, Number, Null, Boolean, None }

    public abstract class JSONNode : IEnumerable<JSONNode>
    {
        public virtual JSONNodeType Tag => JSONNodeType.None;
        public virtual JSONNode this[int aIndex] { get => null; set { } }
        public virtual JSONNode this[string aKey] { get => null; set { } }
        public virtual string Value { get => ""; set { } }
        public virtual int Count => 0;

        public virtual IEnumerable<string> Keys { get { yield break; } }
        public virtual IEnumerable<JSONNode> Values { get { yield break; } }

        public virtual double AsDouble
        {
            get
            {
                if (double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
                return 0.0;
            }
            set => Value = value.ToString(CultureInfo.InvariantCulture);
        }
        public virtual float AsFloat { get => (float)AsDouble; set => AsDouble = value; }
        public virtual int AsInt { get => (int)AsDouble; set => AsDouble = value; }
        public virtual bool AsBool
        {
            get
            {
                if (bool.TryParse(Value, out var v)) return v;
                return !string.IsNullOrEmpty(Value);
            }
            set => Value = value ? "true" : "false";
        }

        public virtual JSONArray AsArray => this as JSONArray;
        public virtual JSONObject AsObject => this as JSONObject;

        public static JSONNode Parse(string aJSON)
        {
            using (var sr = new StringReader(aJSON))
                return Parse(sr);
        }

        public static JSONNode Parse(TextReader aReader)
        {
            var stack = new Stack<JSONNode>();
            JSONNode ctx = null;
            var sb = new StringBuilder();
            string tokenName = "";
            bool quoteMode = false;
            bool tokenIsQuoted = false;

            while (aReader.Peek() != -1)
            {
                char c = (char)aReader.Read();

                if (c == '"' && !quoteMode)
                {
                    quoteMode = true;
                    tokenIsQuoted = true;
                    continue;
                }
                if (c == '"' && quoteMode)
                {
                    quoteMode = false;
                    continue;
                }

                if (quoteMode)
                {
                    sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '{':
                        stack.Push(new JSONObject());
                        if (ctx != null)
                        {
                            if (ctx is JSONArray) ctx.AsArray.Add(stack.Peek());
                            else if (tokenName != "") ctx[tokenName] = stack.Peek();
                        }
                        tokenName = "";
                        sb.Length = 0;
                        ctx = stack.Peek();
                        tokenIsQuoted = false;
                        break;

                    case '[':
                        stack.Push(new JSONArray());
                        if (ctx != null)
                        {
                            if (ctx is JSONArray) ctx.AsArray.Add(stack.Peek());
                            else if (tokenName != "") ctx[tokenName] = stack.Peek();
                        }
                        tokenName = "";
                        sb.Length = 0;
                        ctx = stack.Peek();
                        tokenIsQuoted = false;
                        break;

                    case '}':
                    case ']':
                        if (sb.Length > 0 || tokenIsQuoted)
                        {
                            var val = sb.ToString().Trim();
                            if (ctx is JSONArray) ctx.AsArray.Add(ParseElement(val, tokenIsQuoted));
                            else if (tokenName != "") ctx[tokenName] = ParseElement(val, tokenIsQuoted);
                        }
                        sb.Length = 0;
                        tokenName = "";
                        tokenIsQuoted = false;

                        stack.Pop();
                        ctx = stack.Count > 0 ? stack.Peek() : null;
                        break;

                    case ':':
                        tokenName = sb.ToString().Trim();
                        sb.Length = 0;
                        tokenIsQuoted = false;
                        break;

                    case ',':
                        if (sb.Length > 0 || tokenIsQuoted)
                        {
                            var val = sb.ToString().Trim();
                            if (ctx is JSONArray) ctx.AsArray.Add(ParseElement(val, tokenIsQuoted));
                            else if (tokenName != "") ctx[tokenName] = ParseElement(val, tokenIsQuoted);
                        }
                        sb.Length = 0;
                        tokenName = "";
                        tokenIsQuoted = false;
                        break;

                    default:
                        if (!char.IsWhiteSpace(c)) sb.Append(c);
                        break;
                }
            }

            return ctx;
        }

        private static JSONNode ParseElement(string token, bool quoted)
        {
            if (quoted) return new JSONString(token);

            if (token.Equals("null", StringComparison.OrdinalIgnoreCase)) return new JSONNull();
            if (token.Equals("true", StringComparison.OrdinalIgnoreCase)) return new JSONBool(true);
            if (token.Equals("false", StringComparison.OrdinalIgnoreCase)) return new JSONBool(false);

            if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return new JSONNumber(v);

            return new JSONString(token);
        }

        public IEnumerator<JSONNode> GetEnumerator() { foreach (var n in Values) yield return n; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class JSONArray : JSONNode
    {
        private readonly List<JSONNode> m_List = new List<JSONNode>();
        public override JSONNodeType Tag => JSONNodeType.Array;
        public override int Count => m_List.Count;
        public override JSONNode this[int aIndex] { get => aIndex < 0 || aIndex >= m_List.Count ? null : m_List[aIndex]; set { if (aIndex >= 0 && aIndex < m_List.Count) m_List[aIndex] = value; } }
        public override IEnumerable<JSONNode> Values { get { foreach (var n in m_List) yield return n; } }
        public void Add(JSONNode item) => m_List.Add(item);
    }

    public class JSONObject : JSONNode
    {
        private readonly Dictionary<string, JSONNode> m_Dict = new Dictionary<string, JSONNode>();
        public override JSONNodeType Tag => JSONNodeType.Object;
        public override int Count => m_Dict.Count;
        public override JSONNode this[string aKey] { get => m_Dict.TryGetValue(aKey, out var v) ? v : null; set => m_Dict[aKey] = value; }
        public override IEnumerable<string> Keys { get { foreach (var k in m_Dict.Keys) yield return k; } }
        public override IEnumerable<JSONNode> Values { get { foreach (var v in m_Dict.Values) yield return v; } }
    }

    public class JSONString : JSONNode
    {
        private string m_Data;
        public JSONString(string aData) => m_Data = aData;
        public override JSONNodeType Tag => JSONNodeType.String;
        public override string Value { get => m_Data; set => m_Data = value; }
    }

    public class JSONNumber : JSONNode
    {
        private double m_Data;
        public JSONNumber(double aData) => m_Data = aData;
        public override JSONNodeType Tag => JSONNodeType.Number;
        public override string Value { get => m_Data.ToString(CultureInfo.InvariantCulture); set { if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) m_Data = v; } }
        public override double AsDouble { get => m_Data; set => m_Data = value; }
    }

    public class JSONBool : JSONNode
    {
        private bool m_Data;
        public JSONBool(bool aData) => m_Data = aData;
        public override JSONNodeType Tag => JSONNodeType.Boolean;
        public override string Value { get => m_Data ? "true" : "false"; set { if (bool.TryParse(value, out var v)) m_Data = v; } }
        public override bool AsBool { get => m_Data; set => m_Data = value; }
    }

    public class JSONNull : JSONNode
    {
        public override JSONNodeType Tag => JSONNodeType.Null;
        public override string Value { get => "null"; set { } }
    }
}
