using System;
using ReMindAst;
using ReMindBackend;
using ReMindParser;

class Program
{
    const string ReMindSourceCode = @"";
static void Main(string[] args)
{
  var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    var caseName = args.Length > 0 && args[0].Equals("variable", StringComparison.OrdinalIgnoreCase)
      ? "variable"
      : args.Length > 0 && args[0].Equals("bubblesort", StringComparison.OrdinalIgnoreCase)
        ? "bubblesort"
        : "helloworld";
  var sourceFilePath = Path.Combine(projectRoot, "sourceFiles", $"{caseName}cs.aoi");
  var source = File.Exists(sourceFilePath)
      ? File.ReadAllText(sourceFilePath)
      : ReMindSourceCode;

  // 1. 字句解析
  var lexer = new Lexer(source);
    var tokens = lexer.Tokenize();

  // 2. 構文解析
    var parser = new Parser(tokens);
    var ast = parser.ParseCompilationUnit();

  // 3. 意味解析
    var resolver = new SemanticResolver(ast);
    resolver.Resolve();

  // 4. 中間表現の構築
    var irBuilder = new IRBuilder();
  var programIR = irBuilder.Build(ast);

  // 5. コード生成（既定ターゲット: C#）
    var generator = new CSharpCodeGenerator();
  string output = generator.Generate(programIR);

  // 6. 標準出力
  Console.WriteLine(output);
  var generatedFilePath = Path.Combine(projectRoot, "transcompiledFiles", $"{caseName}.generated.cs_");
  File.WriteAllText(generatedFilePath, output);

  var expectedFilePath = Path.Combine(projectRoot, "transcompiledFiles", $"{caseName}.cs_");
  var verificationResultPath = Path.Combine(projectRoot, "transcompiledFiles", $"{caseName}.verification.txt");
  if (File.Exists(expectedFilePath))
  {
      CompareGeneratedOutput(generatedFilePath, expectedFilePath);
  }
  File.WriteAllText(verificationResultPath, string.Empty);
}

static void CompareGeneratedOutput(string generatedFilePath, string expectedFilePath)
{
}

}