Imports System
Namespace HelloWorld

    ''' <summary>
    ''' プログラム型
    ''' </summary>
    Public Class Program

        ''' <summary>挨拶１</summary>
       Private Shared aisatsu1 As String

        ''' <summary>メイン</summary>
        ''' <param name=args>引数</param>
        Shared Sub Main(ByVal args As String())

           aisatsu1="Hello World one!"
           ConsoleOut(aisatsu1)
        End Sub

        ''' <summary>コンソール表示する</summary>
        ''' <param name=args>引数</param>
       Private Shared Sub ConsoleOut(ByVal args2 As String)

           Console.WriteLine(args2)
       End Sub

    End Class

End Namespace

