// See https://aka.ms/new-console-template for more information
using System.Text.RegularExpressions;

Console.WriteLine("Hello, World!");

var files = Directory.EnumerateFiles("D:\\Repos\\TIPSWEB5\\quality", "*.cs", SearchOption.AllDirectories);
var matchRegex = new Regex("/// <inheritdoc cref=\"(.*)\" />");
var inhRegex = new Regex("/// <inheritdoc />");
var runRegex = new Regex("protected override (.*) Run\\(");
var propertyRegex = new Regex("public (.*) (.*) \\{ get; set; \\}");
var classNameRagex = new Regex("(.*) class (.*)");
var bsRegex = new Regex("/// <summary>(.*)</summary>");

foreach (var file in files)
{
    if (file.Contains("Activity", StringComparison.OrdinalIgnoreCase) ||
        file.Contains("Workflow", StringComparison.OrdinalIgnoreCase) ||
        file.Contains("Facade", StringComparison.OrdinalIgnoreCase))
    {
        var lines = await File.ReadAllLinesAsync(file);
        var originalLines = lines.ToList();

        for (int i = 0; i < lines.Length; ++i)
        {
            var line = lines[i];
            var match = runRegex.Match(line);
            if (match.Success && bsRegex.IsMatch(lines[i - 1]))
            {
                lines[i - 1] = "        /// <inheritdoc />";
            }
        }

        if (originalLines.Zip(lines).Any(pair => pair.First != pair.Second))
        {
            Console.WriteLine(file);
            await File.WriteAllTextAsync(file, string.Join(Environment.NewLine, lines));
        }
    }
}

//await TrasnformFiles(files, matchRegex, inhRegex, propertyRegex, classNameRagex);

string DecamelCase(string input)
{
    return Regex.Replace(input.Trim(' ', '\t', '{', '}'), @"(\B[A-Z]+?(?=[A-Z][^A-Z])|\B[A-Z]+?(?=[^A-Z]))", " $1");
}

string ReplaceText(string content, string value, string elementName)
{
    if (elementName.EndsWith("in", StringComparison.OrdinalIgnoreCase))
    {
        return content.Replace(value, $"/// <summary>The {DecamelCase(elementName.Remove(elementName.Length - 2))} input.</summary>");
    }
    else if (elementName.EndsWith("out", StringComparison.OrdinalIgnoreCase))
    {
        return content.Replace(value, $"/// <summary>The {DecamelCase(elementName.Remove(elementName.Length - 3))} output.</summary>");
    }
    else
    {
        return content.Replace(value, $"/// <summary>The {DecamelCase(elementName)}.</summary>");
    }
}

static string GetLineLooped(string[] lines, int index)
{
    return lines[index % lines.Length];
}

static void GetPropertyType(Regex propertyRegex, string[] lines, int index, out TokenType tokenType, out string nextLine)
{
    for (int x = 1; x < lines.Length && x < 20; ++x)
    {
        nextLine = GetLineLooped(lines, index + x);
        if (nextLine.Contains("override"))
        {
            tokenType = TokenType.Override;
            return;
        }

        if (nextLine.Contains("class"))
        {
            tokenType = TokenType.Class;
            return;
        }

        if (propertyRegex.IsMatch(nextLine))
        {
            tokenType = TokenType.Property;
            return;
        }
    }

    nextLine = GetLineLooped(lines, index + 1);
    tokenType = TokenType.Unknown;
}

async Task TrasnformFiles(IEnumerable<string> files, Regex matchRegex, Regex inhRegex, Regex propertyRegex, Regex classNameRagex)
{
    foreach (var file in files)
    {
        Console.WriteLine(file);
        if (file.Contains("OpenCertConfMeasGroupController"))
        {
            Console.WriteLine();
        }


        var lines = await File.ReadAllLinesAsync(file);
        var originalLines = lines.ToList();
        for (int i = 0; i < lines.Length; i++)
        {
            var content = lines[i];

            var match = matchRegex.Match(content);
            if (match.Success)
            {
                var value = match.Groups[1].Value;
                if (Path.GetFileNameWithoutExtension(file) == value)
                {
                    content = ReplaceText(content, match.Value, value);
                }
            }

            match = inhRegex.Match(content);
            if (match.Success)
            {
                GetPropertyType(propertyRegex, lines, i, out var token, out var nextLine);

                switch (token)
                {
                    case TokenType.Override:
                        break;

                    case TokenType.Property:
                        var propMatch = propertyRegex.Match(nextLine);
                        if (propMatch.Success)
                        {
                            var typeName = propMatch.Groups[1].Value;
                            var valueName = propMatch.Groups[2].Value;

                            if (typeName == "bool" || typeName == "bool?")
                            {
                                content = content.Replace(match.Value, $"/// <summary> Gets or sets Gets or sets a value indicating whether {DecamelCase(valueName)}. </summary>");
                            }
                            else
                            {
                                content = content.Replace(match.Value, $"/// <summary> Gets or sets the {DecamelCase(valueName)}. </summary>");
                            }
                        }
                        break;

                    case TokenType.Class:
                        var classMatch = classNameRagex.Match(nextLine);
                        if (classMatch.Success)
                        {
                            var className = classMatch.Groups[2].Value.Split(':')[0].Trim();
                            content = content.Replace(match.Value, $"/// <summary>{DecamelCase(className)}</summary>");
                        }
                        break;

                    case TokenType.Unknown:
                    default:
                        content = content.Replace(match.Value, $"/// <summary> TODO: Fix comment </summary>");
                        break;
                }
            }

            lines[i] = content;
        }

        if (originalLines.Zip(lines).Any(pair => pair.First != pair.Second))
        {
            await File.WriteAllLinesAsync(file, lines);

            var co = await File.ReadAllTextAsync(file);
            var ca = co.Trim();
            if (co != ca)
            {
                await File.WriteAllTextAsync(file, ca);
            }
        }
    }
}