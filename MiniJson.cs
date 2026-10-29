using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AstronomicalEvent
{
    // Tiny JSON reader for glTF headers (Newtonsoft can't be referenced from net472). Missing keys yield null.
    internal sealed class JNode : IEnumerable<JNode>
    {
        readonly object v;

        JNode(object value)
        {
            v = value;
        }

        public static JNode Parse(string s)
        {
            int p = 0;
            return new JNode(Read(s, ref p));
        }

        public JNode this[string key]
        {
            get
            {
                var d = v as Dictionary<string, object>;
                object o;
                return d != null && d.TryGetValue(key, out o) ? new JNode(o) : null;
            }
        }
        public JNode this[int i]
        {
            get
            {
                var l = v as List<object>;
                return l != null && i >= 0 && i < l.Count ? new JNode(l[i]) : null;
            }
        }
        public int Count
        {
            get
            {
                var l = v as List<object>;
                return l != null ? l.Count : 0;
            }
        }

        public static explicit operator int(JNode n)
        {
            return (int)Convert.ToDouble(n.v, CultureInfo.InvariantCulture);
        }

        public static explicit operator float(JNode n)
        {
            return (float)Convert.ToDouble(n.v, CultureInfo.InvariantCulture);
        }

        public static explicit operator bool(JNode n)
        {
            return n.v is bool && (bool)n.v;
        }

        public static explicit operator string(JNode n)
        {
            return n == null ? null : n.v as string;
        }

        public float[] Floats()
        {
            var l = (List<object>)v;
            var r = new float[l.Count];
            for (int i = 0; i < r.Length; i++)
                r[i] = (float)Convert.ToDouble(l[i], CultureInfo.InvariantCulture);
            return r;
        }

        public IEnumerator<JNode> GetEnumerator()
        {
            var l = v as List<object>;
            if (l != null)
                foreach (var o in l)
                    yield return new JNode(o);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        static void Ws(string s, ref int p)
        {
            while (p < s.Length && char.IsWhiteSpace(s[p]))
                p++;
        }

        static object Read(string s, ref int p)
        {
            Ws(s, ref p);
            char c = s[p];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                p++;
                Ws(s, ref p);
                if (s[p] == '}')
                {
                    p++;
                    return d;
                }
                while (true)
                {
                    Ws(s, ref p);
                    var k = (string)Read(s, ref p);
                    Ws(s, ref p);
                    p++; // ':'
                    d[k] = Read(s, ref p);
                    Ws(s, ref p);
                    if (s[p++] == '}')
                        return d;
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                p++;
                Ws(s, ref p);
                if (s[p] == ']')
                {
                    p++;
                    return l;
                }
                while (true)
                {
                    l.Add(Read(s, ref p));
                    Ws(s, ref p);
                    if (s[p++] == ']')
                        return l;
                }
            }
            if (c == '"')
            {
                var sb = new StringBuilder();
                p++;
                while (s[p] != '"')
                {
                    if (s[p] == '\\')
                    {
                        p++;
                        char e = s[p];
                        if (e == 'u')
                        {
                            sb.Append((char)Convert.ToInt32(s.Substring(p + 1, 4), 16));
                            p += 4;
                        }
                        else
                            sb.Append(
                                e == 'n' ? '\n'
                                : e == 't' ? '\t'
                                : e == 'r' ? '\r'
                                : e == 'b' ? '\b'
                                : e == 'f' ? '\f'
                                : e
                            );
                    }
                    else
                        sb.Append(s[p]);
                    p++;
                }
                p++;
                return sb.ToString();
            }
            if (string.CompareOrdinal(s, p, "true", 0, 4) == 0)
            {
                p += 4;
                return true;
            }
            if (string.CompareOrdinal(s, p, "false", 0, 5) == 0)
            {
                p += 5;
                return false;
            }
            if (string.CompareOrdinal(s, p, "null", 0, 4) == 0)
            {
                p += 4;
                return null;
            }
            int st = p;
            while (p < s.Length && "+-0123456789.eE".IndexOf(s[p]) >= 0)
                p++;
            return double.Parse(s.Substring(st, p - st), CultureInfo.InvariantCulture);
        }
    }
}
