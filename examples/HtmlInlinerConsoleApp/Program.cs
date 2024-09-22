using HtmlInliner;

var htmlInliner = new HTMLInliner();
// var googleInlined = htmlInliner.Process("https://www.google.com");
// File.WriteAllText(@"c:\temp\web\google_inlined.htm", googleInlined);

var mstackInlined = htmlInliner.Process("https://www.mstack.nl");
File.WriteAllText(@"c:\temp\web\mstack_inlined.htm", mstackInlined);