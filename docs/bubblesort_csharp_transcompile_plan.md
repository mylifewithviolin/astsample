# BubbleSort C# トランスコンパイル実行計画

## 1. 目的

`Program.cs` の `ReMindSourceCode` が空文字列になったため、代替入力として次のファイルを読み込み、C# の生成結果を保存できる状態にする。

```text
sourceFiles/bubblesortcs.aoi
    -> Lexer
    -> Parser
    -> SemanticResolver
    -> IRBuilder
    -> CSharpCodeGenerator
    -> transcompiledFiles/bubblesort.generated.cs_
```

BubbleSort の期待ファイルはまだ存在しないため、期待結果との比較検証は行わない。生成された `bubblesort.generated.cs_` をユーザーが目視確認することを完了条件とする。

## 2. 現状確認

### 入力

- `Program.cs` の `const string ReMindSourceCode` は空文字列
- `sourceFiles/bubblesortcs.aoi` は存在する
- BubbleSort の内容は、以前 `ReMindSourceCode` に埋め込まれていたソースを外部ファイル化したもの

### 現在のケース選択

現行の `Program.cs` は次の選択になっている。

- 引数が `variable`: `sourceFiles/variablecs.aoi`
- それ以外: `sourceFiles/helloworldcs.aoi`
- ファイルがない場合: 空の `ReMindSourceCode`

このままでは `bubblesort` 引数を渡しても HelloWorld が選択される。

### 現在の出力

出力ファイル名はケース名から作られるため、ケース名を `bubblesort` にできれば次のファイルを生成できる。

```text
transcompiledFiles/bubblesort.generated.cs_
```

## 3. 必要な最小修正

### 3.1 ケース選択

`Program.cs` のケース選択に `bubblesort` を追加する。

想定する実行方法:

```text
astsample.exe bubblesort
```

または:

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' run --project .\astsample.csproj -- bubblesort
```

ケース選択の結果:

```text
caseName = bubblesort
sourceFilePath = sourceFiles/bubblesortcs.aoi
```

既存ケースは維持する。

- 引数なし: `helloworld`
- `variable`: `variable`
- `bubblesort`: `bubblesort`

`ReMindSourceCode` は空文字列のまま保持し、対象ファイルが存在しない場合のフォールバックとして使用する。今回の BubbleSort 実行ではファイルが存在するため、空文字列は使用されない。

### 3.2 期待ファイル比較の扱い

BubbleSort の期待ファイルは存在しないため、次の条件分岐枠を追加する。

```text
if expectedFilePath exists:
    CompareGeneratedOutput(...)
else:
    comparison is skipped
```

比較ロジック自体は今回の目的では実装しない。BubbleSort 実行時に、存在しない期待ファイルを前提にエラーを発生させないことだけを保証する。

### 3.3 出力

既存の標準出力とファイル出力を維持する。

```text
Console.WriteLine(output)
File.WriteAllText(
    transcompiledFiles/bubblesort.generated.cs_,
    output)
```

出力拡張子は `.cs_` とする。通常の `.cs` にはしない。

## 4. 既存パイプラインの利用

BubbleSort 用に新しい変換経路は追加しない。現在の実行順をそのまま利用する。

1. `sourceFiles/bubblesortcs.aoi` を読み込む
2. `Lexer.Tokenize()` でトークン化する
3. `Parser.ParseCompilationUnit()` で AST を生成する
4. `SemanticResolver.Resolve()` を実行する
5. `IRBuilder.Build(ast)` で IR を生成する
6. `CSharpCodeGenerator.Generate(programIR)` で C# を生成する
7. 標準出力へ出力する
8. `transcompiledFiles/bubblesort.generated.cs_` へ保存する

## 5. 実行前の確認項目

### 入力ファイル

- `sourceFiles/bubblesortcs.aoi` が存在する
- ファイル名が `bubblesortcs.aoi` である
- 実行時のプロジェクトルート解決が正しい
- `Program.cs` の空 `ReMindSourceCode` が入力として選ばれない

### Lexer

BubbleSort 入力に含まれる次の記号を処理できることを確認する。

- `▽`、`△`
- `・`、`□`
- `◇`、`〇`
- `■`、`▼`、`▲`
- 全角空白
- 配列の `[`、`]`
- メンバーアクセスの `.`
- 比較演算子 `<`、`>`
- 数値・文字列・日本語識別子

### Parser / AST

既存の BubbleSort 用 Parser 経路が、少なくとも次の構造を処理できることを確認する。

- namespace
- class
- method
- 配列宣言
- method invocation
- for / while
- if
- assignment
- `Console.WriteLine` の AliasClass

### IR / C# Generator

現在の実装範囲で生成結果が空にならないことを確認する。IRBuilder または C# Generator が BubbleSort 固有の配列・for・代入・比較式で未対応例外を出した場合は、原因を記録して BubbleSort 対応の次工程へ分離する。

## 6. 実装手順

1. `Program.cs` の現在内容を確認する。
2. `caseName` の選択肢に `bubblesort` を追加する。
3. `sourceFiles/bubblesortcs.aoi` を選択できるようにする。
4. BubbleSort 用出力パスを `transcompiledFiles/bubblesort.generated.cs_` にする。
5. 期待ファイルが存在しない場合は比較をスキップする枠を追加する。
6. 既存の Lexer → Parser → SemanticResolver → IRBuilder → CSharpCodeGenerator の順序を維持する。
7. 生成結果を標準出力と `.cs_` ファイルへ出力する。
8. BubbleSort 引数で実行する。

## 7. 検証手順

### ビルド

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' build .\astsample.csproj --no-restore
```

受け入れ条件:

- ビルド成功
- 新規コンパイルエラーなし

### BubbleSort 実行

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' run --no-build --project .\astsample.csproj -- bubblesort
```

確認項目:

1. `bubblesortcs.aoi` が選択される。
2. 空の `ReMindSourceCode` にはフォールバックしない。
3. Lexer の `Unexpected character` が発生しない。
4. Parser が入力処理を開始する。
5. SemanticResolver が実行される。
6. IRBuilder が実行される。
7. CSharpCodeGenerator が実行される。
8. `transcompiledFiles/bubblesort.generated.cs_` が作成される。
9. 標準出力に生成 C# が表示される。
10. 期待ファイル比較はスキップされる。

### 生成ファイル確認

```text
transcompiledFiles/bubblesort.generated.cs_
```

ユーザーが目視で確認する項目:

- namespace / class / method の構造
- C# の修飾子・型・識別子
- 配列宣言
- 代入文
- if / while / for
- `Console.WriteLine`
- XML コメント
- インデントと空行

## 8. 完了条件

- `sourceFiles/bubblesortcs.aoi` を引数 `bubblesort` で選択できる。
- 空の `ReMindSourceCode` に依存せず実行できる。
- 既存のコンパイルパイプラインを順番どおり通過する。
- `transcompiledFiles/bubblesort.generated.cs_` が生成される。
- 標準出力へ生成結果が出力される。
- 存在しない期待ファイルを比較しない。
- 期待ファイルを用いた検証は実施しない。
- 生成ファイルをユーザーが目視確認できる。

## 9. 対象外

今回の実行プランでは次を行わない。

- BubbleSort の期待ファイル作成
- 期待結果との自動比較
- BubbleSort 生成 C# の完全な意味検証
- Parser / IR / CodeGenerator の全面改修
- Java / VB.NET の生成
- `ReMindSourceCode` へのソース再埋め込み
