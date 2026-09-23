Imports System

Namespace LinearSearch
    ''' <summary>プログラム型</summary>
    Public Class Program
        ''' <summary>メイン</summary>
        ''' <param name=args>引数</param>
        Shared Sub Main(ByVal args As String())
            ''' <summary>配列</summary>
            Dim array As Integer() = { 15, 13, 9, 6, 4, 1 }
            ''' <summary>探す値</summary>
            Dim target As Integer = 4
            ''' <summary>該当インデックス</summary>
            Dim index As Integer = LinearSearch(array, target)
            If index <> -1 Then
                ConsoleOut(index)
            Else
                ConsoleOut("該当なし")
            End If
        End Sub
        
        ''' <summary>線形探索する</summary>
        ''' <param name=array>配列</param>
        ''' <param name=target>探す値</param>
        Public Shared Function LinearSearch(ByVal array As Integer(), ByVal target As Integer) As Integer
            ''' <summary>インデックス</summary>
            Dim i As Integer = 0
            While i < array.Length
                If array(i) = target Then
                    Return i
                End If
                i += 1
            End While
            Return -1
        End Function
        
        ''' <summary>コンソール表示する</summary>
        ''' <param name=dispStr>引数2</param>
        Shared Sub ConsoleOut(ByVal dispStr As String)
            Console.WriteLine(dispStr)
        End Sub
        
    End Class
    
End Namespace
