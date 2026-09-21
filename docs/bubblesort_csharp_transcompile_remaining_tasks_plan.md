# BubbleSort C# トランスコンパイル残課題 実行計画

## 1. 目的

`docs/bubblesort_csharp_transcompile_plan_executed.md` の残課題 1〜5 を実行し、BubbleSort の次の構文を C# へ変換できる状態にする。

- クラス名・メソッド名・パラメーター名の `NameJa` / `NameEn` 解決
- 配列宣言・初期化
- 配列要素アクセス
- 代入
- `for` 相当の繰り返し
- `while` 相当の繰り返し
- `if` / `else`
- XML コメント
- `Console.WriteLine`
- BubbleSort メソッド本体

期待ファイルは作成せず、比較検証も実施しない。生成された `transcompiledFiles/bubblesort.generated.cs_` を目視確認できる状態を完了条件とする。

対象外にする残課題:

- 6. 生成 C# の目視確認後、必要になった時点で期待ファイルを追加する

## 2. 現状の問題

現在の BubbleSort 実行は、専用の行ベース Parser / Generator へフォールバックしている。その結果、出力は namespace・class・空の Main 程度に留まり、メソッド本体の構文が失われている。

```text
sourceFiles/bubblesortcs.aoi
  -> 既存専用 Parser
  -> AST
  -> AST 用 CSharpCodeGenerator
  -> 空または部分的な C#
```

一方、共通パイプライン側には次の不足がある。

- Token Parser がメソッド本体の DocumentComment を文として処理する
- Token Parser がローカル変数宣言を処理しない
- Token Parser が for 構文を処理しない
- Token Parser が比較・算術式を処理しない
- Token Parser が配列要素アクセスと代入左辺を処理しない
- IRBuilder が `LocalVariableDeclaration`、`ForStatement`、`BinaryExpression`、`ElementAccessExpression` を十分に変換しない
- IR に for、array access、unary、literal array の表現が不足している
- C# IR Generator が variable、for、binary、array access、unary を完全に出力しない
- User-defined method と `Console.WriteLine` の解決経路が統一されていない

## 3. 実行方針

BubbleSort 専用の固定変換を増やすのではなく、次の共通経路を BubbleSort が通過できるようにする。

```text
Lexer
  -> Token Parser
  -> AST
  -> SemanticResolver
  -> IRBuilder
  -> ProgramIR
  -> CSharpCodeGenerator
```

既存の BubbleSort 専用 Parser は回帰用に保持してもよいが、`bubblesort` ケースの本番経路では使用しない。`Program.cs` の `bubblesort` 分岐は、共通 Token Parser 経路へ戻す。

## 4. Phase 1: Token Parser の宣言・コメント処理

### 4.1 宣言名の解決

AST は次の情報を明確に保持する。

| Re:Mind 名 | C# 名 |
|---|---|
| `プログラム型` | `Program` |
| `メイン` | `Main` |
| `引数` | `args` |
| `バブルソートする` | `BubbleSort` または Javadoc の NameEn |
| `コンソール表示する` | `ConsoleOut` |
| `配列` | `array` |
| `外側` | `outer` |
| `内側` | `inner` |
| `一時` | `temp` |
| `i` | `i` |

固定名の追加は最小限にし、可能なら Javadoc の Summary と Symbol Table を `NameEn` 解決の情報源にする。

### 4.2 DocumentComment の扱い

メソッド本体内に現れる `/** ... */` は、次の宣言または文に付くコメントとして扱う。少なくともローカル変数宣言へ渡す。

```text
/** array */
・int[] 配列 = ...
```

コメントを `ParseStatement()` の未対応文として送らない。

### 4.3 フィールド・ローカル変数・メソッド

次を共通 Parser で扱う。

- class field: `・private static string? ...`
- local variable: `・int[] 配列 = ...`
- method: `▽static void ...`
- method parameter: `int[] 配列`
- method body end: `△`
- class / namespace end: `△`

## 5. Phase 2: 式 Parser

### 5.1 リテラル

- 数値
- 文字列
- 識別子
- 配列初期化値のカンマ区切り

`・int[] 配列 = 15,13,9,6,4,1` は `ArrayLiteralExpression` または対応する配列生成 AST として保持する。

### 5.2 MemberAccess / ElementAccess

次を AST 化する。

```text
配列.Length
配列[内側]
配列[内側 + 1]
コンソール.一行表示する
```

`配列.Length` は `MemberAccessExpression`、`配列[index]` は `ElementAccessExpression` とする。

### 5.3 二項式

少なくとも次の演算子を扱う。

- `<`
- `>`
- `-`
- `+`

式の優先順位を簡易実装する場合でも、次の構造を壊さない。

```text
配列.Length - 外側 - 1
配列[内側] > 配列[内側 + 1]
i < 配列.Length
```

### 5.4 単項式

次を `UnaryExpression` として保持する。

```text
i++
外側++
内側++
```

## 6. Phase 3: 文 Parser

### 6.1 代入

次を `AssignmentStatement` に変換する。

```text
□配列[内側] = 配列[内側 + 1]
□配列[内側 + 1] = 一時
□外側++
```

代入左辺は識別子だけでなく `ElementAccessExpression` を許可する。

### 6.2 LocalVariableDeclaration

次を `LocalVariableDeclaration` に変換する。

```text
・int[] 配列 = 15,13,9,6,4,1
・int 外側 = 0
・int 内側 = 0
・int 一時 = 配列[内側]
```

型、NameJa、NameEn、Initializer、Javadoc を保持する。

### 6.3 For

次を `ForStatement` に変換する。

```text
〇int i=0,i<配列.Length,i++ 繰り返す
```

保持する情報:

