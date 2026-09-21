# Plan: Re:Mind コンパイラ全体設計

## 1. 目的

Re:Mind の仕様に沿って、入力テキストを解析し、AST を生成し、中間表現に変換し、C# / Java / VB.NET へトランスコンパイルできるようにする。

本計画は、既存の [Ast.cs](../Ast.cs)、[Parser.cs](../Parser.cs)、[CSharpCodeGenerator.cs](../CSharpCodeGenerator.cs)、[JavaCodeGenerator.cs](../JavaCodeGenerator.cs)、[VbNetCodeGenerator.cs](../VbNetCodeGenerator.cs)、[Program.cs](../Program.cs) を前提に、段階的に拡張する。

## 2. 実装方針

### 2.1 全体パイプライン

1. Lexer で Re:Mind ソースをトークン化する
2. Parser で構文木（AST）を生成する
3. AST を中間表現（IR）へ変換する
4. IR を各ターゲット言語向けコードへ生成する
5. Program.cs の `const string ReMindSourceCode` を入力として扱い、標準出力またはファイル出力へ反映する

## 3. フェーズ分割

### Phase 0: 現状把握

- 既存のパーサ、AST、コード生成器の責務を確認する
- どの構文が現状対応済みかを棚卸しする
- 仕様書 [remind_specification.md](./remind_specification.md) と実装コードの差分を整理する

### Phase 1: Lexer の実装

- Re:Mind 固有記号を認識する
  - `・`、`□`、`◇`、`〇`、`▽`、`△`、`▲`、`■`、`▼`
- 識別子、数値、文字列、演算子、コメントをトークン化する
- コメントや空白の扱いを定義する

依存関係:
- Parser から使用される
- まずは最小構文のトークンだけ実装する

### Phase 2: Parser の実装

- 変数宣言・代入
- 定数宣言
- 関数呼び出し
- 関数宣言
- 条件分岐（if 相当）
- 繰り返し（while / for / do-while 相当）
- 代表構文として `□コンソール.一行表示する` を解析できるようにする

依存関係:
- Lexer の出力を受ける
- AST を生成する

### Phase 3: AST モデル拡張

- ノードの基本構造を整理する
- 次のノードを追加する
  - `VariableDeclarationNode`
  - `ConstantDeclarationNode`
  - `FunctionDeclarationNode`
  - `FunctionCallNode`
  - `IfStatementNode`
  - `LoopStatementNode`
  - `ReturnStatementNode`
- 既存の [Ast.cs](../Ast.cs) に段階的に追記する

依存関係:
- Parser が生成する
- IR 変換の入力になる

### Phase 4: IR 設計

- 言語非依存の中間表現を定義する
- 代表命令を追加する
  - `Assign`
  - `Call`
  - `Return`
  - `If`
  - `Loop`
  - `Block`
- IR は各ターゲット言語コード生成へ変換しやすい構造にする

依存関係:
- AST を入力にする
- CodeGenerator の共通基盤になる

### Phase 5: CodeGenerator 実装

- C# 生成
  - 変数宣言、代入、条件分岐、ループ、関数呼び出しを出力する
- Java 生成
  - 同様の構文を Java に変換する
- VB.NET 生成
  - 同様の構文を VB.NET に変換する

対象例:
- `□コンソール.一行表示する`
- `・int 年齢 = 34`
- `◇i > 0 の場合` など

依存関係:
- IR を入力にする
- 各生成器は共通の命名規則と出力ルールに従う

### Phase 6: 名前解決とシンボル管理

- Imports の `AliasClasses` を使って名前解決できるようにする
- 変数名・関数名・クラス名をシンボルテーブルで管理する
- 参照解決の失敗時に明確なエラーを出す

依存関係:
- Parser / AST / IR の後段で利用する

### Phase 7: 実行フロー統合

- [Program.cs](../Program.cs) の `const string ReMindSourceCode` を入力ソースとして扱う
- 文字列を Lexer → Parser → AST → IR → CodeGenerator へ流す
- 変換結果をコンソールに出力する
- 最終的に C#/Java/VB.NET の出力例を確認できるようにする

## 4. 最小実装順

1. まず最小セットの構文だけ実装する
   - 変数宣言
   - 代入
   - 関数呼び出し
   - if / while
2. 次に中間セットを追加する
   - 配列
   - switch / for / do-while
   - クラス
   - 例外
3. 最後に拡張セットを追加する
   - imports
   - 継承
   - null 条件演算子
   - null 合体演算子

## 5. 完了条件

- Re:Mind の基本サンプルコードを C# / Java / VB.NET に変換できる
- `□コンソール.一行表示する` のような中核構文が正しく出力される
- 既存の Ast.cs と各 CodeGenerator の拡張が段階的に進められる
- 実装と仕様の対応関係が明確になる
