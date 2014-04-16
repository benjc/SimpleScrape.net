using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

class TagMatch
{
    public string name;
    public int start;
}

class Tag
{
    public string variableName;
    public string upToHTML;
}

public class simpleScrape
{
    private Dictionary<string, List<string>> values;
    private int sourceStartPos = 0;
    private string _script;
    private string[] _scriptLines;

    public string source = "";

    const string userAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.11 (KHTML, like Gecko) Chrome/23.0.1271.95 Safari/537.11";
    const string grabTag = "GRAB";
    const string repeatTag = "REPEAT";

    // GETTERS/ SETTERS
    public string sourceURL
    {
        set
        {
            source = urlGetContents(value);
        }
    }

    public string sourcePath
    {
        set
        {
            source = file_get_contents(value);
        }
    }

    public string scriptPath
    {
        set
        {
            _script = file_get_contents(value);
            _scriptLines = _script.Split('\n');
        }
    }

    public string script
    {
        set
        {
            _script = value;
            _scriptLines = _script.Split('\n');
        }
        get { return _script; }
    }

    // FILE_GET_CONTENTS
    // Reads in and returns the text in a given file
    private string file_get_contents(string filename)
    {
        string content = "";

        try
        {
            using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath(filename)))
            {
                content = sr.ReadToEnd();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("The file could not be read:");
            Console.WriteLine(e.Message);
        }

