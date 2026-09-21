Imports System

Namespace BubbleSort

    ''' <summary>
    ''' プログラム型
    ''' </summary>
    Public Class ProgramType

        ''' <summary>
        ''' メイン<
        ''' </summary>
        ''' <param name="args">引数</param>
        Public Shared Sub Main(args As String())

            ''' <summary>array （配列）</summary>
            Dim array As Integer() = {15, 13, 9, 6, 4, 1}

            ''' <summary>index （該当インデックス）</summary>
            BubbleSort(array)

            For i As Integer = 0 To array.Length - 1
                ConsoleOut(array(i).ToString())
            Next
        End Sub

        ''' <summary>
        ''' バブルソートする
        ''' </summary>
        ''' <param name="array">配列</param>
        Public Shared Sub BubbleSort(array As Integer())

            ''' <summary>outer （外側）</summary>
            Dim outer As Integer = 0

            While outer < array.Length

                ''' <summary>inner （内側）</summary>
                Dim inner As Integer = 0

                While inner < array.Length - outer - 1

                    If array(inner) > array(inner + 1) Then

                        ''' <summary>temp （一時）</summary>
                        Dim temp As Integer = array(inner)
                        array(inner) = array(inner + 1)
                        array(inner + 1) = temp
                    End If

                    inner += 1
                End While

                outer += 1
            End While
        End Sub

        ''' <summary>
        ''' コンソール表示する
        ''' </summary>
        ''' <param name="dispStr">引数2</param>
        Public Shared Sub ConsoleOut(dispStr As String)
            Console.WriteLine(dispStr)
        End Sub

    End Class
End Namespace
