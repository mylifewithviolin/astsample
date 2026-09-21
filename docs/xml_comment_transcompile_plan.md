# XML コメント出力対応計画

## 1. 目的

現在の IR ベース C# 生成経路では、`sourceFiles/variablecs.aoi` に含まれる Javadoc 形式コメントが `transcompiledFiles/variable.generated.cs_` に出力されていない。

期待結果の [variable.cs_](../transcompiledFiles/variable.cs_) および [helloworld.cs_](../transcompiledFiles/helloworld.cs_) に対応するため、次のコメントを保持・変換する。

- クラスコメント
- フィールドコメント
- メソッドコメント
- `@param` コメント

目標出力形式:

```csharp
/// <summary>プログラム型</summary>
/// <summary>挨拶１</summary>
/// <summary>メイン</summary>
/// <param name="args">引数</param>
```

## 2. 根本原因

現行の実行経路は次のとおりである。

```text
Lexer -> Token -> Token Parser -> AST -> IRBuilder -> Generate(ProgramIR)
```

`Lexer.NextToken()` は `/** ... */` をコメントとして読み飛ばしている。そのため、コメント情報が Token 列に存在せず、Token ベース Parser が生成する AST にも渡らない。

一方、旧来の行ベース Parser には `ParseJavadoc()`、`JavadocComment`、`MethodDeclaration.Javadoc` などの処理が存在する。しかし、Program.cs の本番経路は Token Parser と IRBuilder を使うため、旧 AST 生成器のコメント処理は現在の生成結果に反映されない。

さらに、現在の `ProgramIR` / `ClassIR` / `MethodIR` には `Comment` プロパティの枠はあるが、AST の `JavadocComment` をコピーする処理と、IR ベース `CSharpCodeGenerator` で XML コメントを出力する処理が未実装である。

## 3. 修正方針

既存の Lexer → Token → Parser → AST → IR → CodeGenerator の経路を維持し、コメントをその経路に追加する。旧行ベース Parser へ戻すことや、コメント内容を名前マップに固定することは行わない。

修正は次の 3 層に分ける。

1. Lexer: Javadoc を失わない
2. Parser / IRBuilder: コメントを対象ノードへ関連付ける
3. CSharpCodeGenerator: IR コメントを XML コメントへ出力する

## 4. Lexer 修正

### 4.1 コメントトークンの追加

`TokenType.Comment` または `TokenType.DocumentComment` を追加する。

`/** ... */` を検出した場合、現在の読み飛ばし処理を次の処理へ変更する。

- 開始位置を保存
- コメント終端まで走査
- コメント本文または元テキストを Token.Text に保存
- 行番号・列番号を開始位置で保持
- 通常コメント `/* ... */` と `// ...` は従来どおり読み飛ばす

今回の対象は Javadoc のみとし、通常コメントの AST 保持は追加しない。

### 4.2 コメント本文の契約

Parser が扱いやすいよう、少なくとも次の情報を保持する。

- 概要行
- `@param` の日本語名と英語名
- 必要なら元のコメント本文

コメントの構造化を Lexer に持たせすぎない。Javadoc の行分解・`@param` 解釈は Parser または専用変換ヘルパーの責務とする。

## 5. Parser / AST 修正

### 5.1 保留コメント

Token Parser に `JavadocComment? pendingJavadoc` を持たせ、`DocumentComment` を読み取ったら次の宣言まで保持する。

コメントを関連付ける対象:

- `▽public クラス ...` → `ClassDeclaration.Javadoc`
- `▽static void ...` → `MethodDeclaration.Javadoc`
- `・...` → `FieldDeclaration.Javadoc`
- 必要に応じてローカル変数宣言 → `LocalVariableDeclaration.Javadoc`

宣言を読み取った時点で pending コメントを対象ノードへ移し、二重利用しない。

### 5.2 Variable のフィールド

現在の `ClassDeclaration.Fields` と `FieldDeclaration.Javadoc` を利用する。フィールドのコメントを単なる `string` に潰さず、既存の `JavadocComment` の `Summary` と `Params` を保持する。

### 5.3 名前解決との分離

Javadoc の概要が `aisatsu1`、`args`、`dispStr` のような英語名として使われる現行仕様を維持する。ただし、次の情報を別々に保持する。

- `NameJa`: Re:Mind のソース名
- `NameEn`: ターゲット名
- `Javadoc.Summary`: コメント概要
- `Javadoc.Params`: パラメーター説明

コメントを名前解決の副作用だけで処理しない。

## 6. IR 修正

### 6.1 コメント情報の型

既存の `Comment` 文字列枠だけでは `@param` を保持できないため、必要最小限として IR にコメントモデルを追加する。

例:

