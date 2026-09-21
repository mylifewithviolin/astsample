# Plan: Re:Mind コンパイラ全体設計

TL;DR
Re:Mind の現状実装は BubbleSort サンプル向けの簡易パーサーと 1 つの C# 生成器に寄っているため、仕様に沿った完全なパイプラインへ再設計する。最初に字句解析と構文解析を固め、その後 AST を拡張し、意味解析・IR 変換・各ターゲット言語生成へ段階的に進める。最終的に Program.cs の ReMindSourceCode を入力として扱い、Re:Mind から C#/Java/VB.NET へのコンソール出力ができる状態を目指す。

## Phase 0: スコープと前提整理
1. 仕様書の構文を基準に、まず実装対象の最小・中間・拡張セットを分ける。
2. 第 1 期の MVP では、既存の Ast.cs と各 CodeGenerator の実装状況に合わせて、以下を優先対象とする。
   - 変数宣言・初期化・代入
     - 代表構文: `・int 年齢 = 34`、`□年齢 = 35`
     - 既存の LocalVariableDeclaration / AssignmentStatement に対応しやすい
   - 式文
     - 代表構文: リテラル、二項演算、単項演算、メンバー参照、配列要素アクセス、関数呼び出し
     - 既存の Expression 系ノードで比較的自然に落とし込める
   - 関数宣言・関数呼び出し
     - 代表構文: `▽public int 関数名(...)`、`□関数名(...)`
     - 既存の MethodDeclaration / Parameter / InvocationExpression を中心に扱う
     - 戻り値は単純な `返す` / `return` までを対象とし、複雑な例外や多値返却は後続フェーズへ回す
   - 条件分岐
     - 代表構文: `◇条件 の場合` / `◇他に` / `◇ここまで`
     - `if / else` までを優先し、`switch` は対象外とする
   - 繰り返し
     - 代表構文: `〇繰り返す`、`〇(条件) の間は繰り返す`、`〇ここまで`
     - `while / for` を優先し、`do-while` は後続フェーズへ回す
   - クラスとメンバーアクセス
     - 代表構文: `▽public class クラス名 ... △`、`インスタンス.メンバー`
     - クラス宣言とメソッド定義は扱うが、継承・多重構造・高度なアクセス制御は対象外とする
   - 配列
     - 代表構文: `・int[] 配列 = 1,2,3`、`□配列[0] = 1`
     - 配列初期化と要素アクセスまでを対象とし、多次元配列や複雑な生成式は対象外とする
   - import / AliasClass
     - 代表構文: `■using System`
     - 既存の ImportDeclaration / AliasClass を利用した簡易名前解決までを対象とし、完全なライブラリ連携は後続フェーズへ回す
   - 例外構文・switch・高度な構文
     - `try / catch / finally`、`switch`、`break / continue` の高度な制御構文は、MVP では対象外とする
3. 既存の Program.cs の const string ReMindSourceCode を「入力サンプル」から「実行入口」に昇格させ、将来的にファイル入力や CLI 引数にも切り替えられる構造にする。

## Phase 1: 字句解析（Lexer）
1. Re:Mind のトークン体系を定義する。
   - 予約語: ・, □, ▽, △, ◇, 〇, ■, ▼, ▲, ここまで, の場合, で分岐する, 既定, 繰り返す, ここから, の間は, 返す, 試す, 捉まえる, 必ず最後に, 脱出する, ループ先頭へ, 反復処理, やめる
   - 識別子・日本語識別子・英字識別子
   - リテラル: 文字列、数値、真偽値
   - 演算子: =, +, -, *, /, %, ==, !=, >, <, >=, <=
   - 区切り: (), [], {}, カンマ, ドット, セミコロン
2. コメント・空白・マルチラインコメントをトークン化対象から除外する。
3. 行番号・列番号を保持し、エラー位置を正確に報告できるようにする。
4. 既存の Parser.cs から文字列ベースの前処理を分離し、Lexer を通した入力へ移行する。

## Phase 2: 構文解析（Parser）
1. 現状の Parser.cs の「BubbleSort 用の前提実装」を全面的に置き換える。
2. 構文を以下の層に分けて実装する。
   - ルート: CompilationUnit
   - 宣言: namespace / import / class / method / variable / constant
   - 文: assignment / expression-statement / if / while / for / return / break / continue
   - 式: identifier / literal / binary / unary / member access / invocation / array literal / new expression
3. 仕様にある中核構文を優先実装する。
   - □コンソール.一行表示する(引数)
   - □年齢 = ...
   - □(名前, 特待区分)で メソッド名
   - ◇条件 の場合 / ◇他に / ◇ここまで
   - 〇繰り返す / 〇(条件) の間は繰り返す / 〇ここまで
4. 構文エラー時には、トークン列と行番号を含むエラー情報を返すようにする。

## Phase 3: AST モデルの拡張
1. 既存の Ast.cs を拡張し、仕様に必要なノードを追加する。
2. 追加する主なノードを整理する。
   - 宣言系: ConstantDeclaration, FieldDeclaration, NamespaceDeclaration, ImportDeclaration, AliasClass, AliasMember
   - 文系: ReturnStatement, BreakStatement, ContinueStatement, TryCatchStatement, SwitchStatement, EmptyStatement
   - 式系: NewExpression, NullLiteralExpression, ConditionalExpression, AssignmentExpression, ArrayCreationExpression
   - メタ情報: JavadocComment, Parameter, TypeReference
