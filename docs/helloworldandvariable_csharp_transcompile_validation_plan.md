# HelloWorld / Variable C# トランスコンパイル修正・検証計画

## 1. 目的

次の 2 つの Re:Mind 入力を、それぞれ対応する期待 C# 出力へ変換できる状態にする。

| 入力 | 期待結果 |
|---|---|
| `sourceFiles/helloworldcs.aoi` | `transcompiledFiles/helloworld.cs_` |
| `sourceFiles/variablecs.aoi` | `transcompiledFiles/variable.cs_` |

対象言語は C# とする。2 ケースを同じ Lexer → Parser → AST → SemanticResolver → IR → C# CodeGenerator のパイプラインで検証する。

```text
ReMind source file
  -> Lexer
  -> Parser
  -> AST
  -> SemanticResolver
  -> IRBuilder
  -> ProgramIR
  -> CSharpCodeGenerator
  -> .cs_ output
```

## 2. 入力と期待結果の差分

### 2.1 共通する構文

両方の入力で次の変換が必要になる。

- `▽名前空間 HelloWorld`
- `▽public クラス プログラム型`
- Javadoc コメント
- `▽static void メイン(string[] 引数)`
- `△` によるメソッド・クラス・名前空間終了
- `■インポートする System`
- `▼...▼` による `Console` AliasClass
- `コンソール.一行表示する(...)` → `Console.WriteLine(...)`
- 日本語名から C# 名への解決
- `using System;`、namespace、class、method の C# 出力

### 2.2 HelloWorld 固有の構文

`helloworldcs.aoi` は、Main メソッド内の 1 つの呼び出しを含む最小ケースである。

```text
□コンソール.一行表示する("Hello World!")
```

期待する主要出力:

```csharp
Console.WriteLine("Hello World!");
```

### 2.3 Variable 固有の構文

`variablecs.aoi` は HelloWorld の拡張ケースであり、次を含む。

- クラスフィールド宣言
- `private static string?` の型と修飾子
- フィールド名 `挨拶１` の Javadoc による `aisatsu1` 解決
- `□挨拶１="Hello World one!"` の代入
- `□コンソール表示する(挨拶１)` のユーザー定義メソッド呼び出し
- `▽static void コンソール表示する(string 引数2)` のメソッド宣言
- Javadoc `@param 引数2 dispStr` に基づくパラメーター名解決
- ユーザー定義メソッド内の `Console.WriteLine(args2)` 生成

期待する主要出力:

```csharp
private static string? aisatsu1;
aisatsu1="Hello World one!";
ConsoleOut(aisatsu1);
static void ConsoleOut(string args2)
{
    Console.WriteLine(args2);
}
```

期待ファイルにはコメントと空行・インデントも含まれるため、意味的な一致と整形を分けて検証する。

## 3. 現在の実装との差分と修正方針

### 3.1 入力選択

`Program.cs` は検証対象ファイルを優先して読み込む。入力ケースを固定せず、検証時に次の 2 つを切り替えられる構造にする。

1. `sourceFiles/helloworldcs.aoi`
2. `sourceFiles/variablecs.aoi`

既存の `const string ReMindSourceCode` は互換用に保持する。ファイルが存在しない場合のみフォールバックに使用する。

生成ファイル名は次の規則にする。

- HelloWorld: `transcompiledFiles/helloworld.generated.cs_`
- Variable: `transcompiledFiles/variable.generated.cs_`

既存の標準出力とファイル出力は維持する。検証結果ファイルの出力枠を設ける場合も、生成処理そのものと分離する。

### 3.2 Lexer

構造記号と入力文字を両ケースでトークン化する。

- `▽` / `△`
- `・` / `□`
- `■` / `▼` / `▲`
- `.` / `(` / `)` / `[` / `]` / `=` / `,`
- `?`、`:` などの型・宣言記号
- 日本語識別子
- 識別子継続文字の数字・`_`
- 全角空白
- 文字列リテラル

受け入れ条件:

- 2 入力とも `Unexpected character` が発生しない。
- `引数2`、`挨拶１` を 1 識別子として取得できる。
- `string?` を型情報として失わない。
- `"Hello World!"` を 1 つの文字列リテラルとして取得する。

### 3.3 Parser / AST

Parser は宣言と文を別の層で処理する。HelloWorld 用の最小解析に加え、Variable 用のフィールドと代入を追加する。

#### 宣言

- namespace
- class
- field variable declaration
- method declaration
- parameter
- import / AliasClass / AliasMember

#### 文

- invocation statement
- assignment statement
- 文字列リテラル
- 識別子式

#### 名前情報

AST では日本語名と C# 名を保持する。新しい固定マップを増やすのではなく、次の優先順位で解決する。

