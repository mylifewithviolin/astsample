Imports System

Namespace BinarySearch

    ''' <summary>
    ''' プログラム型
    ''' </summary>
    Public Class Program

        ''' <summary>
        ''' メイン
        ''' </summary>
        ''' <param name="args">引数</param>
        Shared Sub Main(args As String())

            ''' <summary>array （配列）</summary>
            Dim array As Integer() = {15, 13, 9, 6, 4, 1}

            ''' <summary>target （探す値）</summary>
            Dim target As Integer = 4

            ''' <summary>index （該当インデックス）</summary>
            Dim index As Integer = BinarySearch(array, target)

            If index <> -1 Then
                ConsoleOut(index.ToString())
            Else
                ConsoleOut("該当なし")
            End If
        End Sub

        ''' <summary>
        ''' 二分探索する
        ''' </summary>
        ''' <param name="array">配列</param>
        ''' <param name="target">探す値</param>
        ''' <returns>index</returns>
        Public Shared Function BinarySearch(array As Integer(), target As Integer) As Integer

            ''' <summary>left （左端）</summary>
            Dim left As Integer = 0

            ''' <summary>right （右端）</summary>
            Dim right As Integer = array.Length - 1

            While left <= right

                ''' <summary>mid （中央）</summary>
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

        ''' <summary>
        ''' コンソール表示する
        ''' </summary>
        ''' <param name="dispStr">引数2</param>
        Shared Sub ConsoleOut(dispStr As String)
            Console.WriteLine(dispStr)
        End Sub

    End Class
End Namespace
