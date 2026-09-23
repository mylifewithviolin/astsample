Imports System

Namespace BinarySearch
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
            Dim index As Integer = BinarySearch(array, target)
            If index <> -1 Then
                ConsoleOut(index)
            Else
                ConsoleOut("該当なし")
            End If
        End Sub
        
        ''' <summary>二分探索する</summary>
        ''' <param name=array>配列</param>
        ''' <param name=target>探す値</param>
        Public Shared Function BinarySearch(ByVal array As Integer(), ByVal target As Integer) As Integer
            ''' <summary>左端</summary>
            Dim left As Integer = 0
            ''' <summary>右端</summary>
            Dim right As Integer = array.Length - 1
            While left <= right
                ''' <summary>中央</summary>
                Dim mid As Integer = (left + right) \ 2
                If array(mid) = target Then
                    Return mid
                End If
                If array(mid) < target Then
                    left = mid + 1
                Else
                    right = mid - 1
                End If
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
