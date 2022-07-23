namespace HtmlInliner;

public interface IHTMLInliner
{
    /// <summary>
    /// Packages an HTML document into a single file that embeds all images, favicon, css, scripts, fonts and other url() loaded entries.
    /// 
    /// The result is a fully self-contained HTML document. 
    /// </summary>
    /// <param name="urlOrFileOrHtmlText">A Web Url or fully qualified local file name or Html text string.</param>
    /// <param name="basePath">
    /// An optional basePath for the document which helps resolve relative
    /// paths. Unless there's a special use case, you should leave this
    /// value blank and let the default use either the value from a
    /// BASE tag or the base location of the document.
    /// 
    /// If the document itself contains a BASE tag this value is not used.
    /// </param>
    /// <returns>HTML string or null in case of an error.</returns>
    string? Process(string urlOrFileOrHtmlText, string? basePath = null);
}