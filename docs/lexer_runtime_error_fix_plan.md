# Lexer 実行エラー解消計画

## 1. 発生原因

実行時に次の例外が発生している。

```text
Unhandled exception. System.InvalidOperationException:
Unexpected character '▽' at line 2, column 1.
```

`Program.cs` の `ReMindSourceCode` は先頭の空行を読み飛ばした後、名前空間宣言の開始記号 `▽` を含む。現在の `Lexer.NextToken()` は `□`、`◇`、`〇` だけを専用トークンとして処理し、`▽` に対応する分岐がない。そのため、数値・文字列・識別子の判定にも進まず、最後の `Unexpected character` 例外に到達している。

なお、行番号 2 は、ソース文字列の先頭改行を Lexer が処理した結果であり、エラー位置自体は想定どおりである。

## 2. 修正方針

最初の目的は、`ReMindSourceCode` が Lexer を通過できる状態にすることとする。Parser や AST の責務を Lexer に移さず、記号を正しい `TokenType` と `Token.Text` に変換する。

既存コードを前提に、次の順序で修正する。

1. Re:Mind の宣言・構造記号を `TokenType` に追加する。
2. `Lexer.NextToken()` に各記号の 1 文字トークン化を追加する。
3. 宣言に続く日本語・英字識別子、キーワード、型名が正しく 1 トークンずつ取得できるようにする。
4. Lexer 単体の確認を行った後、Parser との接続を確認する。
5. Parser がまだ対応していない構文は、Lexer 修正の範囲を越えて別課題として切り分ける。

## 3. TokenType の追加候補

仕様および現在の入力に存在する記号を、用途が分かるトークンとして定義する。

| 記号 | 用途 | 追加候補 |
|---|---|---|
| `▽` | 名前空間・クラス・メソッド宣言の開始 | `DeclarationStart` または用途別トークン |
| `△` | メソッド・クラス・名前空間宣言の終了 | `DeclarationEnd` |
| `・` | 変数・定数宣言の開始 | `DeclarationBullet` |
| `■` | import 定義の開始 | `ImportStart` |
| `▼` | AliasClass / AliasMember 定義の開始 | `AliasStart` |
| `▲` | Alias 定義の終了 | `AliasEnd` |

実装時は、既存 Parser が記号の文字列を参照するのか `TokenType` を参照するのかを確認し、現在の `Box`・`Diamond`・`Circle` と同じ方針で統一する。単一の汎用トークンで足りる場合は無用な細分化を避ける。

## 4. Lexer の修正内容

`Lexer.NextToken()` の専用記号判定ブロックに、上記記号の処理を追加する。

各分岐では次の契約を守る。

- `Advance()` を 1 回だけ呼ぶ。
- `Token.Text` には元の記号をそのまま保持する。
- `Token.Line` と `Token.Column` は記号を読み取る前の位置を保持する。
- 空白・改行・コメントのスキップ処理は変更しない。
- 未知の文字を黙って識別子へ混ぜない。

併せて、日本語識別子の読み取り条件を確認する。現在の `char.IsLetter` により日本語文字は処理できるが、識別子に数字や `_` を含める仕様を採用する場合は、先頭と継続文字の条件を分けて調整する。ただし、今回の実行エラーだけを解消するために不要な拡張は行わない。

## 5. Parser との接続確認

Lexer が `▽` を処理できても、現行の `Parser.ParseCompilationUnit()` は主に `□`、`◇`、`〇`、`{` を文の開始として扱う構造であり、名前空間・クラス・メソッド宣言を完全には解析できない可能性がある。

そのため検証を次の 2 段階に分ける。

### 5.1 Lexer 段階

最小入力で次のトークン列を確認する。

```text
▽名前空間 BubbleSort
▽public クラス プログラム型
△
```

確認項目:

- `▽` が例外にならない。
- `名前空間`、`BubbleSort`、`public`、`クラス`、`プログラム型` が識別子またはキーワードとして取得される。
- `△` が終了記号として取得される。
- 行番号・列番号が単調に進む。
- EOF が最後に 1 個だけ追加される。

### 5.2 パイプライン段階

`Program.cs` の既存フローで次の順に確認する。

1. `new Lexer(ReMindSourceCode)`
2. `Tokenize()`
3. `new Parser(tokens)`
4. `ParseCompilationUnit()`

Lexer 修正後に Parser で失敗した場合は、エラーの記号と位置を記録し、宣言構文の Parser 対応を別の修正単位として扱う。今回の計画では、Lexer の例外解消と Parser 全面改修を同時に混在させない。

## 6. テスト・検証計画

既存のテスト基盤がない場合は、まずビルドと最小実行確認を行う。

1. `dotnet build .\\astsample.csproj --no-restore`
2. `Lexer` に対する最小記号入力の確認
3. `ReMindSourceCode` の先頭部分のトークン化確認
4. `dotnet run --no-build` による実行時エラーの確認
5. `Parser` で新たに発生したエラーを、行番号・トークン文字列付きで分類する

最低限の受け入れ条件は次のとおりとする。

- `▽` で `Unexpected character` が発生しない。
- `▽`、`△`、`・`、`■`、`▼`、`▲` が入力に含まれていても Lexer が該当位置を報告できる。
- 既存の `□`、`◇`、`〇`、括弧、配列記号、演算子のトークン化を壊さない。
- ビルドが成功する。
- Parser 以降の未対応構文は、Lexer の例外と区別できるエラーになる。

## 7. 影響範囲と注意点

- 主な変更対象は `Lexer.cs`。必要に応じて `TokenType` の定義だけを拡張する。
- `Parser.cs` は新しいトークン種別を参照する必要がある場合に限り、最小変更する。
- `Program.cs` の入力文字列や実行順は変更しない。
- `Ast.cs`、`IR.cs`、各 CodeGenerator は今回の Lexer エラー解消では変更しない。
- `▽` を単純な `Symbol` として処理するか専用 TokenType とするかは、Parser の宣言解析に必要な識別性を優先して決める。
- 記号を追加しただけでは、現行 Parser が名前空間・クラス・メソッド宣言を AST 化できるとは限らない。Lexer の修正完了と Parser の対応完了を別々に報告する。

## 8. 完了後の次工程

Lexer が全入力をトークン化できた後、仕様に沿って Parser の宣言層を整備する。

- namespace / import
- class / method
- variable / constant
- AliasClass / AliasMember
- `□`、`◇`、`〇` の中核文

その後、SemanticResolver、IRBuilder、C# / Java / VB.NET CodeGenerator の順に接続し、`Program.cs` の ReMindSourceCode から標準出力までの一連のパイプラインを確認する。
