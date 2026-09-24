Imports System

Namespace Priority03ReturnKeyword
    ''' <summary>プログラム型</summary>
    Public Class Program
        ''' <summary>メイン</summary>
        ''' <param name=args>引数</param>
        Public Shared Sub Main(ByVal args As String())
            Dim 結果 As String = ReturnText()
            Console.WriteLine(結果)
        End Sub
        
        ''' <summary>結果を返す</summary>
        Shared Function ReturnText() As String
            Return "return-keyword-ok"
        End Function
        
    End Class
    
End Namespace
