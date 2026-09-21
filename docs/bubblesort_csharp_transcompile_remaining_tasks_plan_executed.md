# BubbleSort C# トランスコンパイル残課題 完了レポート

## 1. 実行日

2026-09-20

## 2. 実施結果

`bubblesort` ケースを旧 `ReMindBubbleSortAstGenerator` フォールバックから外し、共通の次の経路へ統合した。

```text
Lexer
  -> Parser
  -> SemanticResolver
  -> IRBuilder
  -> ProgramIR
  -> CSharpCodeGenerator
```

実装した内容:

- Token Parser にローカル変数、配列リテラル、要素アクセス、メンバーアクセス、呼び出し、二項演算、後置 `++` を追加
- `for`、`while`、`if`、代入と入れ子ブロックを解析
- メソッド・ループ・条件分岐内の Javadoc をローカル変数へ関連付け
- `プログラム型`、`メイン`、`バブルソートする`、`配列`、`外側`、`内側`、`一時`、`コンソール表示する` の名前解決を追加
- `@param` の `NameEn` をメソッド本文の識別子参照へ反映
- `VariableDeclarationIR` にドキュメントを保持し、C# の XML `<summary>` をローカル変数にも出力
- `ConsoleOut` 本体の `コンソール.一行表示する` を `Console.WriteLine` に変換

## 3. ビルド結果

実行コマンド:

```text
C:\Program Files\dotnet\dotnet.EXE build C:\developments\cs12\astsample\astsample.csproj
```

結果:

- ビルド成功
- エラー 0
- 既存の nullable 警告は残存

## 4. BubbleSort 実行結果

実行対象:

```text
sourceFiles/bubblesortcs.aoi
```

結果:

- 実行時例外なし
- `transcompiledFiles/bubblesort.generated.cs_` を更新
- 期待ファイルが存在しないため比較はスキップ
- 旧 BubbleSort 専用 Parser は本番経路で使用しない

## 5. 生成内容の確認

生成ファイルには次の構造が含まれることを確認した。

```csharp
int[] array = new int[] { 15, 13, 9, 6, 4, 1 };
BubbleSort(array);

for (int i = 0; i < array.Length; i++)
{
    ConsoleOut(array[i]);
}

while (outer < array.Length)
{
    while (inner < array.Length - outer - 1)
    {
        if (array[inner] > array[inner + 1])
        {
            int temp = array[inner];
            array[inner] = array[inner + 1];
            array[inner + 1] = temp;
        }
    }
}

Console.WriteLine(dispStr);
```

## 6. 完了判定

| 項目 | 結果 |
| --- | --- |
| 共通 Token -> AST -> IR -> C# 経路 | 完了 |
| `NameJa` / `NameEn` の主要名前解決 | 完了 |
| 配列宣言・初期化・要素アクセス | 完了 |
| 代入・二項式・後置 `++` | 完了 |
| `for` / `while` / `if` | 完了 |
| XML コメント出力 | 完了 |
| `Console.WriteLine` の別名解決 | 完了 |
| `bubblesort.generated.cs_` の更新 | 完了 |
| 期待ファイル比較 | 対象外 |

## 7. 補足

入力ソースは `ConsoleOut(string dispStr)` に `array[i]` (`int`) を渡している。そのため、生成 C# 自体を別プロジェクトでコンパイルする場合は、入力側の引数型を `int` または `object` に変更する必要がある。本計画の完了条件である生成内容の目視確認には影響しない。