        return content;
    }

    // URLGETCONTENTS
    // Returns content of a given URL
    private string urlGetContents(string url)
    {
        StringBuilder sb = new StringBuilder();
        string response = "";

        byte[] buf = new byte[8192];

        HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
        webRequest.UserAgent = userAgent;

        HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();

        Stream resStream = webResponse.GetResponseStream();

        string tempString = null;
        int count = 0;

        do
        {
            count = resStream.Read(buf, 0, buf.Length);

            if (count != 0)
            {
                tempString = Encoding.ASCII.GetString(buf, 0, count);

                sb.Append(tempString);
            }
        }
        while (count > 0);

        response = sb.ToString();

        return response;
    }

    // SCRAPE
    // Kicks off a scrape using the provided script and source
    public Dictionary<string, List<string>> scrape()
    {
        sourceStartPos = 0;
        values = new Dictionary<string, List<string>>();

        if (source.Trim() == "")
        {
            Console.WriteLine("No source was provided");
        }
        else if (script.Trim() == "")
        {
            Console.WriteLine("No script was provided");
        }
        else
        {
            runScript(_scriptLines);
        }

        return values;
    }

    // RUNSCRIPT
    // Runs through an array of script lines
    // This function will call itself if a REPEAT tag is found
    private bool runScript(string[] scriptLines, int offsetLineNo = 0, int nestLevel = 0)
    {
        int scriptLineNo = 0;
        bool result = false;

        while (scriptLineNo < scriptLines.Length)
        {
            string scriptLine = scriptLines[scriptLineNo].Replace("\r", "").Replace("\t", "");

            if (scriptLine.Trim() == "")
            {
                // Empty line- ignore
            }
            else
            {
                Regex regex = new Regex("\\[" + repeatTag + "[:]?([0-9])?\\]");
                MatchCollection matchCollection = regex.Matches(scriptLine);

                if (matchCollection.Count == 0)
                {
                    // No REPEAT tag so this must be a straightforward HTML search with or without GRABs
                    result = matchLineAndExtractGrabs(scriptLine);

                    if (!result) return false;
                }
                else
                {
                    // This is a REPEAT tag
                    int repeatLimit = int.Parse("0" + matchCollection[0].Groups[1].Value);

                    int endTagLineNo = findClosingTagLine(scriptLines, repeatTag, (scriptLineNo + 1));

                    if (endTagLineNo > 0)
                    {
                        int subScriptStartLine = (scriptLineNo + 1);
                        int subScriptEndLine = (endTagLineNo - 1);
                        int subScriptLinesArraySize = subScriptEndLine - subScriptStartLine + 1;
                        string[] subScriptLines = new string[subScriptLinesArraySize];

                        // Just get the lines inside the REPEAT
                        for (int scriptLineIndex = subScriptStartLine; scriptLineIndex <= subScriptEndLine; scriptLineIndex++)
                        {
                            subScriptLines[scriptLineIndex - subScriptStartLine] = scriptLines[scriptLineIndex];
                        }

                        int repetitions = 0;

                        do
                        {
                            result = runScript(subScriptLines, (offsetLineNo + scriptLineNo), (nestLevel + 1));

                            repetitions++;

                            if ((repeatLimit > 0) && (repetitions == repeatLimit)) break;
                        }
                        while (result);

                        scriptLineNo = endTagLineNo;
                    }
                    else
                    {
                        Console.WriteLine("Found no closing tag for " + repeatTag + " (line " + (scriptLineNo + offsetLineNo) + ")");
                    }
                }
            }

            scriptLineNo++;
        }

        return true;
    }

    // MATCHLINEANDEXTRACTGRABS
    // Checks for the next occurrence of the given script line in the source
    // If the given script line contains GRAB tags then will also store the html found in that relative position in the source
    public bool matchLineAndExtractGrabs(string scriptLine)
    {
        if (scriptLine.IndexOf("[" + grabTag) == -1)
        {
            // We're just finding a piece of HTML
            int startPos = source.IndexOf(scriptLine, sourceStartPos);

            if (startPos > 0) sourceStartPos = startPos + scriptLine.Length;

            return (startPos > 0);
        }
        else
        {
            // We're grabbing data from a piece of HTML
            List<Tag> tags = new List<Tag>();
            List<TagMatch> tagMatches = new List<TagMatch>();

            scriptLine = "[" + grabTag + ":__STARTTAG__]" + scriptLine;

            Regex regex = new Regex("\\[" + grabTag + ":([^\\]]+)?\\]");
            MatchCollection matchCollection = regex.Matches(scriptLine);

            foreach (Match match in matchCollection)
            {
                tagMatches.Add(new TagMatch { name = match.Value, start = match.Index });
            }

            tagMatches.Add(new TagMatch { name = "__ENDTAG__", start = scriptLine.Length });

            int tagIndex = 1;

            // Store all grab tags in this line into tags array
            foreach (TagMatch tagMatch in tagMatches)
            {
                string tagName = tagMatch.name;

                //do not do last item 
                if (tagName == "__ENDTAG__") break;

                int tagStart = tagMatch.start;
                int tagEnd = (tagStart + tagName.Length);

                int nextTagStart = tagMatches[tagIndex].start;

                string variableName = tagName.Substring(("[" + grabTag + ":").Length);
                variableName = variableName.Substring(0, variableName.Length - "]".Length);

                // Store grab key name along with what HTML we're grabbing up to
                string uptoHTML = scriptLine.Substring(tagEnd, (nextTagStart - tagEnd));
                tags.Add(new Tag { variableName = variableName, upToHTML = uptoHTML });

                tagIndex++;
            }

            // Loop through grab tags array tags, attempting to match them all
            foreach (Tag tag in tags)
            {
                string key = tag.variableName;
                string value = tag.upToHTML;

                if (key == "__STARTTAG__")
                {
                    if (value != "")
                    {
                        int startPos = source.IndexOf(value, sourceStartPos);

                        if (startPos == -1) return false;

                        sourceStartPos = (startPos + value.Length);
                    }
                }
                else
                {
                    int sourceEndPos = source.IndexOf(value, sourceStartPos);

                    if (sourceEndPos == -1) return false;

                    storeValue(key, source.Substring(sourceStartPos, sourceEndPos - sourceStartPos));

                    sourceStartPos = (sourceEndPos + value.Length);
                }
            }

            return true;
        }
    }

    // FINDCLOSINGTAGLINE
    // Returns line number of the closing tag for the given tag, allowing for nested versions of this tag
    private int findClosingTagLine(string[] scriptLines, string tag, int startLine)
    {
        int unmatchedClosingTags = 1;
        int scriptLineNo;

        for (scriptLineNo = startLine; scriptLineNo < scriptLines.Length; scriptLineNo++)
        {
            string scriptLine = Regex.Replace(scriptLines[scriptLineNo], "[\r\t]", "");

            if (scriptLine.IndexOf("[" + tag, StringComparison.CurrentCultureIgnoreCase) > -1)
            {
                unmatchedClosingTags++;
            }
            else if (scriptLine.ToUpper() == "[/" + tag + "]".ToUpper())
            {
                unmatchedClosingTags--;
            }

            if (unmatchedClosingTags == 0) break;
        }

        return ((unmatchedClosingTags > 0) ? -1 : scriptLineNo);
    }

    // STOREVALUE
    // Inserts a value into the $values array for a given key
    private void storeValue(string key, string value = null)
    {
        List<string> arrayItems;

        if (values.ContainsKey(key))
        {
            // This key exists and points to an array
            // Push the provided value onto it
            arrayItems = values[key];
        }
        else
        {
            // Key doesn't exist
            // Create key and associate it with an array initially containing just this value
            arrayItems = new List<string>();
        }

        arrayItems.Add(value);

        values[key] = arrayItems;
    }
}
