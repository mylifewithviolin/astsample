Imports System

Namespace Priority05Exception
    ''' <summary>プログラム型</summary>
    Public Class Program
        ''' <summary>メイン</summary>
        ''' <param name=args>引数</param>
        Public Shared Sub Main(ByVal args As String())
            Try
                Throw New Exception("planned")
            Catch e As Exception
                Console.WriteLine("exception=caught")
            Finally
                Console.WriteLine("finally=done")
            End Try
        End Sub
        
    End Class
    
End Namespace
