using System;
using System.Collections.Generic;

public partial class _Default : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        simpleScrape scraper = new simpleScrape();
        Dictionary<string, List<string>> values = new Dictionary<string, List<string>>();

        scraper.sourceURL = "https://twitter.com/ProfBrianCox";
        scraper.scriptPath = "~/App_Code/exampleScript.txt";

        values = scraper.scrape();

        if (values != null) {
            foreach (string key in values.Keys) {
                for (int index = 0; index < values[key].Count; index++) {
                    Response.Write("[\"" + key + "\"][" + index.ToString() + "] = \"" + values[key][index] + "\"<br>");
                }
            }
        }
    }
}