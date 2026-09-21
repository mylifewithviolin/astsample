# XML コメント残課題実行計画

## 1. 目的

`docs/xml_comment_transcompile_plan_executed.md` の残課題 1 を実行する。

> コメント概要に `NameJa` を使う

対象は現在の IR ベース C# 生成経路とし、次の期待結果に合わせる。

- クラス: `プログラム型`
- フィールド: `挨拶１`
- Main メソッド: `メイン`
- ConsoleOut メソッド: `コンソール表示する`

期待する XML コメント例:

```csharp
/// <summary>プログラム型</summary>
/// <summary>挨拶１</summary>
/// <summary>メイン</summary>
/// <summary>コンソール表示する</summary>
```

## 2. 現状と原因

現在の `DocumentationIR` は `Summary` と `Parameters` を保持している。`IRBuilder.BuildDocumentation()` は `JavadocComment.Summary` を `Summary` にコピーし、`CSharpCodeGenerator.GenerateDocumentation()` はその値を出力している。

そのため、Javadoc の概要に書かれた英語名が出力される。

```text
Javadoc Summary -> DocumentationIR.Summary -> <summary>
```

例:

```text
Program -> <summary>Program</summary>
aisatsu1 -> <summary>aisatsu1</summary>
Main -> <summary>Main</summary>
```

期待結果は対象宣言の AST の日本語名を使うため、`NameJa` をコメント情報へ渡す必要がある。

```text
ClassDeclaration.NameJa -> DocumentationIR.NameJa
FieldDeclaration.NameJa -> DocumentationIR.NameJa
MethodDeclaration.NameJa -> DocumentationIR.NameJa
```

## 3. 修正方針

変更は IR のコメント情報受け渡しと C# Generator の出力選択に限定する。

- Lexer のコメント保持処理は変更しない。
- Parser の Javadoc 関連付け処理は変更しない。
- 名前解決ロジックは変更しない。
- `JavadocComment.Summary` は `@param` 以外のコメント情報として保持する。
- XML の `<summary>` だけ `NameJa` を優先して出力する。
- 既存の `<param>` 出力は変更しない。
- Java / VB.NET Generator は今回の対象外とする。

## 4. 必要な最小差分

### 4.1 DocumentationIR

`IR.cs` の `DocumentationIR` に日本語名を保持するプロパティを追加する。

```csharp
public string NameJa { get; set; } = "";
public string? Summary { get; set; }
```

`Summary` は既存 API と将来の用途のため残す。今回の C# XML `<summary>` 生成では `NameJa` を使う。

### 4.2 IRBuilder

`BuildDocumentation()` の引数を次のように拡張する。

```text
BuildDocumentation(JavadocComment? documentation, string nameJa)
```

生成する `DocumentationIR`:

- `NameJa = nameJa`
- `Summary = documentation?.Summary`
- `Params` は既存どおり `NameEn` と `NameJa` をコピー

各呼び出しを対象宣言名に合わせて変更する。

| AST | IR documentation | 渡す名前 |
|---|---|---|
| `ClassDeclaration` | `ClassIR.Documentation` | `cls.NameJa` |
| `FieldDeclaration` | `FieldIR.Documentation` | `field.NameJa` |
| `MethodDeclaration` | `MethodIR.Documentation` | `method.NameJa` |

コメントがない場合は、既存と同じく `null` を返す。空の documentation を無条件に生成しない。

### 4.3 CSharpCodeGenerator

`GenerateDocumentation()` の `<summary>` 出力を変更する。

優先順位:

1. `DocumentationIR.NameJa` が空でない場合は `NameJa`
2. `NameJa` が空の場合だけ既存の `Summary`
3. 両方空の場合は `<summary>` を出力しない

出力例:

```text
NameJa = プログラム型
Summary = Program
```

```csharp
/// <summary>プログラム型</summary>
```

`<param>` の出力は既存の `Parameters` をそのまま使用する。

## 5. 実装手順

1. `IR.cs` の `DocumentationIR` に `NameJa` を追加する。
2. `IRBuilder.BuildDocumentation()` を nameJa 受け取りに変更する。
3. クラス、フィールド、メソッドから日本語名を渡す。
4. `CSharpCodeGenerator.GenerateDocumentation()` の summary 値選択だけを変更する。
5. 既存の Javadoc Token 化・Parser 関連付けは変更しない。

## 6. 検証計画

### 6.1 ビルド

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' build .\astsample.csproj --no-restore
```

受け入れ条件:

- コンパイルエラー 0 件
- 既存 nullable 警告以外の新規警告を増やさない

### 6.2 HelloWorld

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' run --no-build --project .\astsample.csproj
```

確認対象:

- `transcompiledFiles/helloworld.generated.cs_`
- `/// <summary>プログラム型</summary>`
- `/// <summary>メイン</summary>`
- `/// <param name="args">引数</param>`
- `Program`、`Main`、`Console.WriteLine` の英語コード名は変更されない

### 6.3 Variable

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' run --no-build --project .\astsample.csproj -- variable
```

確認対象:

- `transcompiledFiles/variable.generated.cs_`
- `/// <summary>プログラム型</summary>`
- `/// <summary>挨拶１</summary>`
- `/// <summary>メイン</summary>`
- `/// <summary>コンソール表示する</summary>`
- `/// <param name="args">引数</param>`
- `/// <param name="dispStr">引数2</param>`
- フィールド `aisatsu1`、メソッド `ConsoleOut`、`Console.WriteLine` は従来どおり英語名で出力される

### 6.4 期待ファイルとの差分

比較対象:

- `transcompiledFiles/helloworld.generated.cs_` と `transcompiledFiles/helloworld.cs_`
- `transcompiledFiles/variable.generated.cs_` と `transcompiledFiles/variable.cs_`

まずコメント行だけを比較し、次にコード本体と空白・インデントを分類する。

コメント比較の完了条件:

- クラス、フィールド、メソッドの summary が `NameJa` と一致する。
- `@param` の name と説明が期待結果と一致する。
- 英語の Javadoc Summary が summary に出力されない。

## 7. 回帰確認

- コメントがない宣言で空の XML コメントが出ない。
- HelloWorld の文字列出力が変わらない。
- Variable のフィールド、代入、`ConsoleOut` 呼び出しが変わらない。
- `NameEn` による C# 識別子生成が `NameJa` の採用によって壊れない。
- AST 直生成経路の既存 `WriteXmlComment` は変更しない。

## 8. 完了条件

- `DocumentationIR` が対象宣言の `NameJa` を保持する。
- C# Generator が `NameJa` を XML `<summary>` に出力する。
- HelloWorld と Variable の両方で日本語 summary が生成される。
- `@param` 出力が維持される。
- ビルドが成功する。
- 生成 C# のコード識別子は従来どおり英語名である。
- 残課題 1「コメント概要に NameJa を使う」が完了する。

## 9. 対象外

今回の計画では次を実装しない。

- コメント比較ロジックの実装
- 空白・インデントの完全一致対応
- Java / VB.NET の XML/Javadoc コメント仕様
- 新しい名前解決ルール
- Lexer / Parser の再設計
