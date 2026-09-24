using System;
using System.Text;
using ReMindAst;
using ReMindBackend;
using ReMindParser;

class Program
{
  private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

  static void Main(string[] args)
  {
    if (args.Length != 2)
    {
      Console.WriteLine(Messages.E00001);
      return;
    }

    var sourceFilePath = args[0];
    if (!File.Exists(sourceFilePath))
    {
      Console.WriteLine(Messages.E00002 + sourceFilePath);
      return;
    }

    string source;
    try
    {
      source = File.ReadAllText(sourceFilePath, Encoding.UTF8);
    }
    catch (Exception ex)
    {
      Console.WriteLine(Messages.E00003 + ex.Message);
      return;
    }

    var targetFilePath = args[1];
    var targetExtension = Path.GetExtension(targetFilePath);
    if (string.IsNullOrEmpty(targetExtension))
    {
      Console.WriteLine(Messages.E00008);
      return;
    }

    string output;
    try
    {
      var lexer = new Lexer(source);
      var tokens = lexer.Tokenize();
      var parser = new Parser(tokens);
      var ast = parser.ParseCompilationUnit();
      var resolver = new SemanticResolver(ast);
      resolver.Resolve();
      var programIR = new IRBuilder().Build(ast);

      output = targetExtension.ToLowerInvariant() switch
      {
        ".cs_" => new CSharpCodeGenerator().Generate(programIR),
        ".java_" => new JavaCodeGenerator().Generate(programIR),
        ".vb" => new VbNetCodeGenerator().Generate(programIR),
        _ => throw new InvalidOperationException(Messages.E00004)
      };
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"変換エラー: {ex.Message}");
      Environment.ExitCode = 1;
      return;
    }

    try
    {
      var outputDirectory = Path.GetDirectoryName(targetFilePath);
      if (!string.IsNullOrEmpty(outputDirectory))
      {
        Directory.CreateDirectory(outputDirectory);
      }

      File.WriteAllText(targetFilePath, output, Utf8NoBom);

      CompareWithReferenceFile(targetFilePath);
    }
    catch (Exception ex)
    {
      Console.WriteLine(Messages.E00005 + ex.Message);
      return;
    }

    Console.WriteLine(output);
  }

  private static void CompareWithReferenceFile(string targetFilePath)
  {
    var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    var referenceFileName = Path.GetFileName(targetFilePath);
    var referenceFilePath = Path.Combine(projectRoot, "referrenceFiles", referenceFileName);
    if (!File.Exists(referenceFilePath))
    {
      Console.WriteLine($"Reference comparison skipped: {referenceFileName}");
      return;
    }

    var generated = NormalizeForComparison(File.ReadAllText(targetFilePath, Encoding.UTF8));
    var reference = NormalizeForComparison(File.ReadAllText(referenceFilePath, Encoding.UTF8));
    if (string.Equals(generated, reference, StringComparison.Ordinal))
    {
      Console.WriteLine($"Reference comparison passed: {referenceFileName}");
      return;
    }

    Console.WriteLine($"Reference comparison differs: {referenceFileName}");
    if (generated.Contains("未対応", StringComparison.Ordinal) ||
        reference.Contains("未対応", StringComparison.Ordinal) ||
        generated.Contains("'''", StringComparison.Ordinal) != reference.Contains("'''", StringComparison.Ordinal))
    {
      Console.WriteLine($"Comment or unsupported-output difference remains: {referenceFileName}");
    }
  }

  private static string NormalizeForComparison(string content)
  {
    return content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
  }

}