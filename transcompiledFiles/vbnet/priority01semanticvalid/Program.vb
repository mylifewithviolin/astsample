Imports System

Namespace Priority01SemanticValid
    ''' <summary>プログラム型</summary>
    Public Class Program
        ''' <summary>メイン</summary>
        ''' <param name=args>引数</param>
        Public Shared Sub Main(ByVal args As String())
            ''' <summary>個数</summary>
            Dim count As Integer = 3
            ''' <summary>結果</summary>
            Dim result As Integer = VerifyValue(count)
            ''' <summary>妥当</summary>
            Dim valid As Boolean = result = 3
            ''' <summary>メッセージ</summary>
            Dim message As String = "semantic-ok"
            If valid Then
                Console.WriteLine(message)
            Else
                Console.WriteLine("semantic-ng")
            End If
        End Sub
        
        ''' <summary>値を確認する</summary>
        ''' <param name=value>値</param>
        Shared Function VerifyValue(ByVal value As Integer) As Integer
            Return value
        End Function
        
    End Class
    
End Namespace
