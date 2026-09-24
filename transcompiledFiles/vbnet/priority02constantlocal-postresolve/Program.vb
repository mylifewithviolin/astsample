Imports System

Namespace Priority02ConstantLocal
    ''' <summary>プログラム型</summary>
    Public Class Program
        ''' <summary>メイン</summary>
        ''' <param name=args>引数</param>
        Public Shared Sub Main(ByVal args As String())
            ''' <summary>制限値</summary>
            Const limit As Integer = 3
            ''' <summary>固定文</summary>
            Const label As String = "constant-ok"
            Console.WriteLine(limit)
            Console.WriteLine(label)
        End Sub
        
    End Class
    
End Namespace
