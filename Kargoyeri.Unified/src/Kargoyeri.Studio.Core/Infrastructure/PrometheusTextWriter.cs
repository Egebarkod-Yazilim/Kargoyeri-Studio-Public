using System.Globalization;
using System.Text;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// P2-#4 — Prometheus text exposition format (v0.0.4) icin kucuk yardimci.
/// NuGet bagimliligi olmadan minimal counter/gauge yazimi saglar.
/// Spec: https://github.com/prometheus/docs/blob/main/content/docs/instrumenting/exposition_formats.md
/// </summary>
public sealed class PrometheusTextWriter
{
    private readonly StringBuilder _sb = new();
    private readonly HashSet<string> _emittedHelp = new(StringComparer.Ordinal);

    public void Gauge(string name, string help, double value, IReadOnlyList<(string key, string value)>? labels = null)
        => Write(name, help, "gauge", value, labels);

    public void Counter(string name, string help, double value, IReadOnlyList<(string key, string value)>? labels = null)
        => Write(name, help, "counter", value, labels);

    private void Write(string name, string help, string type, double value,
                       IReadOnlyList<(string key, string value)>? labels)
    {
        if (_emittedHelp.Add(name))
        {
            _sb.Append("# HELP ").Append(name).Append(' ').AppendLine(EscapeHelp(help));
            _sb.Append("# TYPE ").Append(name).Append(' ').AppendLine(type);
        }

        _sb.Append(name);
        if (labels is { Count: > 0 })
        {
            _sb.Append('{');
            for (var i = 0; i < labels.Count; i++)
            {
                if (i > 0) _sb.Append(',');
                _sb.Append(labels[i].key).Append("=\"").Append(EscapeLabel(labels[i].value)).Append('"');
            }
            _sb.Append('}');
        }
        _sb.Append(' ').AppendLine(value.ToString("0.######", CultureInfo.InvariantCulture));
    }

    public string Build() => _sb.ToString();

    private static string EscapeHelp(string s) => s.Replace(@"\", @"\\").Replace("\n", @"\n");

    private static string EscapeLabel(string s)
        => s.Replace(@"\", @"\\").Replace("\"", "\\\"").Replace("\n", @"\n");
}
