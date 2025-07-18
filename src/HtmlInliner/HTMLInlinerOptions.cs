namespace HtmlInliner;

public class HTMLInlinerOptions
{
    /// <summary>
    /// An optional basePath for the document which helps resolve relative
    /// paths. Unless there's a special use case, you should leave this
    /// value blank and let the default use either the value from a
    /// BASE tag or the base location of the document.
    /// 
    /// If the document itself contains a BASE tag this value is not used. 
    /// </summary>
    public string? BasePath { get; set; }

    public bool ProcessEmbeddedUrls { get; set; }

    public bool ProcessCss { get; set; }

    public bool ProcessScripts { get; set; }

    public bool ProcessImages { get; set; }

    public bool ProcessLinks { get; set; }

    public bool ProcessAudio { get; set; }

    public static HTMLInlinerOptions Default => new HTMLInlinerOptions
    {
        BasePath = null,
        ProcessEmbeddedUrls = true,
        ProcessCss = true,
        ProcessScripts = true,
        ProcessImages = true,
        ProcessLinks = true,
        ProcessAudio = true
    };
}