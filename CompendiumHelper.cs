using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using HeavyDuck.Dnd.Forms;
using HeavyDuck.Utilities.Net;
using HtmlAgilityPack;

namespace HeavyDuck.Dnd
{
    public class CompendiumHelper
    {
        private const string URL_LOGIN = "http://www.wizards.com/dndinsider/compendium/login.aspx";
        private const string URL_SEARCH = "http://www.wizards.com/dndinsider/compendium/database.aspx";
        private const string URL_SERVICE = "http://www.wizards.com/dndinsider/compendium/CompendiumSearch.asmx/KeywordSearch";
        private const string URL_MONSTER = "http://www.wizards.com/dndinsider/compendium/monster.aspx?id=";
        private const string URL_CSS_RESET = "http://www.wizards.com/dndinsider/compendium/styles/reset.css";
        private const string URL_CSS_SITE = "http://www.wizards.com/dndinsider/compendium/styles/site.css";
        private const string URL_CSS_DETAIL = "http://www.wizards.com/dndinsider/compendium/styles/detail.css";

        private static readonly Regex m_style_regex = new Regex("href=\"styles/(.*?\\.css)\"", RegexOptions.IgnoreCase);
        private static readonly string m_cache_root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"HeavyDuck.Dnd");

        private CookieContainer m_cookies = new CookieContainer();

        static CompendiumHelper()
        {
            try
            {
                Directory.CreateDirectory(m_cache_root);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        public void Login()
        {
            Dictionary<string, string> parameters;

            // show login dialog
            using (InsiderLoginDialog d = new InsiderLoginDialog())
            {
                if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    throw new ApplicationException("User refused Insider login");

                parameters = new Dictionary<string, string>();
                parameters["email"] = d.Email;
                parameters["password"] = d.Password;
                parameters["InsiderSignin"] = "Sign In";
            }

            // perform login page GET to get event validation
            using (HttpWebResponse response = HttpHelper.UrlGet(URL_LOGIN, m_cookies))
            {
                // extract the validation parameter from the response stream
                using (Stream s = response.GetResponseStream())
                {
                    HtmlDocument doc;
                    XPathNavigator nav_view, nav_event;
                    const string NAME_VIEW = "__VIEWSTATE";
                    const string NAME_EVENT = "__EVENTVALIDATION";

                    // read in the login page HTML
                    doc = new HtmlDocument();
                    doc.Load(s);

                    // select the elements we want to read
                    nav_view = doc.CreateNavigator().SelectSingleNode("//input[@name = '" + NAME_VIEW + "']");
                    nav_event = doc.CreateNavigator().SelectSingleNode("//input[@name = '" + NAME_EVENT + "']");

                    // if there was no such node, we're screwed
                    if (nav_view == null || nav_event == null)
                        throw new ApplicationException("Can't find parameters in login page");

                    // set value
                    parameters[NAME_VIEW] = nav_view.SelectSingleNode("@value").Value;
                    parameters[NAME_EVENT] = nav_event.SelectSingleNode("@value").Value;
                }
            }

            // perform login request and grab the resulting cookie(s)
            using (HttpWebResponse response = HttpHelper.UrlPost(URL_LOGIN, parameters, false, m_cookies))
            {
                // pass for now, should probably do some validation later
            }
        }

        public string GetCombinedDetailCss()
        {
            StringBuilder css = new StringBuilder();

            // combine all the css files it imports into one big string
            foreach (string url in new string[] { URL_CSS_RESET, URL_CSS_SITE, URL_CSS_DETAIL })
            {
                using (HttpWebResponse response = HttpHelper.UrlGet(url))
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        css.AppendLine(reader.ReadToEnd());
                }

                // don't spam!
                System.Threading.Thread.Sleep(100);
            }

            // add our auto-width dealie
            css.AppendLine("#detail { width: auto !important; }");

            // return the whole bit
            return css.ToString();
        }

        public Stream GetEntryByUrl(string url)
        {
            HttpWebResponse response = null;

            // check if login is required
            while (!ValidateCookies(url))
                Login();

            try
            {
                response = HttpHelper.UrlGet(url, m_cookies);
                return response.GetResponseStream();
            }
            catch
            {
                if (response != null) response.Close();
                throw;
            }
        }

        public Stream GetMonster(int id)
        {
            return GetEntryByUrl(URL_MONSTER + id);
        }

        public Stream SearchMonsters(string query)
        {
            Dictionary<string, string> parameters;
            HttpWebResponse response = null;

            parameters = new Dictionary<string, string>();
            parameters["Keywords"] = query;
            parameters["Tab"] = "Monster";

            try
            {
                response = HttpHelper.UrlPost(URL_SERVICE, parameters, true, m_cookies);
                return response.GetResponseStream();
            }
            catch
            {
                if (response != null) response.Close();
                throw;
            }
        }

        public bool ValidateCookies(string url)
        {
            CookieCollection cookies = m_cookies.GetCookies(new Uri(url));
            string[] required_cookies = {
                "ASP.NET_SessionId",
                "iPlanetDirectoryPro",
            };

            // check for the cookies we know are required and for expiration
            foreach (string name in required_cookies)
                if (cookies[name] == null || cookies[name].Expired)
                    return false;

            // if we got past the gauntlet, all's well
            return true;
        }

        public void SaveCookies()
        {
            BinaryFormatter formatter;
            string path = Path.Combine(m_cache_root, @"cookies.dat");

            try
            {
                using (FileStream fs = File.OpenWrite(path))
                {
                    formatter = new BinaryFormatter();
                    formatter.Serialize(fs, m_cookies);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        public void LoadCookies()
        {
            BinaryFormatter formatter;
            string path = Path.Combine(m_cache_root, @"cookies.dat");

            if (!File.Exists(path))
                return;

            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    formatter = new BinaryFormatter();
                    m_cookies = (CookieContainer)formatter.Deserialize(fs);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        public static string FixStyles(string html)
        {
            html = m_style_regex.Replace(html, (m) => "href=\"http://www.wizards.com/dndinsider/compendium/styles/" + m.Groups[1].Value + "\"");
            html = html.Replace("</head>", "<style>#detail { width: auto !important; }</style></head>");

            return html;
        }
    }
}
