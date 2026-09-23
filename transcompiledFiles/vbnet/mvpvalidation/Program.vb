Imports System

Namespace MvpValidation
    ''' <summary>プログラム型</summary>
    Public Class Program
        ''' <summary>制限値</summary>
        Private Const Limit As Integer = 3
        ''' <summary>メイン</summary>
        ''' <param name=args>引数</param>
        Public Shared Sub Main(ByVal args As String())
            ''' <summary>有効</summary>
            Dim enabled As Boolean = true
            ''' <summary>無効</summary>
            Dim disabled As Boolean = false
            ''' <summary>カウント</summary>
            Dim count As Integer = 0
            ''' <summary>結果</summary>
            Dim result As Integer = Evaluate(enabled, disabled)
            If result > 0 Then
                While count < result
                    Console.WriteLine(count)
                    count += 1
                End While
            Else
                Console.WriteLine("disabled")
            End If
        End Sub
        
        ''' <summary>判定する</summary>
        ''' <param name=enabled>有効</param>
        ''' <param name=disabled>無効</param>
        Shared Function Evaluate(ByVal enabled As Boolean, ByVal disabled As Boolean) As Integer
            If (enabled AndAlso Not disabled) OrElse disabled Then
                Return Limit
            Else
                Return -1
            End If
        End Function
        
        ''' <summary>コンソール表示する</summary>
        ''' <param name=dispStr>引数2</param>
        Shared Sub ConsoleOut(ByVal dispStr As String)
            Console.WriteLine(dispStr)
        End Sub
        
    End Class
    
End Namespace
