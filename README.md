# ProductionStackTrace

[Don't want to deploy PDBs][2], but still get exception stack traces that map to original source line numbers? Want to [have your cake and eat it too](http://en.wikipedia.org/wiki/You_can't_have_your_cake_and_eat_it)?

[ProductionStackTrace][1] is intended to **run in Production**, **without any access to PDB symbol files**. It produces a stack trace with enough information to be analyzed by the techies back on the base with a **help of a [Symbol Store](http://msdn.microsoft.com/en-us/library/windows/desktop/ms680693\(v=vs.85\).aspx)** to get original source code line numbers.

[1]: https://www.nuget.org/packages/ProductionStackTrace
[2]: http://www.lionhack.com/2014/01/14/advanced-dotnet-debugging-pdbs-and-symbols/

## Usage

1. Install the **ProductionStackTrace** [NuGet package][1]
   `PM> Install-Package ProductionStackTrace`

2. In your code, where you log exceptions:
   
   <pre><code language="csharp">
    <b>using ProductionStackTrace;</b>
    ...
    try
    {
        ...
    }
    catch (Exception ex)
    {
        var trace = <b>ExceptionReporting.GetExceptionReport(ex);</b>
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


## Analyzing

Analyzing these stack traces is simple with an associated _analyzer_ application.

1. Install the **ProductionStackTrace Analyze Tools** [NuGet package](https://www.nuget.org/packages/ProductionStackTrace.Analyze.Console) - it's a solution-level tools package:<br/>
`PM> Install-Package ProductionStackTrace.Analyze.Console`

2. This will add a PowerShell command to the Package Manager Console - to launch it:<br/>
`PM> ProductionStackTrace-Analyze`

Copy-paste the stack trace into the window, to get the converted stack trace with original source line mappings:

<pre>
System.Exception: Test exception
   at ProductionStackTrace.Test.TestExceptionReporting.TestSimpleException() in ..\ProductionStackTrace.Test\TestExceptionReporting.cs:line 23
</pre>

You can also convert the entire log file:

`PM> ProductionStackTrace-Analyze <logfile> <outfile>`

## Embedding

If you want to embed analyzing into your own automated process, you can just get the binary files under `tools` folder in the extracted package folder.

Alternatively, there is a **ProductionStackTrace Analyze** NuGet package, which contain the library which performs the conversion. 