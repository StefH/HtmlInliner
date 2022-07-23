using HtmlInliner;

var htmlInliner = new HTMLInliner();
var inlined = htmlInliner.Process("https://www.google.com");
File.WriteAllText(@"c:\temp\web\google_inlined.htm", inlined);