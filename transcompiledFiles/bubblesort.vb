Imports System

Namespace BubbleSort
    ''' <summary>プログラム型</summary>
    Public Class Program
        ''' <summary>メイン</summary>
        ''' <param name=args>引数</param>
        Shared Sub Main(ByVal args As String())
            ''' <summary>配列</summary>
            Dim array As Integer() = { 15, 13, 9, 6, 4, 1 }
            BubbleSort(array)
            For i As Integer = 0 To array.Length - 1
                ConsoleOut(array(i))
            Next
        End Sub
        
        ''' <summary>バブルソートする</summary>
        ''' <param name=array>配列</param>
        Public Shared Sub BubbleSort(ByVal array As Integer())
            ''' <summary>外側</summary>
            Dim i As Integer = 0
            While i < array.Length
                ''' <summary>内側</summary>
                Dim j As Integer = 0
                While j < array.Length - i - 1
                    If array(j) > array(j + 1) Then
                        ''' <summary>一時</summary>
                        Dim temp As Integer = array(j)
                        array(j) = array(j + 1)
                        array(j + 1) = temp
                    End If
                    j += 1
                End While
                i += 1
            End While
        End Sub
        
        ''' <summary>コンソール表示する</summary>
        ''' <param name=dispStr>引数2</param>
        Shared Sub ConsoleOut(ByVal dispStr As String)
            Console.WriteLine(dispStr)
        End Sub
        
    End Class
    
End Namespace
