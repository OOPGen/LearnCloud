using System.Text;

namespace LearnCloud.Infrastructure.Text;

/// <summary>
/// CSV for spreadsheets. Values are always quoted, and a value a spreadsheet would treat as a
/// formula (starting with =, +, -, @, tab or carriage return) is prefixed with an apostrophe,
/// so a name like =HYPERLINK(...) is shown as text instead of being executed (CSV injection).
/// </summary>
public static class CsvWriter
{
    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        var safe = value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

    public static string Row(params string?[] values) => string.Join(",", values.Select(Field));

    public static byte[] ToUtf8(IEnumerable<string> lines)
    {
        var text = new StringBuilder();
        foreach (var line in lines) text.Append(line).Append("\r\n");
        // Byte order mark so Excel opens UTF-8 names (e.g. accented characters) correctly.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }
}