3. 既存の NameJa / NameEn の扱いを維持しつつ、ターゲット言語名と元の日本語名を両方保持する。
4. AST には「構文上の形」を残し、意味解析前の情報として保持する。

## Phase 4: 意味解析と名前解決
1. Symbol Table を導入し、以下を解決する。
   - ローカル変数・引数・フィールド・メソッド名
   - クラス名・名前空間名
   - import された AliasClasses とそのメンバー
2. Imports の AliasClasses を活用して、Re:Mind の日本語メンバー呼び出しをターゲット言語側の英字名へ変換する。
3. 例として、以下のような解決を実現する。
   - コンソール.一行表示する → Console.WriteLine
   - メンバー取り扱い方法.年齢 → object.age
   - 変数・メソッドの型とスコープを解決する
4. 変数宣言前の使用や未定義名の検出を行う。
5. これは後続の IR 変換とコード生成の正確性を保証する基盤になる。

## Phase 5: 中間表現（IR）の設計
1. AST から後続ターゲットへ落とし込むための IR を追加する。
2. IR の設計例は以下の通り。
   - ProgramIR: namespace/import/class/method の集合
   - ClassIR: field と method の集合
   - MethodIR: parameter / local variable / body block
   - BlockIR: 文の列
   - StatementIR: assignment / call / if / while / for / return / break / continue
   - ExpressionIR: identifier / literal / binary / call / member access / array access / new
3. AST → IR の変換は「構文情報を保持したまま、バックエンド非依存の形に近づける」設計にする。
4. 生成器は IR を受け取って言語別に出力する構造に切り替える。

## Phase 6: コード生成パイプラインの再設計
1. 既存の CSharpCodeGenerator.cs / JavaCodeGenerator.cs / VbNetCodeGenerator.cs を、AST 直生成から IR ベース生成へ切り替える。
2. 各バックエンドで共通に使える出力ユーティリティを整える。
3. 実装対象は以下の順で進める。
   - まず C# を安定化する
   - 次に Java を追加する
   - 最後に VB.NET を追加する
4. 各生成器で実装する主な変換規則を整理する。
   - 変数宣言: 型と初期化式の変換
   - 配列: C# int[] / Java int[] / VB.NET Integer()
   - 代入・演算: 中置演算子の変換
   - 条件・ループ: if / while / for の変換
   - 呼び出し: メソッド呼び出し・メンバー呼び出し・AliasClass の解決
   - 例外・switch: 後続フェーズで追加

## Phase 7: コア構文のトランスコンパイル実装
1. まずは以下の「中核構文」を確実に生成できるようにする。
   - □コンソール.一行表示する(引数)
   - □変数名 = 式
   - □(引数)で 関数名
   - ◇条件 の場合 / ◇他に / ◇ここまで
   - 〇繰り返す / 〇(条件) の間は繰り返す / 〇ここまで
2. 生成対象言語ごとに、対応する構文へマッピングする。
3. 仕様上の「日本語の記法」と「ターゲット言語の構文」を両立させるため、変換ルール表を明示的に持つ。

## Phase 8: Program.cs からの実行フロー統合
1. Program.cs をオーケストレータとして再構成する。
2. 実行フローは次の順にする。
   - ReMindSourceCode を読み込む
   - Lexer でトークン化する
   - Parser で AST を生成する
   - SemanticResolver で名前解決・型解決を行う
   - IR へ変換する
   - 指定ターゲット言語の CodeGenerator でコードを生成する
   - 生成コードを標準出力またはファイルに書き出す
3. 既存の JSON 直列化・デシリアライズはデバッグ用途として残しつつ、本番フローでは不要なら段階的に外す。

## Phase 9: 検証と品質改善
1. 既存のビルド・実行を継続的に確認する。
2. 各フェーズごとにサンプル入力を用意し、期待コードが出力されることを確認する。
3. 追加する検証サンプルを用意する。
   - 変数宣言 + 代入 + console 出力
   - if / while / for のハンドリング
   - import / alias class の解決
   - class / method / member access
4. 未対応構文は明示的にエラーまたはプレースホルダーとして出力する。

## 依存関係
1. Lexer 実装 → Parser 変更
2. Parser が安定 → AST 拡張
3. AST が安定 → SemanticResolver / IR 変換
4. IR が安定 → 各 CodeGenerator の実装
5. Imports / AliasClasses の解決 → Console などのメンバー呼び出し生成
6. 上記が揃って初めて Program.cs のエンドツーエンド実行が成立する

## 主要対象ファイル
- Ast.cs
- Parser.cs
- Program.cs
- CSharpCodeGenerator.cs
- JavaCodeGenerator.cs
- VbNetCodeGenerator.cs
- IndenetWriter.cs

## 完了条件
1. Re:Mind の代表的なサンプルコードが Lexer と Parser を通過する
2. AST に必要な構文ノードが保持される
3. Imports の AliasClasses を利用した名前解決ができる
4. C#/Java/VB.NET のそれぞれに対して、コア構文の生成ができる
5. Program.cs から const string ReMindSourceCode を入力として受け取り、標準出力に生成コードを出せる

