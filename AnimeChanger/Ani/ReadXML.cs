﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;

using AnimeChanger.Ani.FilterTypes;

namespace AnimeChanger.Ani
{
    /*  <Root>
     *      <GlobalFilters>
     *          <Filter Type="Whitelist" Keyword="Keyword">Filterword</Filter>
     *          <Filter Type="">Filterword</Filter>
     *      </GlobalFilters>
     *      <WebsiteFilters>
     *          <Website Keyword="Keyword">
     *              <Filter Keywords="Keyword">FilterWord</Filter>
     *              <Filter>FilterWord</Filter>
     *              <Add Where="Start">Someword</Add>
     *          </Website>
     *      </WebsiteFilters>
     *  </Root>
     */
    internal static class XML
    {
        internal static string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DoubleKilled_AniChanger");
                               
        internal static IEnumerable<Filter> ReadXML()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(Path.Combine(FolderPath, "ani.xml"));

            XmlNode root = doc.SelectSingleNode("Root");

            XmlNode GlobalCont = root.SelectSingleNode("GlobalFilters");
            foreach (XmlNode node in GlobalCont.ChildNodes)
            {
                Filter f = new Filter();
                f.Keyword = node.Attributes["Keyword"].InnerText ?? null;
                f.IsGlobal = true;

                if (node.Name == "Filter")
                {
                    var filter = new BasicFilter();
                    filter.FilterWord = node.InnerText;

                    f.FilterType = filter;

                    yield return f;
                }
                else if (node.Name == "Replace")
                {
                    var filter = new Replace();
                    filter.From = node.Attributes["From"].InnerText;
                    filter.To = node.InnerText;

                    f.FilterType = filter;

                    yield return f;
                }
            }

            XmlNode WebCont = root.SelectSingleNode("WebsiteFilters");
            foreach (XmlNode node in WebCont.ChildNodes)
            {
                Filter f = new Filter();
                f.Keyword = node.Attributes["Keyword"].InnerText ?? null;
                f.IsGlobal = false;

                if (node.Name == "Replace")
                {
                    var filter = new Replace();
                    filter.From = node.Attributes["From"].InnerText;
                    filter.To = node.InnerText;

                    f.FilterType = filter;

                    yield return f;
                }
                else if (node.Name == "RemoveFromStart")
                {
                    var filter = new RemoveFromStart();
                    filter.Char = node.InnerText.ToCharArray()[0];

                    f.FilterType = filter;

                    yield return f;
                }
                else if (node.Name == "RemoveFromChar")
                {
                    var filter = new RemoveFromChar();
                    filter.Char = node.InnerText.ToCharArray()[0];

                    f.FilterType = filter;

                    yield return f;
                }
                else if (node.Name == "RemoveInBetween")
                {
                    var filter = new RemoveInBetween();
                    filter.FirstChar = node.Attributes["FirstChar"].InnerText.ToCharArray()[0];
                    filter.LastChar = node.InnerText.ToCharArray()[0];

                    f.FilterType = filter;

                    yield return f;
                }
                else if (node.Name == "Filter")
                {
                    var filter = new BasicFilter();
                    filter.FilterWord = node.InnerText;

                    f.FilterType = filter;

                    yield return f;
                }
                else if (node.Name == "Add")
                {
                    var filter = new BasicAdd();
                    filter.AddWord = node.InnerText;

                    f.FilterType = filter;

                    yield return f;
                }
            }
        }
    }
}