```csharp
public class DocumentationIR
{
    public string? Summary { get; set; }
    public List<DocumentationParamIR> Parameters { get; } = new();
}

public class DocumentationParamIR
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
```

または、既存の `Comment` / `FormattingInfo` 枠を維持し、`DocumentationIR` をクラス・フィールド・メソッド IR に追加する。既存 API を壊さない形を優先する。

### 6.2 AST → IR のコピー

`IRBuilder` で次をコピーする。

- `ClassDeclaration.Javadoc` → `ClassIR.Documentation`
- `FieldDeclaration.Javadoc` → field IR の documentation
- `MethodDeclaration.Javadoc` → `MethodIR.Documentation`
- `LocalVariableDeclaration.Javadoc` → statement IR の documentation

`@param 引数 args` は、期待 C# 出力に合わせて次の形式で保持する。

```text
Name = args
Description = 引数
```

## 7. CSharpCodeGenerator 修正

### 7.1 出力位置

IR の出力順序を期待結果に合わせる。

1. class documentation
2. class declaration
3. field documentation
4. field declaration
5. method documentation
6. method declaration
7. statement documentation
8. statement

### 7.2 XML コメント出力ヘルパー

IR 経路に専用ヘルパーを追加する。

```text
GenerateDocumentation(DocumentationIR documentation)
```

最小処理:

- Summary があれば `/// <summary>...</summary>` を出力
- Parameters を `/// <param name="..."><description></param>` で出力
- documentation が null の場合は何も出力しない

これは既存 AST 用 `WriteXmlComment(JavadocComment)` と責務を分ける。既存の AST 直生成経路は保持する。

### 7.3 IR フィールド表現

現在 `ClassIR.Fields` は `List<string>` のためコメントをフィールド単位で関連付けられない。次のどちらかを選択する。

- `FieldIR` を追加して `ClassIR.Fields` を構造化する
- 既存の `List<string>` を保持し、別の field documentation リストを追加する

推奨は `FieldIR` への構造化だが、変更範囲を抑える必要がある場合は既存 API と利用箇所を確認してから決める。

## 8. 期待出力との検証

### 8.1 HelloWorld

入力:

- [helloworldcs.aoi](../sourceFiles/helloworldcs.aoi)

期待するコメント:

```csharp
/// <summary>プログラム型</summary>
/// <summary>メイン</summary>
/// <param name="args">引数</param>
```

### 8.2 Variable

入力:

- [variablecs.aoi](../sourceFiles/variablecs.aoi)

期待するコメント:

```csharp
/// <summary>プログラム型</summary>
/// <summary>挨拶１</summary>
/// <summary>メイン</summary>
/// <param name="args">引数</param>
/// <summary>コンソール表示する</summary>
/// <param name="args2">引数2</param>
```

### 8.3 検証順

1. Lexer 単体で `DocumentComment` が取得できることを確認
2. Parser の AST に class / field / method の Javadoc が入ることを確認
3. IRBuilder 後に documentation が存在することを確認
4. `helloworld.generated.cs_` を生成
5. `variable.generated.cs_` を生成
6. 各生成物に `/// <summary>` と `/// <param>` が存在することを確認
7. 期待ファイルとコメント内容を比較
8. コメント以外の空白・インデント差分を別分類

## 9. 受け入れ条件

- HelloWorld と Variable の両方で Javadoc が Lexer から消失しない。
- class / field / method の XML コメントが生成される。
- `@param` が C# の `<param name="...">...</param>` になる。
- コメント付きで生成された C# がコンパイル可能である。
- コメントの追加で名前解決・代入・呼び出し生成が壊れない。
- `variable.generated.cs_` に `aisatsu1`、`ConsoleOut`、`Console.WriteLine` と XML コメントが共存する。
- HelloWorld と Variable の両ケースを同じ IR ベース経路で検証できる。

## 10. 段階的実装順

1. `TokenType.DocumentComment` と Lexer の Javadoc 保持
2. Parser の pending Javadoc と AST への関連付け
3. IR の documentation モデル追加
4. IRBuilder の documentation コピー
5. CSharpCodeGenerator の IR コメント出力
6. HelloWorld のコメント検証
7. Variable の field / method / parameter コメント検証
8. 既存の代入・呼び出し・`.cs_` 出力の回帰確認

## 11. 注意点

- コメントを Lexer で完全に捨てる現行処理を残したまま、CodeGenerator だけを修正しても出力できない。
- AST 直生成経路の `WriteXmlComment` が存在しても、Program.cs が `Generate(ProgramIR)` を呼ぶ限り IR 側のコメント処理が必要である。
- `transcompiledFiles/*.cs_` は検証生成物であり、プロジェクトのコンパイル対象へ混入させない。
- 完全一致にはコメントだけでなく、空行・インデント・代入演算子周辺の空白も別途整形規則として扱う。
