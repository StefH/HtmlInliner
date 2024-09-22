using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Stef.Validation;

namespace HtmlInliner;

/// <summary>
/// Based on https://github.com/RickStrahl/Westwind.HtmlPackager
/// </summary>
public class HTMLInliner : IHTMLInliner
{
    private const string DefaultContentType = "application/image";
    private static readonly Regex UrlRegEx = new("url\\(.*?\\)", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public string? Process(string urlOrFileOrHtmlText, string? basePath = null)
    {
        Guard.NotNullOrEmpty(urlOrFileOrHtmlText);

        Uri baseUri;
        HtmlDocument doc;

        if (urlOrFileOrHtmlText.IsHttp() && urlOrFileOrHtmlText.Contains("://"))
        {
            baseUri = new Uri(urlOrFileOrHtmlText);

            try
            {
                doc = LoadHtmlDocument(urlOrFileOrHtmlText);
            }
            catch
            {
                return null;
            }

            var docBase = doc.DocumentNode.SelectSingleNode("//base");
            if (docBase != null)
            {
                basePath = docBase.Attributes["href"]?.Value;
                baseUri = basePath != null ? new Uri(baseUri: new Uri(urlOrFileOrHtmlText), relativeUri: basePath) : new Uri(urlOrFileOrHtmlText); // TODO
            }
            docBase?.Remove();

            // Process the document to also replace inline url(...)
            doc.LoadHtml(ProcessEmbeddedUrls(doc.Text, string.Empty, baseUri));

            ProcessCss(doc, baseUri);
            ProcessScripts(doc, baseUri);
            ProcessImages(doc, baseUri);
            ProcessLinks(doc, baseUri);
            ProcessAudio(doc, baseUri);
        }
        else
        {
            try
            {
                doc = new HtmlDocument();
                doc.Load(urlOrFileOrHtmlText);
            }
            catch
            {
                try
                {
                    doc = new HtmlDocument();
                    doc.LoadHtml(urlOrFileOrHtmlText);
                }
                catch
                {
                    return null;
                }
            }

            var docBase = doc.DocumentNode.SelectSingleNode("//base");
            var url = docBase?.Attributes["href"]?.Value;
            if (url != null && url.StartsWith("file:///"))
            {
                var tempBasePath = url.Replace("file:///", string.Empty);
                if (!string.IsNullOrEmpty(tempBasePath) && tempBasePath != "\\")
                {
                    basePath = tempBasePath;
                }
            }
            docBase?.Remove();

            var oldPath = Environment.CurrentDirectory;
            try
            {
                if (string.IsNullOrEmpty(basePath))
                {
                    basePath = Path.GetDirectoryName(urlOrFileOrHtmlText) + "\\";
                }

                Directory.SetCurrentDirectory(basePath);
                baseUri = new Uri(basePath);

                // Process the document to also replace inline url(...)
                doc.LoadHtml(ProcessEmbeddedUrls(doc.Text, string.Empty, baseUri));

                ProcessCss(doc, baseUri);
                ProcessScripts(doc, baseUri);
                ProcessImages(doc, baseUri);
                ProcessLinks(doc, baseUri);
                ProcessAudio(doc, baseUri);
            }
            finally
            {
                Directory.SetCurrentDirectory(oldPath);
            }
        }

        return doc.DocumentNode.InnerHtml;
    }

    private static void ProcessCss(HtmlDocument doc, Uri baseUri)
    {
        var links = doc.DocumentNode.SelectNodes("//link");
        if (links == null)
        {
            return;
        }

        int internalNamingCounter = 0;
        foreach (var link in links)
        {
            var url = link.Attributes["href"]?.Value;
            var rel = link.Attributes["rel"]?.Value;

            if (url == null || rel?.Contains("stylesheet") != true)
            {
                continue;
            }

            string cssText;
            if (url.IsHttp())
            {
                using var http = new WebClient();
                cssText = http.DownloadString(url);
            }
            else if (url.StartsWith("file:///"))
            {
                var fileUri = new Uri(url);
                cssText = File.ReadAllText(fileUri.LocalPath);
            }
            else // Relative Path
            {
                var relativeUri = Append(baseUri, url);
                url = relativeUri.AbsoluteUri;

                if (url.StartsWith("http") && url.Contains("://"))
                {
                    using var http = new WebClient();
                    cssText = http.DownloadString(url);
                }
                else
                {
                    cssText = File.ReadAllText(relativeUri.LocalPath);
                }
            }

            cssText = ProcessEmbeddedUrls(cssText, url, baseUri);

            var newChild = new HtmlNode(HtmlNodeType.Element, doc, internalNamingCounter++)
            {
                Name = "style",
                InnerHtml = Environment.NewLine + cssText + Environment.NewLine
            };

            link.ParentNode.InsertAfter(newChild, link);
            link.Remove();
        }
    }

    private static void ProcessScripts(HtmlDocument doc, Uri baseUri)
    {
        var scripts = doc.DocumentNode.SelectNodes("//script");
        if (scripts == null || scripts.Count < 1)
        {
            return;
        }

        foreach (var script in scripts)
        {
            var url = script.Attributes["src"]?.Value;
            if (url == null)
            {
                continue;
            }

            byte[] scriptData;
            if (url.IsHttp())
            {
                using var http = new WebClient();
                scriptData = http.DownloadData(url);
            }
            else if (url.IsFile())
            {
                url = url.Substring(8);
                scriptData = File.ReadAllBytes(WebUtility.UrlDecode(url));
            }
            else // Relative Path
            {
                try
                {
                    var origUri = Append(baseUri, url);
                    url = origUri.AbsoluteUri;

                    if (url.StartsWith("http") && url.Contains("://"))
                    {
                        using var http = new WebClient();
                        scriptData = http.DownloadData(url);
                    }
                    else
                    {
                        scriptData = File.ReadAllBytes(WebUtility.UrlDecode(url));
                    }
                }
                catch
                {
                    continue;
                }
            }

            var data = $"data:text/javascript;base64,{Convert.ToBase64String(scriptData)}";
            script.Attributes["src"].Value = data;
        }
    }

    private static void ProcessImages(HtmlDocument doc, Uri baseUri)
    {
        // https://www.w3schools.com/tags/tag_img.asp
        var imageNodes = doc.DocumentNode.SelectNodes("//img");
        if (imageNodes == null || imageNodes.Count < 1)
        {
            return;
        }

        // srcset ???
        foreach (var src in new[] { "src" })
        {
            var images = imageNodes
                .Select(node => new { attr = src, node })
                .ToList();

            // https://www.w3schools.com/tags/att_link_rel.asp
            var favIconNodes = doc.DocumentNode.SelectNodes("//link");
            if (favIconNodes != null)
            {
                foreach (var favIconNode in favIconNodes)
                {
                    var url = favIconNode.Attributes["href"]?.Value;
                    var rel = favIconNode.Attributes["rel"]?.Value;

                    if (url != null && rel.Contains("icon", StringComparison.OrdinalIgnoreCase))
                    {
                        images.Add(new { attr = "href", node = favIconNode });
                    }
                }
            }

            foreach (var image in images)
            {
                var url = image.node.Attributes[image.attr]?.Value;
                if (url == null)
                {
                    continue;
                }

                byte[] imageData;
                string contentType;

                if (url.IsHttp())
                {
                    using var http = new WebClient();
                    imageData = http.DownloadData(url);
                    contentType = http.ResponseHeaders[HttpResponseHeader.ContentType];
                }
                else if (url.IsFile())
                {
                    url = url.Substring(8);

                    try
                    {
                        imageData = File.ReadAllBytes(url);
                        contentType = GetMimeTypeFromUrl(url);
                    }
                    catch
                    {
                        continue;
                    }
                }
                else // Relative Path
                {
                    try
                    {
                        var origUri = Append(baseUri, url);
                        url = origUri.AbsoluteUri;

                        if (url.IsHttp() && url.Contains("://"))
                        {
                            using var http = new WebClient();
                            imageData = http.DownloadData(url);
                        }
                        else
                        {
                            imageData = File.ReadAllBytes(WebUtility.UrlDecode(url.Replace("file:///", string.Empty)));
                        }

                        contentType = GetMimeTypeFromUrl(url);
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (imageData == null)
                {
                    continue;
                }

                // Only replace the node.Name for a real image, not for an icon.
                if (image.attr == src)
                {
                    image.node.Name = "img";
                }

                var data = $"data:{contentType};base64,{Convert.ToBase64String(imageData)}";
                image.node.Attributes[image.attr].Value = data;
            }
        }
    }

    private static void ProcessLinks(HtmlDocument doc, Uri baseUri)
    {
        var links = doc.DocumentNode.SelectNodes("//a");
        if (links == null || links.Count < 1)
        {
            return;
        }

        foreach (var link in links)
        {
            var url = link.Attributes["href"]?.Value;

            if (string.IsNullOrEmpty(url) ||
                url!.StartsWith("http", comparisonType: StringComparison.InvariantCultureIgnoreCase) ||
                url.StartsWith("#") ||
                url.IndexOf("javascript:", StringComparison.InvariantCultureIgnoreCase) > -1)
            {
                continue;
            }

            try
            {
                var linkUrl = Append(baseUri, url).ToString();
                link.Attributes["href"].Value = linkUrl;
                link.Name = "a";
            }
            catch
            {
                // Just continue
            }
        }
    }

    private static void ProcessAudio(HtmlDocument doc, Uri baseUri)
    {
        var audioNodes = doc.DocumentNode.SelectNodes("//audio");
        if (audioNodes == null || audioNodes.Count < 1)
        {
            return;
        }

        foreach (var audioNode in audioNodes)
        {
            var url = audioNode.Attributes["src"]?.Value;
            if (url == null)
            {
                continue;
            }

            byte[]? audioData;
            string? contentType;
            if (url.IsHttp())
            {
                var http = new WebClient();
                audioData = http.DownloadData(url);
                contentType = http.ResponseHeaders[HttpResponseHeader.ContentType];
            }
            else if (url.IsFile())
            {
                url = url.Substring(8);

                try
                {
                    audioData = File.ReadAllBytes(url);
                    contentType = GetMimeTypeFromUrl(url);
                }
                catch
                {
                    continue;
                }
            }
            else // Relative Path
            {
                try
                {
                    var origUri = Append(baseUri, url);
                    url = origUri.AbsoluteUri;

                    if (url.IsHttp() && url.Contains("://"))
                    {
                        var http = new WebClient();
                        audioData = http.DownloadData(url);
                    }
                    else
                    {
                        audioData = File.ReadAllBytes(WebUtility.UrlDecode(url.Replace("file:///", string.Empty)));
                    }

                    contentType = GetMimeTypeFromUrl(url);
                }
                catch
                {
                    continue;
                }
            }

            if (audioData == null)
            {
                continue;
            }

            var data = $"data:{contentType};base64,{Convert.ToBase64String(audioData)}";
            audioNode.Attributes["src"].Value = data;
            audioNode.Name = "audio";
        }
    }

    /// <summary>
    /// Processes embedded url('link') links and embeds the data and returns the expanded HTML string either with embedded content, or externalized links.
    /// </summary>
    private static string ProcessEmbeddedUrls(string html, string baseUrl, Uri baseUri)
    {
        var matches = UrlRegEx.Matches(html);

        foreach (Match match in matches)
        {
            string matched = match.Value;
            if (string.IsNullOrEmpty(matched))
            {
                continue;
            }

            var url = ExtractString(matched, "(", ")").Trim('\'', '\"').Replace("&amp;", "").Replace("quot;", "");
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            if (url.Contains("?"))
            {
                url = ExtractString(url, "", "?");
            }

            if (url.EndsWith(".eot") || url.EndsWith(".ttf"))
            {
                continue;
            }

            string? contentType;
            byte[]? linkData;
            if (url.IsHttp())
            {
                using var http = new WebClient();
                linkData = http.DownloadData(url);
                contentType = http.ResponseHeaders[HttpResponseHeader.ContentType];
            }
            else if (url.IsFile())
            {
                var baseUriForFile = new Uri(baseUrl);
                url = new Uri(baseUriForFile, new Uri(url)).AbsoluteUri;

                try
                {
                    contentType = GetMimeTypeFromUrl(url);
                    if (contentType == DefaultContentType)
                    {
                        continue;
                    }

                    linkData = File.ReadAllBytes(WebUtility.UrlDecode(url));
                }
                catch
                {
                    continue;
                }
            }
            else
            {
                try
                {
                    var uri = Append(baseUri, url);
                    url = uri.AbsoluteUri;

                    if (url.IsHttp() && url.Contains("://"))
                    {
                        using var http = new WebClient();
                        linkData = http.DownloadData(url);
                    }
                    else
                    {
                        linkData = File.ReadAllBytes(WebUtility.UrlDecode(url.Replace("file:///", string.Empty)));
                    }

                    contentType = GetMimeTypeFromUrl(url);
                }
                catch
                {
                    continue;
                }
            }

            if (linkData == null)
            {
                continue;
            }

            var data = $"data:{contentType};base64,{Convert.ToBase64String(linkData)}";
            var urlContent = "url('" + data + "')";

            html = html.Replace(matched, urlContent);
        }

        return html;
    }

    /// <summary>
    /// https://github.com/zzzprojects/html-agility-pack/issues/480
    /// </summary>
    private static HtmlDocument LoadHtmlDocument(string htmlUrlOrFile)
    {
        var web = new HtmlWeb
        {
            AutoDetectEncoding = false,
            OverrideEncoding = Encoding.UTF8
        };

        return web.Load(htmlUrlOrFile);
    }

    private static string GetMimeTypeFromUrl(string url)
    {
        var ext = Path.GetExtension(url).ToLower();

        return MimeTypeMap.TryGetMimeType(ext, out var mime) ? mime : DefaultContentType;
    }

    private static Uri Append(Uri? uri, params string[] paths)
    {
        return new Uri(paths.Aggregate(uri?.AbsoluteUri ?? string.Empty, (current, path) => $"{current.TrimEnd('/')}/{path.TrimStart('/')}"));
    }

    /// <summary>
    /// Extracts a string from between a pair of delimiters. Only the first instance is found.
    /// </summary>
    private static string ExtractString(
        string? source,
        string startDelim,
        string endDelim,
        bool caseSensitive = false,
        bool allowMissingEndDelimiter = false,
        bool returnDelimiters = false)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        var at1 = source!.IndexOf(startDelim, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        if (at1 == -1)
        {
            return string.Empty;
        }

        var at2 = source.IndexOf(endDelim, at1 + startDelim.Length, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

        if (allowMissingEndDelimiter && at2 < 0)
        {
            return !returnDelimiters ?
                source.Substring(at1 + startDelim.Length) :
                source.Substring(at1);
        }

        if (at1 > -1 && at2 > 1)
        {
            return !returnDelimiters ?
                source.Substring(at1 + startDelim.Length, at2 - at1 - startDelim.Length) :
                source.Substring(at1, at2 - at1 + endDelim.Length);
        }

        return string.Empty;
    }
}