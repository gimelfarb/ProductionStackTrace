# ProductionStackTrace

[Don't want to deploy PDBs][2], but still get exception stack traces that map to original source line numbers? Want to [have your cake and eat it too](http://en.wikipedia.org/wiki/You_can't_have_your_cake_and_eat_it)?

[ProductionStackTrace][1] is intended to **run in Production**, **without any access to PDB symbol files**. It produces a stack trace with enough information to be analyzed by the techies back on the base with a **help of a [Symbol Store][3]** to get original source code line numbers.

[1]: https://www.nuget.org/packages/ProductionStackTrace
[2]: http://www.lionhack.com/2014/01/14/advanced-dotnet-debugging-pdbs-and-symbols/
[3]: http://msdn.microsoft.com/en-us/library/windows/desktop/ms680693(v=vs.85).aspx

## Usage

1. Install the **ProductionStackTrace** [NuGet package][1]

        PM> Install-Package ProductionStackTrace

2. In your code, where you log exceptions:
   
   <pre lang="csharp"><code>
    using ProductionStackTrace;
    ...
    try
    {
        ...
    }
    catch (Exception ex)
    {
        var trace = ExceptionReporting.GetExceptionReport(ex);
        Console.WriteLine(trace);
    }
   </code></pre>

This will produce a stack trace similar to this, which is still very much similar to standard one, but with some extra info embedded:

<pre>
System.Exception: Test exception
   at <b>ProductionStackTrace.Test!0x0600000f!</b>ProductionStackTrace.Test.TestExceptionReporting.TestSimpleException() <b>+0xc</b>
==========
MODULE: ProductionStackTrace.Test => ProductionStackTrace.Test, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null; G:4e6f400982514fc29d72d9928819aac0; A:6
</pre>

Differences with original stack trace (i.e. `ex.ToString()`):

  * Assembly name, plus method's [metadata token](http://blogs.msdn.com/b/davbr/archive/2011/10/17/metadata-tokens-run-time-ids-and-type-loading.aspx), to find it later inside PDB symbols
  * IL offset inside the method, which later can be mapped to line number
  * Section on assembly full naming, and associated PDB [symbols parameters (GUID+Age)][2]

## Symbol Store

Make sure you're using a [Symbol Store][3] and have a build that publishes your PDB symbols there. If you need help setting it up, [see here][2] (also has more in-depth info about PDB symbols).

The simplest way to do this is to setup a shared network location, and use TFS Build server, which has integrated symbol indexing & publishing via a simple property setting ("Path to Publish Symbols").

![TFS Build Symbols Options](http://static.lionhack.com/images/2014-01-14-advanced-dotnet-debugging-pdbs-and-symbols/TFSBuild_SourceIndexing.png)

When analyzing the retrieved logs, it helps to have the Symbol Server path configured (in Visual Studio go to Tools > Options > Debugger > Symbols). That way the analyzer can automatically find the right symbols file, and generate line mappings.

![Visual Studio Debugging Symbols Paths](http://static.lionhack.com/images/2014-01-14-advanced-dotnet-debugging-pdbs-and-symbols/Debugging_Options_Symbols.png)

## Analyzing

Analyzing these stack traces is simple with an associated _analyzer_ application.

1. Install the **ProductionStackTrace Analyze Tool** [NuGet package](https://www.nuget.org/packages/ProductionStackTrace.Analyze.Console) - it's a solution-level tools package:

        PM> Install-Package ProductionStackTrace.Analyze.Console

2. This will add a PowerShell command to the Package Manager Console - to launch it:

        PM> Convert-ProductionStackTrace

Copy-paste the stack trace into the window, to get the converted stack trace with original source line mappings:

<pre>
System.Exception: Test exception
   at ProductionStackTrace.Test.TestExceptionReporting.TestSimpleException() in ..\ProductionStackTrace.Test\TestExceptionReporting.cs:line 23
</pre>

You can also convert the entire log file:

    PM> Convert-ProductionStackTrace [logfile] [outfile]

## Embedding

If you want to embed analyzing into your own automated process, you can just get the binary files under `tools` folder in the extracted package folder.

Alternatively, there is a **ProductionStackTrace Analyze** NuGet package, which contain the library which performs the conversion. 

## Use with a logging framework

If you are logging exceptions through a logging framework, such as `log4net`, chances are that framework by default renders Exceptions by using the built-in `.ToString()` method which produces the default stack trace.

To override it, you have several choices:

  - Instead of `Exception`, log the string from `ExceptionReporting.GetExceptionReport`
    - Quick fix, but that means you don't get the benefit of logging framework recognizing that you are logging an exception
  - Wrap it inside `ReportingException` custom class, which overrides `ToString` method to produce the stack trace report
    - Not very nice, because the type of exception going through logging framework is now `ReportingException` and not the original one (although the output is overridden)
  - Customize the framework to override how `Exception` is rendered

The 3rd choice is obviously better, if your logging framework supports it. Below is an example of how to do it with the widely popular `log4net`.

<pre lang="csharp">
<code>
[assembly: log4net.Config.Plugin(
    typeof(ProductionStackTraceLog4NetPlugin))]

internal class ProductionStackTraceLog4NetPlugin : 
    log4net.Plugin.PluginSkeleton,
    log4net.ObjectRenderer.IObjectRenderer
{
    public ProductionStackTraceLog4NetPlugin() 
        : base("ProductionStackTrace") {}

    public override void Attach(
        log4net.Repository.ILoggerRepository repository)
    {
        base.Attach(repository);
        repository.RendererMap.Put(typeof(Exception), this);
    }

    public void RenderObject(
        log4net.ObjectRenderer.RendererMap rendererMap, 
        object obj, 
        System.IO.TextWriter writer)
    {
        writer.Write(ProductionStackTrace.ExceptionReporting
            .GetExceptionReport((Exception) obj));
    }    
}
</code>
</pre>

This registers a plugin with `log4net` which in turn adds an `IObjectRenderer` for the Exception type, which overrides how Exceptions look in the output.

## Troubleshooting

1. Original source line information is not showing when analyzing the logs

    In order for source line info to show, analyzer must be able to locate the matching PDB symbol files (i.e. matching the GUID & Age parameters of the specific assembly). The best way to ensure that is to incorporate publishing to Symbol Store in your build process - this makes sure that any built assembly has its symbols stored in a discoverable location.

    If you start analyzer in interactive mode (e.g. `Convert-ProductionStackTrace`), it will print the current symbols paths at the start. If your symbols server is not showing, ensure that it is specified in the Visual Studio Debugger Symbol Paths (Tools > Options > Debugger > Symbols).

2. Stack traces look the same and don't include the extra information

    Ensure that you are using `ExceptionReporting.GetExceptionReport(ex)` to produce the stack trace. If you are using a logging framework, such as `log4net`, then you might want to customize how the logging framework renders exception stack traces. See above section on how to set that up with `log4net` specifically.

## Additional help

If you have any problems with using the library, you can [create an issue on GitHub][10] (or see if someone else already raised the same one). If it's just a quick question, you can [send me a Tweet][11].

Finally, if you are using it and liking it - share it around. Also [send me a Tweet @LevGimelfarb][12], I'd love to know it was useful!

[10]: https://github.com/gimelfarb/ProductionStackTrace/issues
[11]: http://twitter.com/home?status=@LevGimelfarb+%23prod-stack+q%3f
[12]: http://twitter.com/home?status=@LevGimelfarb+%23prod-stack+
