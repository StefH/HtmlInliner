using HtmlInliner;

var htmlInliner = new HTMLInliner();

var emailOptions = new HTMLInlinerOptions
{
    BasePath = @"c:\temp",
    ProcessImages = true
};
var email = htmlInliner.Process(File.ReadAllText(@"c:\temp\email.html"), emailOptions);
File.WriteAllText(@"c:\temp\email_inlined.html", email);