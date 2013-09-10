using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

public class simpleScrape
{
    private Dictionary<string, List<string>> values;
    private string _script = "";

    public string source = "";

	const string userAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.11 (KHTML, like Gecko) Chrome/23.0.1271.95 Safari/537.11";
    const string grabTag = "GRAB";
    const string repeatTag = "REPEAT";

// GETTERS/ SETTERS
	public string sourceURL 
    {
	    set {
            source = urlGetContents(value);
        }
    }

    public string sourcePath 
    {
        set {
			source = file_get_contents(value);
		}
    }

    public string scriptPath 
    {
        set {
			_script = file_get_contents(value);
		}
    }

    public string script 
    {
        set {
            _script = value;
		}
        get { return _script; }
	}

// FILE_GET_CONTENTS
// Reads in and returns the text in a given file
    private string file_get_contents(string filename) 
    {
        string content=  "";

        try {
            using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath(filename))) {
                content = sr.ReadToEnd();
            }
        }
        catch (Exception e) {
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

        do {
            count = resStream.Read(buf, 0, buf.Length);

            if (count != 0) {
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
        values = new Dictionary<string, List<string>>();

		if (source.Trim() == "") {
            Console.WriteLine("No source was provided");
		}
		else if (script.Trim() == "") {
            Console.WriteLine("No script was provided");
		}
		else {
            string regexScript = convertToRegex(_script);

            runScript(regexScript);
		}

		return values;
	}

// CONVERTTOREGEX
// Converts script into a regex string
    private string convertToRegex(string script) 
    {
        string regexString = @"(?<tagComplete>\[(?<tagType>[^\n]+?)(?::(?<tagParameter>[^\n\]]+?)?)*\])";
        string regexScript = "";
        string[] scriptLines = script.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
        Regex regex = new Regex(regexString, RegexOptions.Singleline);

        for (int scriptLineNo = 0; scriptLineNo < scriptLines.Length; scriptLineNo++) {
            string scriptLine = scriptLines[scriptLineNo].Trim();

            if (scriptLine != "") {
                MatchCollection tagVariables = regex.Matches(scriptLine);
                bool isSearchLine = true;

                foreach (Match tagVariable in tagVariables) {
                    string tagComplete = tagVariable.Groups["tagComplete"].ToString();
                    string tagType = tagVariable.Groups["tagType"].ToString();
                    string tagParameter = tagVariable.Groups["tagParameter"].ToString();

                    switch (tagType.ToUpper()) {
                        case repeatTag:
                            int endTagLineNo = findClosingTagLine(scriptLines, repeatTag, (scriptLineNo + 1));

                            scriptLine = scriptLine.Replace(tagComplete, @"(?>");

                            if (tagParameter == "") {
                                scriptLines[endTagLineNo] = scriptLines[endTagLineNo].Replace(@"[/" + repeatTag + "]", @")*");
                            }
                            else {
                                scriptLines[endTagLineNo] = scriptLines[endTagLineNo].Replace(@"[/" + repeatTag + "]", @"){" + tagParameter + "}");
                            }

                            isSearchLine = false;

                            break;

                        case grabTag:
                            scriptLine = scriptLine.Replace(tagComplete, @"(?<" + tagParameter + @">[^\r\n]+?)");

                            break;
                    }
                }

                if (isSearchLine && (scriptLineNo > 0)) scriptLine = @".+?" + scriptLine;

                scriptLines[scriptLineNo] = scriptLine;
            }
        }

        foreach (string scriptLine in scriptLines) regexScript += scriptLine;

        return regexScript;
    }

// RUNSCRIPT
// Executes regex on the source and stores the matches
    private void runScript(string regexScript)
    {
        Regex regex = new Regex(regexScript, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        MatchCollection matches = regex.Matches(source);

        foreach (Match match in matches) {
            for (int group = 1; group < match.Groups.Count; group++) {
                foreach (Capture capture in match.Groups[group].Captures) storeValue(regex.GroupNameFromNumber(group), capture.Value);
            }
        }
}

// FINDCLOSINGTAGLINE
// Returns line number of the closing tag for the given tag, allowing for nested versions of this tag
	private int findClosingTagLine(string[] scriptLines, string tag, int startLine) 
    {
		int unmatchedClosingTags = 1;
        int scriptLineNo;

		for (scriptLineNo = startLine; scriptLineNo < scriptLines.Length; scriptLineNo++) {
            string scriptLine = Regex.Replace(scriptLines[scriptLineNo], "[\r\t]", "");

			if (scriptLine.IndexOf("[" + tag, StringComparison.CurrentCultureIgnoreCase) > -1) {
				unmatchedClosingTags++;
			}
            else if (scriptLine.ToUpper() == "[/" + tag + "]".ToUpper()) {
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

		if (values.ContainsKey(key)) {
			// This key exists and points to an array
			// Push the provided value onto it
			arrayItems = values[key];
		}
		else {
			// Key doesn't exist
			// Create key and associate it with an array initially containing just this value
            arrayItems = new List<string>();
		}

		arrayItems.Add(value);

		values[key] = arrayItems;
	}
}