1. Javadoc の名前
2. AliasClass / AliasMember
3. SemanticResolver のシンボル情報
4. 未解決時の元名

主な期待解決:

| Re:Mind | C# |
|---|---|
| `プログラム型` | `Program` |
| `メイン` | `Main` |
| `引数` | `args` |
| `挨拶１` | `aisatsu1` |
| `引数2` | `args2` |
| `コンソール表示する` | `ConsoleOut` |
| `コンソール.一行表示する` | `Console.WriteLine` |

### 3.4 SemanticResolver

2 ケース共通で、次を解決する。

- namespace / class / method / field / parameter
- Javadoc の日本語名と英語名
- `Console` AliasClass
- `WriteLine` AliasMember
- フィールド `挨拶１` の宣言・使用
- `コンソール表示する` のメソッド呼び出し
- `引数2` のパラメーター解決

Variable ケースでは、クラスフィールドをローカル変数と混同しない。宣言されたフィールドの型・修飾子・英語名を後続 IR へ渡す。

### 3.5 IR 設計

`ProgramIR`、`ClassIR`、`MethodIR`、`BlockIR`、`StatementIR`、`ExpressionIR` を共通利用する。

Variable ケースを表現するため、必要な情報を次のように保持する。

```text
ProgramIR
  Namespaces: HelloWorld
  Imports: System
  Classes:
    ClassIR
      Name: Program
      Fields:
        private static string? aisatsu1
      Methods:
        MethodIR Main
          Parameters: string[] args
          Body:
            AssignmentIR
              Target: aisatsu1
              Value: LiteralIR("Hello World one!")
            CallIR
              MethodName: ConsoleOut
              Arguments: IdentifierIR(aisatsu1)
        MethodIR ConsoleOut
          Parameters: string args2
          Body:
            CallIR
              MethodName: Console.WriteLine
              Arguments: IdentifierIR(args2)
```

IR には次のメタ情報の保持枠を用意する。

- コメント
- Javadoc / `@param` 情報
- 修飾子
- 型名
- 整形情報

ただし、枠とデータ受け渡しを先に整え、整形ロジックは別検証単位として扱う。

### 3.6 IRBuilder

`IRBuilder.Build(CompilationUnit)` で、2 ケースの共通構造を変換する。

1. imports を `ProgramIR.Imports` に追加
2. namespace を `ProgramIR.Namespaces` に追加
3. class を `ClassIR` に変換
4. field を `ClassIR.Fields` に追加
5. method と parameter を `MethodIR` に変換
6. assignment を `AssignmentIR` に変換
7. invocation を `CallIR` または `CallExpressionIR` に変換
8. literal / identifier / member access を `ExpressionIR` に変換

`CallIR` と `CallExpressionIR` の二重表現を放置せず、式文・代入式それぞれでどちらを使うかを固定する。

### 3.7 CSharpCodeGenerator

C# 生成器は HelloWorld と Variable の双方を同じ IR 経路で処理する。

#### Program / class / method

- `using System;`
- namespace
- `public class Program`
- field 修飾子・型・名前
- method 修飾子・戻り値・名前・parameters
- body の順序保持

#### Statement / expression

- Variable declaration
- Assignment: `左辺 = 右辺;`
- Call: `対象(引数);`
- `Console.WriteLine`
- string literal
- identifier
- member access

#### コメント・整形

- IR のコメント枠を参照するフックを用意する。
- `IndentWriter` のインデント制御を使う。
- 空行・コメント・末尾改行の扱いは、意味生成と分離して調整する。
- 完全一致を必要とする場合は、期待ファイルの実際の空白規則に合わせて決定する。

## 4. 検証フェーズ

### Phase 1: 入力ケース切り替え

HelloWorld と Variable を個別に選択して実行できるようにする。入力ファイル名と出力ファイル名を同じケース識別子から導出する。

確認:

- HelloWorld が `helloworldcs.aoi` を読む。
- Variable が `variablecs.aoi` を読む。
- `ReMindSourceCode` はフォールバックとして残る。
- 生成結果は `.cs_` で保存される。

### Phase 2: Lexer

各入力を Tokenize し、次を確認する。

- 先頭から EOF まで例外がない。
- 構造記号、型、修飾子、名前、演算子、文字列が欠落しない。
- `挨拶１`、`引数2` の文字列境界が正しい。

### Phase 3: AST

HelloWorld:

- namespace / class / Main / Console.WriteLine が存在する。

Variable:

- field `挨拶１`
- assignment `挨拶１ = "Hello World one!"`
- method `コンソール表示する`
- parameter `引数2`
- Console.WriteLine invocation

が存在することを確認する。

### Phase 4: SemanticResolver

