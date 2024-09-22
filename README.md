# HtmlInliner
Packages an HTML document into a single file that embeds all images, favicon, css, scripts, fonts and other url() loaded entries.

This can be usefull when moving CSS to inline style attributes, to gain maximum E-mail client compatibility or Pdf generation from Html.

Code based on [RickStrahl/Westwind.HtmlPackager](https://github.com/RickStrahl/Westwind.HtmlPackager).

## NuGet

[![NuGet Badge](https://img.shields.io/nuget/v/HtmlInliner)](https://www.nuget.org/packages/HtmlInliner)

## Example

``` c#
using HtmlInliner;

var htmlInliner = new HTMLInliner();
var inlined = htmlInliner.Process("https://www.google.com");
File.WriteAllText(@"c:\temp\web\google_inlined.htm", inlined);
```