- Initializer: `int i = 0`
- Condition: `i < 配列.Length`
- Iterator: `i++`
- Body: `□コンソール表示する(配列[i])`

### 6.4 While

次を `WhileStatement` に変換する。

```text
〇外側 < 配列.Length の間は繰り返す
〇内側 < 配列.Length - 外側 - 1 の間は繰り返す
```

### 6.5 If

次を `IfStatement` に変換する。

```text
◇配列[内側] > 配列[内側 + 1] の場合
...
◇ここまで
```

ThenBody と ElseBody のリストは `Add` / `AddRange` で構築する。

## 7. Phase 4: AST → IR

IR に BubbleSort の必要構造を追加する。

### 7.1 ExpressionIR

必要な表現:

- `IdentifierIR`
- `LiteralIR`
- `BinaryIR`
- `MemberAccessIR`
- `ArrayAccessIR`
- `CallExpressionIR`
- `NewExpressionIR`
- 必要に応じて `UnaryIR`

`ArrayAccessIR` は次のように保持する。

```text
Array: IdentifierIR(array)
Index: IdentifierIR(inner)
```

### 7.2 StatementIR

必要な表現:

- `VariableDeclarationIR`
- `AssignmentIR`
- `CallIR`
- `IfIR`
- `WhileIR`
- `ForIR`

既存の `ForIR` がない場合は、`Initializer`、`Condition`、`Iterator`、`Body` を持つ最小 IR を追加する。

### 7.3 コメント

既存の `DocumentationIR.NameJa` を使い、BubbleSort のクラス・メソッド・ローカル変数コメントを保持する。

## 8. Phase 5: SemanticResolver

Symbol Table で次を解決する。

- class / method / parameter
- field / local variable
- `配列` → `array`
- `外側` → `outer`
- `内側` → `inner`
- `一時` → `temp`
- `コンソール.一行表示する` → `Console.WriteLine`
- `コンソール表示する` → `ConsoleOut`

型解決:

- `int[]`
- `int`
- `string`
- `string?`
- 配列の `Length`

スコープ:

- class field
- method parameter
- method local variable
- nested while / if block

未定義識別子を生成時まで持ち越さない。

## 9. Phase 6: IR → C# Generator

C# Generator に次の出力を追加する。

### 9.1 宣言

```csharp
int[] array = new int[] { 15, 13, 9, 6, 4, 1 };
int outer = 0;
int inner = 0;
int temp = array[inner];
```

### 9.2 代入

```csharp
array[inner] = array[inner + 1];
array[inner + 1] = temp;
outer++;
```

### 9.3 If / While / For

```csharp
if (array[inner] > array[inner + 1])
{
}

while (outer < array.Length)
{
}

for (int i = 0; i < array.Length; i++)
{
}
```

### 9.4 呼び出し

```csharp
BubbleSort(array);
ConsoleOut(array[i]);
Console.WriteLine(args2);
```

### 9.5 コメント

クラス・メソッド・ローカル変数の `DocumentationIR.NameJa` を XML `<summary>` に出力する。`@param` は既存の `DocumentationParamIR` を出力する。

## 10. Phase 7: Program.cs の経路統合

`Program.cs` の `bubblesort` 分岐で専用 AST Parser / Generator を使わず、通常の次の経路を使う。

```text
Lexer
Parser
SemanticResolver
IRBuilder
CSharpCodeGenerator.Generate(ProgramIR)
```

保持する処理:

- `sourceFiles/bubblesortcs.aoi` の読み込み
- 標準出力
- `transcompiledFiles/bubblesort.generated.cs_` への出力
- 期待ファイルがない場合の比較スキップ

## 11. 実行順序

1. Parser の DocumentComment / declaration 処理を安定させる。
2. 式 Parser を実装する。
3. assignment / local variable / if / while / for を実装する。
4. AST → IR の不足ノードを追加する。
5. SemanticResolver の名前・型・スコープ解決を追加する。
6. C# Generator で配列・代入・アクセス・制御構文を出力する。
7. `Program.cs` の BubbleSort 専用フォールバックを削除する。
8. `bubblesort` 引数で共通経路を実行する。
9. `transcompiledFiles/bubblesort.generated.cs_` を目視確認する。

## 12. 検証手順

### ビルド

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' build .\astsample.csproj --no-restore
```

### 実行

```powershell
& 'C:\Program Files\dotnet\dotnet.EXE' run --no-build --project .\astsample.csproj -- bubblesort
```

### 確認項目

- Lexer で例外が出ない
- Parser が Main / BubbleSort / ConsoleOut の本体を保持する
- AST に配列、for、while、if、assignment が存在する
- IR が空でない
- C# 出力に配列・ループ・条件・代入が存在する
- `Console.WriteLine` が出力される
- `transcompiledFiles/bubblesort.generated.cs_` が更新される
- XML コメントが日本語 `NameJa` で出力される
- 期待ファイル比較は行わない

## 13. 完了条件

- BubbleSort が専用フォールバックではなく共通 Token → AST → IR → C# 経路を通る。
- クラス名・メソッド名・パラメーター名が C# 名として解決される。
- 配列宣言・初期化・要素アクセスが生成される。
- `for`、`while`、`if` が生成される。
- 代入と `++` が生成される。
- BubbleSort メソッド本体が生成される。
- `Console.WriteLine` とユーザー定義メソッド呼び出しが生成される。
- XML コメントが日本語 `NameJa` で生成される。
- `transcompiledFiles/bubblesort.generated.cs_` を目視確認できる。
- 期待ファイルの作成・比較は行わない。

## 14. 対象外

- BubbleSort の期待ファイル作成
- 自動差分比較
- 期待結果との完全一致
- Java / VB.NET の対応
- 既存の unrelated な nullable 警告の解消
