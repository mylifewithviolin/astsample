# HelloWorld C# トランスコンパイル修正・検証計画

## 1. 目的

`sourceFiles/helloworldcs.aoi` を Re:Mind 入力として処理し、`transcompiledFiles/helloworld.cs_` と同等の C# ソースを標準出力または検証用ファイルへ生成できる状態にする。

対象パイプライン:

```text
ReMind source
  -> Lexer
  -> Parser
  -> AST
  -> SemanticResolver
  -> IRBuilder
  -> ProgramIR
  -> CSharpCodeGenerator
  -> C# source
```

対象言語は C# とする。Java / VB.NET の生成結果は今回の受け入れ条件に含めない。

## 2. 重要な前提差分

### 2.1 入力ソースの取り込み方法

`sourceFiles/helloworldcs.aoi` は `HelloWorld` 名前空間、`プログラム型` クラス、`メイン` メソッド、`コンソール.一行表示する("Hello World!")` を含む最小サンプルである。

一方、現在の `Program.cs` の `ReMindSourceCode` は BubbleSort サンプルであり、`helloworldcs.aoi` と等価ではない。最初の修正では、固定文字列を二重管理しないため、検証対象ファイルを読み込む入口を用意するか、少なくとも `ReMindSourceCode` を HelloWorld サンプルと一致させる必要がある。

推奨する実装順は次のとおり。

1. 検証時は `sourceFiles/helloworldcs.aoi` を入力にできるようにする。
2. 既存の `const string ReMindSourceCode` は互換性のため保持する。
3. ファイル入力が未対応の段階では、HelloWorld の内容を `ReMindSourceCode` に置き換えて同一入力で検証する。
4. BubbleSort の検証は HelloWorld の受け入れ後に別ケースとして行う。

### 2.2 期待出力の一致条件

期待ファイルは次の意味を要求している。

- `using System;`
- `namespace HelloWorld`
- `Program` という英語クラス名
- `Main` という英語メソッド名
- `string[] args` というパラメーター
- `Console.WriteLine("Hello World!");`
- XML コメントと整った空行・インデント

空白だけを完全一致させるか、意味的に同等な C# として比較するかを先に決める。第一段階では改行コード・末尾空白・インデントを正規化したテキスト比較を行い、最終段階で必要なら期待ファイルとの完全一致に切り替える。

## 3. 現在確認できる主な不足

### 3.1 Lexer

`Lexer.cs` は Re:Mind の構造記号を段階的に受け入れているが、HelloWorld 入力を Parser が宣言構文として扱えることまで保証していない。次を確認する。

- `▽`、`△`、`■`、`▼`、`▲` がトークン化される。
- 日本語識別子と `HelloWorld`、`string`、`void` が取得される。
- `"Hello World!"` が 1 つの文字列トークンになる。
- `.`、`(`、`)` が正しいトークンになる。
- EOF が 1 個だけ追加される。

### 3.2 Parser / AST

HelloWorld に必要な最小構文を AST に変換する。

- `▽名前空間 HelloWorld`
- `▽public クラス プログラム型`
- Javadoc コメント
- `▽static void メイン(string[] 引数)`
- `□コンソール.一行表示する("Hello World!")`
- `△` によるメソッド・クラス・名前空間終了
- `■インポートする System`
- `▼...▼` の AliasClass / AliasMember 定義

AST では、日本語名とターゲット名を混同しない。期待出力に必要な名前は次のように保持・解決する。

| Re:Mind 名 | C# 名 |
|---|---|
| `HelloWorld` | `HelloWorld` |
| `プログラム型` | `Program` |
| `メイン` | `Main` |
| `引数` | `args` |
| `コンソール.一行表示する` | `Console.WriteLine` |

### 3.3 SemanticResolver

`Imports` の AliasClass / AliasMember を使い、次を解決する。

```text
コンソール.一行表示する
    -> Console.WriteLine
```

Javadoc のクラス名・メソッド名・パラメーター名も、存在する英語名をシンボル情報として利用する。未定義名エラーを HelloWorld の正しい入力に対して発生させない。

