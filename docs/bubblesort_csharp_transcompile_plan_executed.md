# BubbleSort C# トランスコンパイル実行計画 完了レポート

## 1. 実行日

2026-09-20

## 2. 実施した修正

`Program.cs` に次の最小修正を追加した。

- `bubblesort` 引数をケース選択に追加
- `sourceFiles/bubblesortcs.aoi` を入力として選択
- BubbleSort の期待ファイルが存在しない場合、比較処理をスキップ
- 生成結果を `transcompiledFiles/bubblesort.generated.cs_` に保存
- 既存の `ReMindSourceCode` は空文字列のまま保持
- HelloWorld / Variable の既存ケース選択は維持

BubbleSort については、現行 Token Parser がメソッド本体中の Javadoc や BubbleSort 固有構文を処理できないため、既存の `ReMindBubbleSortAstGenerator` と AST 用 C# Generator を再利用するフォールバック経路を追加した。

## 3. ビルド結果

実行コマンド:

```text
C:\Program Files\dotnet\dotnet.EXE build C:\developments\cs12\astsample/astsample.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary;ForceNoAlign
```

結果:

- ビルド成功
- 0 エラー
- 既存の nullable 警告は残存

## 4. BubbleSort 実行結果

実行コマンド:

```text
C:\Program Files\dotnet\dotnet.EXE run --no-build --project C:\developments\cs12\astsample/astsample.csproj -- bubblesort
```

結果:

- `sourceFiles/bubblesortcs.aoi` を選択
- 空の `ReMindSourceCode` にはフォールバックしなかった
- 実行時例外なし
- 標準出力へ生成 C# を出力
- `transcompiledFiles/bubblesort.generated.cs_` を生成
- BubbleSort の期待ファイル比較は実施していない

## 5. 生成ファイル

生成先:

```text
transcompiledFiles/bubblesort.generated.cs_
```

生成内容:

```csharp
using System;

namespace BubbleSort
{
    /// <summary>Program</summary>
    public class ProgramType
    {
        /// <summary>Main</summary>
        /// <param name="args">引数</param>
        static void main(string[] 引数)
        {
        }
    }
}
```

## 6. 目視確認結果

生成ファイル自体の作成と C# 出力は確認できた。

ただし、旧 BubbleSort 専用 Parser の現状実装に由来する未変換差分がある。

- クラス名が `ProgramType` のまま
- メソッド名が `main` のまま
- パラメーター名が `引数` のまま
- Main 本体の配列、バブルソート、ループ、代入、Console.WriteLine が生成されていない
- Javadoc の概要が `Program` / `Main` のまま

今回の依頼では期待ファイルが存在しないため、これらの差分に対する自動比較・修正は実施していない。

## 7. 完了判定

| 項目 | 結果 |
|---|---|
| `bubblesort` ケース選択 | 完了 |
| `bubblesortcs.aoi` 読み込み | 完了 |
| 空の `ReMindSourceCode` へのフォールバック回避 | 完了 |
| ビルド | 成功 |
| BubbleSort 実行 | 完了 |
| `bubblesort.generated.cs_` 生成 | 完了 |
| 標準出力 | 完了 |
| 期待ファイル比較 | 対象外・未実施 |
| BubbleSort 全構文の変換 | 未完了 |

## 8. 残課題

1. BubbleSort を Token Parser → AST → IR → C# Generator の共通経路で処理する。
2. クラス名・メソッド名・パラメーター名の `NameJa` / `NameEn` 解決を統一する。
3. 配列宣言、初期化、for、while、if、assignment、要素アクセスを IR へ変換する。
4. BubbleSort のメソッド本体を C# へ出力する。
5. XML コメントを `NameJa` ベースで出力する。
6. 生成 C# の目視確認後、必要になった時点で期待ファイルを追加する。

## 9. 結論

BubbleSort の外部ソースを選択し、`transcompiledFiles/bubblesort.generated.cs_` を生成するところまでは完了した。期待ファイルが存在しないため検証比較は行っていない。

現時点の生成物は目視確認用の最小出力であり、BubbleSort 全体のトランスコンパイル完了ではない。