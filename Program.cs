using System;
using System.Text;
using ReMindAst;
using ReMindBackend;
using ReMindParser;

class Program
{
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

    var lexer = new Lexer(source);
    var tokens = lexer.Tokenize();
    var parser = new Parser(tokens);
    var ast = parser.ParseCompilationUnit();
    var resolver = new SemanticResolver(ast);
    resolver.Resolve();
    var programIR = new IRBuilder().Build(ast);

    string output;
    switch (targetExtension.ToLowerInvariant())
    {
      case ".cs_":
        output = new CSharpCodeGenerator().Generate(programIR);
        break;
      case ".java":
        output = new JavaCodeGenerator().Generate(programIR);
        break;
      case ".vb":
        output = new VbNetCodeGenerator().Generate(programIR);
        break;
      default:
        Console.WriteLine(Messages.E00004);
        return;
    }

    try
    {
      var outputDirectory = Path.GetDirectoryName(targetFilePath);
      if (!string.IsNullOrEmpty(outputDirectory))
      {
        Directory.CreateDirectory(outputDirectory);
      }

      File.WriteAllText(targetFilePath, output, Encoding.UTF8);
    }
    catch (Exception ex)
    {
      Console.WriteLine(Messages.E00005 + ex.Message);
      return;
    }

    Console.WriteLine(output);
  }

}