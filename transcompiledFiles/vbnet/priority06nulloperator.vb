Imports System

Namespace Priority06NullOperator
    ''' <summary>プログラム型</summary>
    Public Class Program
        ''' <summary>メイン</summary>
        ''' <param name=args>引数</param>
        Public Shared Sub Main(ByVal args As String())
            Dim 名前 As String = null
            Dim 候補 As String = If(名前, 名前.ToString(), Nothing)
            Dim 結果 As String = 候補 ?? "none"
            Console.WriteLine(結果)
        End Sub
        
    End Class
    
End Namespace