### 3.4 IRBuilder

現在の `IRBuilder.Build(CompilationUnit)` は未実装であるため、次の構造を生成する最小実装が必要になる。

```text
ProgramIR
  Namespaces: HelloWorld
  Imports: System
  Classes:
    ClassIR Name = Program
      Methods:
        MethodIR Name = Main
          ReturnType = void
          Parameters = string[] args
          Body:
            CallExpressionIR MethodName = Console.WriteLine
              Arguments:
                LiteralIR Value = Hello World!
```

既存の `CallIR` と `CallExpressionIR` の責務を整理し、式文として呼び出しを保持できるようにする。HelloWorld ではまず呼び出し 1 文に限定し、BubbleSort 用の配列・ループ・代入は後続で広げる。

### 3.5 CSharpCodeGenerator

現在の `Generate(ProgramIR)`、`GenerateClass(ClassIR)`、`GenerateMethod(MethodIR)` は枠だけで、実際の出力を行っていない。HelloWorld 受け入れに必要な順で実装する。

1. `ProgramIR.Imports` を `using ...;` に変換する。
2. `ProgramIR.Namespaces` を `namespace ...` に変換する。
3. `ClassIR` を `public class Program` などへ変換する。
4. `MethodIR` を修飾子、戻り値、メソッド名、パラメーター付きで変換する。
5. `MethodIR.Body.Statements` を順番に生成する。
6. `CallExpressionIR` を `Console.WriteLine(...)` へ変換する。
7. `LiteralIR` を C# 文字列リテラルとして出力する。
8. `IndentWriter` で期待出力に近いインデントと空行を生成する。

`MappingTable.ConsoleWriteLine` は日本語キーから `Console.WriteLine` への解決に利用する。ただし、IR に解決済みの名前を保持する設計を採る場合は、CodeGenerator で日本語名を再解決しない。

## 4. 実装フェーズ

### Phase 1: 入力を固定

- `helloworldcs.aoi` を検証入力として選択できるようにする。
- `ReMindSourceCode` との内容差分を解消する。
- 入力ファイルの読み込みを追加する場合も、既存の const string は削除しない。
- 入力元がファイルか const string かをログまたは検証手順で明確にする。

完了条件:

- Lexer に渡る文字列が `helloworldcs.aoi` と一致することを確認できる。

### Phase 2: Lexer 検証

- HelloWorld の全入力を Tokenize する。
- 記号、識別子、文字列、括弧、ドット、EOF を確認する。
- 未対応文字を検出した場合は、行・列・文字を記録して修正する。

完了条件:

- `Unexpected character` が発生しない。
- トークン列が Parser の宣言・文解析に必要な情報を保持する。

### Phase 3: Parser / AST 検証

- namespace / class / method / invocation / import / alias を AST 化する。
- コレクションは既存 AST の `get` 専用リストへ `Add` / `AddRange` で追加する。
- エラーはトークン文字列と行番号を含める。

完了条件:

- HelloWorld の AST に namespace、class、Main、Console.WriteLine 呼び出しが存在する。

### Phase 4: 名前解決

- `SemanticResolver.Resolve()` を実装または必要範囲で補完する。
- クラス名、メソッド名、パラメーター名、AliasClass / AliasMember を解決する。
- `コンソール.一行表示する` が `Console.WriteLine` として IR へ渡ることを確認する。

完了条件:

- HelloWorld の正しい識別子に未定義エラーがない。

### Phase 5: AST → IR

- `IRBuilder.Build()` で `ProgramIR` を構築する。
- HelloWorld に必要な Program / Class / Method / Block / Call / Literal を変換する。
- IR のリストへは既存設計どおり追加する。

完了条件:

- `programIR` が空でなく、少なくとも HelloWorld / Program / Main / Console.WriteLine を含む。

### Phase 6: IR → C#

