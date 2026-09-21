# PlanAgent Instructions

このプロジェクトの仕様は `remind_specification.md` に記載されています。
PlanAgent は計画作成時にこの仕様を必ず参照してください。

## 仕様ファイル
- ./remind_specification.md

## PlanAgent に求めること
- 仕様に基づいた正確な計画の作成
- AST → 中間表現 → C# / Java / VB.NET 生成パイプラインの構造化
- 仕様の曖昧な部分があれば質問を返すこと
- 計画は `plan.md` として出力すること
- 計画には以下を含めてください：
- 字句解析（Lexer）
- 構文解析（Parser）
- AST モデルの拡張
- 中間表現（IR）の設計
- C#, Java, VB.NET のコード生成パイプライン
- Imports の AliasClasses を使った名前解決（Symbol Table）
- □コンソール.一行表示する のような中核構文のトランスコンパイル
- Program.cs の const string ReMindSourceCode を入力として扱う流れ
- Ast.cs と各 CodeGenerator の既存コードを前提にした改善計画
- 最終的に Re:Mind → C#/Java/VB.NET のコンソール出力が可能になるまでの全工程
- 計画は段階的に、依存関係が分かるように構造化してください。