- 日本語名から C# 名への解決結果をログまたは検証用データで確認する。
- Console Alias が両ケースで `Console.WriteLine` になる。
- Variable のフィールド・メソッド・パラメーター参照が未定義にならない。

### Phase 5: IR

- `ProgramIR` が空でない。
- HelloWorld の `Main` に CallIR が 1 件ある。
- Variable の `ClassIR` に field が 1 件以上ある。
- Variable の Main に AssignmentIR と CallIR が順序どおりある。
- ConsoleOut に Console.WriteLine の CallIR がある。

### Phase 6: C# 生成

HelloWorld の必須断片:

```text
using System;
namespace HelloWorld
public class Program
static void Main(string[] args)
Console.WriteLine("Hello World!");
```

Variable の必須断片:

```text
private static string? aisatsu1;
aisatsu1="Hello World one!";
ConsoleOut(aisatsu1);
static void ConsoleOut(string args2)
Console.WriteLine(args2);
```

### Phase 7: 期待ファイル比較

各ケースについて、次の対応で比較する。

| 生成物 | 期待物 |
|---|---|
| `helloworld.generated.cs_` | `helloworld.cs_` |
| `variable.generated.cs_` | `variable.cs_` |

比較順:

1. 出力ファイルを保存
2. 期待ファイルを読み込み
3. CRLF / LF を統一
4. 行末空白を除去
5. 必要なら連続空行を正規化
6. 差分を記録
7. 意味差分と整形差分を分類

完全一致の前に、次を満たすことを確認する。

- 生成 C# がコンパイル可能
- HelloWorld の実行結果が `Hello World!`
- Variable の実行結果が期待する出力になる
- 名前・型・呼び出し先が期待結果と一致

## 5. 検証コマンド案

PowerShell の PATH に `dotnet` がない環境では絶対パスを使用する。

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' build .\astsample.csproj --no-restore
& 'C:\Program Files\dotnet\dotnet.EXE' run --project .\astsample.csproj
```

ケースごとの検証では、入力ケースを選択して次を実行する。

```text
1. HelloWorld input -> helloworld.generated.cs_
2. Variable input -> variable.generated.cs_
3. Normalize and compare against corresponding .cs_
4. Compile generated C# in a temporary project
5. Run generated C# and compare runtime output
```

生成された `.cs_` は通常の C# 拡張子ではないため、独立コンパイル時には一時的に `.cs` へコピーする。プロジェクト自身へ生成 C# を自動収集させないよう、`transcompiledFiles/**/*.cs_` を検証対象として管理する。

## 6. 回帰検証

HelloWorld / Variable の両方が通過した後に、既存ケースを確認する。

- `ifthenelsecs.aoi` → `ifthenelse.cs_`
- `variablejv.aoi` は Java 対象のため C# 受け入れ条件から除外
- BubbleSort の `ReMindSourceCode`
- Lexer の `□`、`◇`、`〇`、配列、比較演算子

回帰時は、HelloWorld 用のトークン Parser 経路と既存の BubbleSort 用経路が入力形式に応じて競合しないことを確認する。

## 7. 完了条件

### 共通

- 2 入力とも Lexer で例外が発生しない。
- 2 入力とも Parser → AST → SemanticResolver → IRBuilder → CSharpCodeGenerator を通過する。
- `ProgramIR` が空でない。
- C# 生成結果が `.cs_` として保存される。
- 生成 C# がコンパイル可能である。

### HelloWorld

- `Console.WriteLine("Hello World!");` が生成される。
- `helloworld.cs_` と比較し、意味的に一致する。
- 必要ならコメント・空白・インデントを含めて完全一致する。

### Variable

- `private static string? aisatsu1;` が生成される。
- `aisatsu1="Hello World one!";` が生成される。
- `ConsoleOut(aisatsu1);` が生成される。
- `static void ConsoleOut(string args2)` が生成される。
- `Console.WriteLine(args2);` が生成される。
- `variable.cs_` と比較し、意味的に一致する。
- 必要ならコメント・空白・インデントを含めて完全一致する。

## 8. 実装上の注意

- HelloWorld 用の最小実装だけで Variable を通過させようとせず、field・assignment・user-defined call を明示的な段階として追加する。
- `MapIdentifierName` の新規固定マップを増やさず、Javadoc と Symbol Table を名前解決の情報源にする。
- `Console.WriteLine` の Alias 解決と `コンソール表示する` の user-defined method 解決を分ける。
- `string?` は C# の nullable 型として保持し、`string` へ潰さない。
- 期待ファイルとの差分は、コード構造・名前・型・呼び出し先と、コメント・空白・インデントに分類する。
- 生成ファイルをプロジェクトのコンパイル対象に混入させない。
- 実装完了後は、2 ケースを同じ検証手順で実行し、片方だけの成功を完了扱いにしない。