- C# CodeGenerator の IR 生成枠を実装する。
- `Generate(ProgramIR)` から class、method、statement、expression へ委譲する。
- 変数宣言、代入、if、while は既存骨格を壊さず、HelloWorld 完了後に段階的に追加する。
- Console.WriteLine は引数をカンマ区切りで生成する。

完了条件:

- 生成文字列に `using System;`、`namespace HelloWorld`、`public class Program`、`static void Main(string[] args)`、`Console.WriteLine("Hello World!");` が含まれる。

### Phase 7: 期待結果比較

比較処理を次の順で行う。

1. 生成出力を一時ファイルへ保存する。
2. `transcompiledFiles/helloworld.cs_` を読み込む。
3. CRLF / LF を統一する。
4. 行末空白を除去する。
5. 必要に応じて連続空行を正規化する。
6. 差分を表示する。
7. 完全一致を受け入れ条件にするか、C# コンパイル可能性と意味一致を受け入れ条件にするか判定する。

比較に加え、生成された C# を独立した一時プロジェクトでコンパイルし、実行結果が次になることを確認する。

```text
Hello World!
```

## 5. 検証コマンド案

PowerShell では環境によって `dotnet` が PATH にないため、絶対パスを使用する。

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' build .\astsample.csproj --no-restore
& 'C:\Program Files\dotnet\dotnet.EXE' run --project .\astsample.csproj
```

期待する確認順:

1. ビルドが成功する。
2. Lexer で停止しない。
3. Parser で停止しない。
4. SemanticResolver で停止しない。
5. IRBuilder が `NotImplementedException` を投げない。
6. CSharpCodeGenerator が空文字列を返さない。
7. 標準出力に C# ソースが出る。
8. 生成 C# をコンパイルできる。
9. 実行結果が `Hello World!` になる。
10. 期待ファイルとの差分が許容範囲内になる。

## 6. テストケース

### 必須ケース

- `helloworldcs.aoi` → `helloworld.cs_`
- AliasClass による `コンソール.一行表示する` の解決
- Javadoc による `プログラム型` → `Program`、`メイン` → `Main`、`引数` → `args`
- 文字列リテラル `"Hello World!"`
- namespace / import / class / method の終端記号

### 回帰ケース

- `variablecs.aoi` と `transcompiledFiles/variable.cs_`
- `ifthenelsecs.aoi` と `transcompiledFiles/ifthenelse.cs_`
- 既存の BubbleSort `ReMindSourceCode`

回帰ケースは HelloWorld を通過させた後に追加する。各ケースで、Lexer の修正が既存の `□`、`◇`、`〇`、配列、比較演算子を壊していないことを確認する。

## 7. 完了条件

### 最小完了

- `helloworldcs.aoi` を入力として Lexer → Parser → SemanticResolver → IRBuilder → CSharpCodeGenerator が例外なく完走する。
- C# ソースが標準出力される。
- 出力に `Console.WriteLine("Hello World!");` が含まれる。
- 生成コードを C# としてコンパイルできる。
- 実行結果が `Hello World!` になる。

### 期待結果一致

- 正規化後の生成出力が `transcompiledFiles/helloworld.cs_` と一致する。
- 差分が残る場合は、意味に影響しない空白・コメント・インデント差分か、実装仕様との差分かを分類して報告する。

## 8. 実装時の注意

- 今回の目的は HelloWorld の最小 E2E を通すこと。BubbleSort の全構文を同時に完成させない。
- `Program.cs` の入力変更、Parser の宣言解析、IRBuilder の実装、C# CodeGenerator の実装は依存関係を分けて検証する。
- `MapIdentifierName` のような固定変換を新たに増やさず、AliasClass と Javadoc の情報を SemanticResolver / IR に渡す。
- `IRBuilder.Build()` の未実装を放置すると Program.cs は必ず実行時に停止するため、C# CodeGenerator の修正前に IR を生成できる状態を作る。
- 期待ファイルの拡張子 `.cs_` は通常の C# ファイル名ではないため、検証時に一時的な `.cs` ファイルへコピーしてコンパイルする。
