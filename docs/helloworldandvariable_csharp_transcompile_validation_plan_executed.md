# HelloWorld / Variable C# トランスコンパイル計画 実行レポート

## 1. 実行日

2026-09-20

## 2. 実施した修正

計画に沿って、HelloWorld と Variable の 2 ケースを同じ Lexer → Parser → AST → SemanticResolver → IRBuilder → CSharpCodeGenerator の経路で処理できるようにした。

### 入力ケース切り替え

`Program.cs` にケース選択を追加した。

- 引数なし: `sourceFiles/helloworldcs.aoi`
- `variable` 引数あり: `sourceFiles/variablecs.aoi`
- 対応ファイルがない場合は既存の `ReMindSourceCode` を使用

出力ファイル:

- `transcompiledFiles/helloworld.generated.cs_`
- `transcompiledFiles/variable.generated.cs_`

### Parser / AST

Variable に必要な最小範囲を追加した。

- `ClassDeclaration.Fields`
- クラスフィールド宣言
- `□変数 = 式` の代入文
- `挨拶１` → `aisatsu1`
- `引数2` → `args2`
- `コンソール表示する` → `ConsoleOut`
- 文字列・数値リテラル

### IRBuilder

- `ClassIR.Fields` へのフィールド変換
- `AssignmentIR` への代入変換
- `CallIR` への式文呼び出し変換
- フィールド、メソッド、パラメーターの IR への受け渡し

### CSharpCodeGenerator

- `ClassIR.Fields` の C# フィールド出力
- Assignment / Call の C# 文生成
- HelloWorld / Variable 共通の namespace、class、method 生成

## 3. ビルド結果

実行したビルド:

```text
C:\Program Files\dotnet\dotnet.EXE build C:\developments\cs12\astsample/astsample.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary;ForceNoAlign
```

結果:

- 成功
- 0 エラー
- 既存の nullable 警告は残存

## 4. 実行結果

### HelloWorld

実行:

```text
C:\Program Files\dotnet\dotnet.EXE run --no-build --project C:\developments\cs12\astsample/astsample.csproj
```

生成ファイル:

`transcompiledFiles/helloworld.generated.cs_`

主要出力:

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

### Variable

実行:

```text
C:\Program Files\dotnet\dotnet.EXE run --no-build --project C:\developments\cs12\astsample/astsample.csproj -- variable
```

生成ファイル:

`transcompiledFiles/variable.generated.cs_`

主要出力:

```csharp
using System;

namespace HelloWorld
{
    public class Program
    {
        private static string? aisatsu1;

        static void Main(string[] args)
        {
            aisatsu1 = "Hello World one!";
            ConsoleOut(aisatsu1);
        }

        static void ConsoleOut(string args2)
        {
            Console.WriteLine(args2);
        }
    }
}
```

## 5. 期待結果との評価

### HelloWorld

意味上の主要構造は一致した。

- `using System;`
- `namespace HelloWorld`
- `public class Program`
- `static void Main(string[] args)`
- `Console.WriteLine("Hello World!");`

未一致:

- クラス・メソッドの XML コメントが未出力
- 期待ファイルと空行・インデントが異なる

### Variable

意味上の主要構造は一致した。

- `private static string? aisatsu1;`
- `aisatsu1 = "Hello World one!";`
- `ConsoleOut(aisatsu1);`
- `static void ConsoleOut(string args2)`
- `Console.WriteLine(args2);`

未一致:

- クラス・フィールド・メソッドの XML コメントが未出力
- `@param` コメントが未出力
- 期待ファイルの `aisatsu1="Hello World one!";` と生成結果の空白が異なる
- 空行・インデントが期待ファイルと異なる

## 6. 検証ファイル

次の検証結果ファイルは作成されたが、`CompareGeneratedOutput` が空実装のため内容は空である。

- `transcompiledFiles/helloworld.verification.txt`
- `transcompiledFiles/variable.verification.txt`

したがって、実ファイルの存在と生成内容は確認できたが、プログラムによる完全一致判定はまだ実施していない。

## 7. 完了判定

| 項目 | 結果 |
|---|---|
| HelloWorld 入力の Lexer 通過 | 完了 |
| Variable 入力の Lexer 通過 | 完了 |
| HelloWorld の Parser / AST | 最小範囲完了 |
| Variable の field / assignment / call | 最小範囲完了 |
| HelloWorld の IR → C# | 完了 |
| Variable の IR → C# | 完了 |
| `.cs_` 生成ファイル作成 | 完了 |
| ビルド | 成功 |
| 期待ファイルとの完全一致 | 未完了 |
| 比較ロジックの実行 | 未完了。比較メソッドは空実装 |
| コメントの完全再現 | 未完了 |
| 空白・インデントの完全再現 | 未完了 |

## 8. 残課題

1. Javadoc と `@param` を AST から IR へ渡す。
2. `ClassIR` / `MethodIR` / field IR にコメント情報と修飾子を保持する。
3. `CSharpCodeGenerator` でコメントを XML コメントへ変換する。
4. 期待ファイルの空行・インデント・代入演算子周辺の空白規則を整形情報として扱う。
5. `CompareGeneratedOutput` に正規化比較または完全比較を実装する。
6. 生成 C# を一時プロジェクトでコンパイルし、実行結果を確認する。
7. 比較結果を `helloworld.verification.txt` と `variable.verification.txt` に書き出す。

## 9. 結論

2 ケースの最小トランスコンパイル経路は実行でき、主要な C# 構造と Variable 固有のフィールド・代入・ユーザー定義メソッド呼び出しを生成できた。一方、期待ファイルとの完全一致は、コメント・整形情報・比較ロジックが未実装のため未達である。
