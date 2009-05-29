using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HeavyDuck.Utilities.Net;

namespace HeavyDuck.Dnd
{
    public class CompendiumHelper
    {
        private const string URL_LOGIN = "http://www.wizards.com/dndinsider/compendium/login.aspx";
        private const string URL_SEARCH = "http://www.wizards.com/dndinsider/compendium/database.aspx";
        private const string URL_SERVICE = "http://www.wizards.com/dndinsider/compendium/CompendiumSearch.asmx/KeywordSearch";
        private const string URL_MONSTER = "http://www.wizards.com/dndinsider/compendium/monster.aspx?id=";

        private static readonly Regex m_style_regex = new Regex("href=\"styles/(.*?\\.css)\"", RegexOptions.IgnoreCase);

        private CookieCollection m_login_cookies = null;
        private string m_email;
        private string m_password;

        public CompendiumHelper(string email, string password)
        {
            m_email = email;
            m_password = password;
        }

        public void Login()
        {
            Dictionary<string, string> parameters;

            parameters = new Dictionary<string, string>();
            parameters["email"] = m_email;
            parameters["password"] = m_password;

            using (HttpWebResponse response = HttpHelper.UrlPost(URL_LOGIN, parameters))
            {
                m_login_cookies = response.Cookies;
            }
        }

        public Stream GetMonster(int id)
        {
            HttpWebResponse response = null;

            try
            {
                response = HttpHelper.UrlGet(URL_MONSTER + id);
                return response.GetResponseStream();
            }
            catch
            {
                if (response != null) response.Close();
                throw;
            }
        }

        public Stream SearchMonsters(string query)
        {
            Dictionary<string, string> parameters;
            HttpWebResponse response = null;

            // sanity check
            if (!ValidateCookies())
                throw new InvalidOperationException("You must login before you can search the compendium");

            parameters = new Dictionary<string, string>();
            parameters["Keywords"] = query;
            parameters["Tab"] = "Monster";

            try
            {
                response = HttpHelper.UrlPost(URL_SERVICE, parameters, m_login_cookies);
                return response.GetResponseStream();
            }
            catch
            {
                if (response != null) response.Close();
                throw;
            }
        }

        public bool ValidateCookies()
        {
            // if there is no cookie it's definitely invalid
            if (m_login_cookies == null)
                return false;

            // check expiration
            foreach (Cookie cookie in m_login_cookies)
                if (cookie.Expired)
                    return false;

            // if we got past the gauntlet, all's well
            return true;
        }

        public static string FixStyles(string html)
        {
            html = m_style_regex.Replace(html, (m) => "href=\"http://www.wizards.com/dndinsider/compendium/styles/" + m.Groups[1].Value + "\"");
            html = html.Replace("</head>", "<style>#detail { width: auto !important; }</style></head>");

            return html;
        }
    }
}
