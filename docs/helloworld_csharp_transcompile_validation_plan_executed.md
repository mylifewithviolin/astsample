# HelloWorld C# トランスコンパイル計画 実行レポート

## 1. 実行日

2026-09-06

## 2. 実施した修正

計画に沿って、HelloWorld の最小パイプラインを実装した。

### 入力

`Program.cs` は、次の優先順位で入力を選択するように変更した。

1. `sourceFiles/helloworldcs.aoi`
2. ファイルが存在しない場合は既存の `const string ReMindSourceCode`

実行ディレクトリに依存しないよう、`AppContext.BaseDirectory` からプロジェクトルートを解決する構成にした。

### Parser

HelloWorld に必要な最小構文を Token 列から AST に変換する経路を追加した。

- `▽名前空間 HelloWorld`
- `▽public クラス プログラム型`
- `▽static void メイン(string[] 引数)`
- `□コンソール.一行表示する("Hello World!")`
- `△` によるメソッド・クラス終端
- Console の AliasClass / AliasMember
- 文字列・数値リテラル
- `プログラム型` → `Program`
- `メイン` → `Main`
- `引数` → `args`
- `コンソール.一行表示する` → `Console.WriteLine`

既存の BubbleSort 用の文字列ベース Parser 実装は残している。

### IRBuilder

`IRBuilder.Build(CompilationUnit)` を実装した。

- `CompilationUnit` → `ProgramIR`
- namespace → `ProgramIR.Namespaces`
- import → `ProgramIR.Imports`
- class → `ClassIR`
- method → `MethodIR`
- invocation statement → `CallIR`
- string literal → C# 文字列リテラルを保持する `LiteralIR`

### CSharpCodeGenerator

IR 用の最小生成経路を実装した。

- `ProgramIR` → `using` / `namespace`
- `ClassIR` → `public class`
- `MethodIR` → `static returnType name(parameters)`
- `CallIR` → `対象(引数);`
- `Console.WriteLine` 呼び出しの生成
- `LiteralIR` / `IdentifierIR` / `CallExpressionIR` の式生成

### 検証用出力

生成結果を `transcompiledFiles/helloworld.generated.cs` に保存する処理を追加した。

## 3. ビルド結果

実行したビルド:

```text
C:\PROGRA~1\dotnet\dotnet.EXE build C:\developments\cs12\astsample\astsample.csproj --no-restore
```

結果:

- ビルド成功
- `astsample.dll` を生成
- Parser.cs / IRBuilder.cs / Program.cs にエラーなし
- 既存の nullable 警告は残存

主な既存警告:

- `CSharpCodeGenerator._aliasMap` の nullable 警告
- `SemanticResolver.cs` の nullable 警告
- Java / VB.NET / C# の既存式生成箇所の nullable 警告

## 4. 実行結果

実行コマンド:

```text
C:\PROGRA~1\dotnet\dotnet.EXE run --no-build --project C:\developments\cs12\astsample\astsample.csproj
```

実行時の例外は発生しなかった。ただし、この環境の端末連携では標準出力が空として返り、検証用の `transcompiledFiles/helloworld.generated.cs` も作成されなかった。

そのため、次の項目は実行環境上確認できなかった。

- 標準出力された C# ソース
- `helloworld.generated.cs` の実ファイル内容
- 期待ファイルとのテキスト差分
- 生成された C# の独立コンパイル
- 生成プログラムの実行結果 `Hello World!`

## 5. 期待結果との評価

現時点でコード上、少なくとも次の構造を生成する経路は追加されている。

```csharp
using System;

namespace HelloWorld
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}
```

ただし、`transcompiledFiles/helloworld.cs_` に含まれる次の要素は、今回の最小 IR にまだ保持していない。

- クラス Javadoc からの XML コメント
- メソッド Javadoc からの XML コメント
- `@param` コメント
- 期待ファイルと同じ空行・インデントの完全一致

したがって、期待ファイルとの完全一致は未達である。現在の実装は HelloWorld の意味的な最小生成を目指した段階であり、完全な期待出力互換ではない。

## 6. 完了判定

| 項目 | 結果 |
|---|---|
| HelloWorld 入力の Lexer 経路 | 修正済み |
| Parser の最小宣言・呼び出し解析 | 実装済み |
| SemanticResolver 呼び出し | 実行フローに存在 |
| AST → IR | 最小 HelloWorld 範囲を実装 |
| IR → C# | 最小 HelloWorld 範囲を実装 |
| ビルド | 成功 |
| 実行例外なし | 確認済み |
| 標準出力の確認 | 端末連携上、未確認 |
| 生成ファイルの確認 | 未生成または端末連携上、未確認 |
| 期待ファイルとの完全一致 | 未達 |
| 生成 C# のコンパイル・実行 | 未確認 |

## 7. 残課題

1. 実行環境で `helloworld.generated.cs` が作成されることを確認する。
2. 生成結果を `transcompiledFiles/helloworld.cs_` と比較する。
3. Javadoc と `@param` を AST から IR へ移す。
4. `ClassIR` / `MethodIR` に修飾子・コメント情報を追加するか、既存設計に合わせて生成器へ渡す。
5. 空行・インデントを期待ファイルに合わせる。
6. 生成 C# を一時プロジェクトでコンパイルする。
7. 生成プログラムを実行し、`Hello World!` を確認する。
8. `variablecs.aoi`、`ifthenelsecs.aoi`、BubbleSort を回帰確認する。

## 8. 結論

ビルド可能な最小 HelloWorld 変換経路の実装までは完了した。一方、期待ファイルとの完全一致と生成 C# の実行結果は、端末連携で生成物を取得できなかったため完走確認できていない。また、コメントと整形情報を IR に移していないため、現実装のままでは期待ファイルとの完全一致には到達しない。
