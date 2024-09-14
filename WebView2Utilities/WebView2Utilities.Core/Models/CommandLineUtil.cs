namespace WebView2Utilities.Core.Models.CommandLineUtil;

public class CommandLine
{
    public CommandLine(string commandLine)
    {
        m_parts = ParseCommandLine(commandLine);
    }

    public string GetKeyValue(string key)
    {
        return GetKeyValue(m_parts, key);
    }

    public string[] Parts => m_parts.ToArray();

    public override string ToString()
    {
        return string.Join(" ",
            Parts.Select(part => part.Contains(" ") ? '"' + part + '"' : part));
    }

    public bool Contains(string entry) => Parts.Contains(entry);

    public bool Add(string entry)
    {
        if (!m_parts.Contains(entry))
        {
            m_parts.Add(entry);
            return true;
        }
        return false;
    }

    public bool Remove(string entry) => m_parts.Remove(entry);

    private List<string> m_parts;

    private static string GetKeyValue(List<string> all, string key)
    {
        foreach (var entry in all)
        {
            if (entry.StartsWith(key + "="))
            {
                return entry.Substring(key.Length + 1);
            }
        }
        return null;
    }

    private static List<string> ParseCommandLine(string commandLine)
    {
        var parts = new List<string>();
        var inQuote = false;
        var part = "";

        if (commandLine == null)
        {
            commandLine = "";
        }

        for (var curIdx = 0; curIdx < commandLine.Length; ++curIdx)
        {
            var curChar = commandLine[curIdx];
            if (!inQuote && char.IsWhiteSpace(curChar))
            {
                if (part.Length > 0)
                {
                    parts.Add(part);
                    part = "";
                }
            }
            else if (!inQuote && curChar == '"')
            {
                inQuote = true;
            }
            else if (inQuote && curChar == '"')
            {
                inQuote = false;
            }
            else
            {
                part += curChar;
            }
        }

        if (part.Length > 0)
        {
            parts.Add(part);
        }

        return parts;
    }
}
