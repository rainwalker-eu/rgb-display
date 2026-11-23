Public Class DisplaySender
    Public Property SerialPort As String

    Public Sub Send(ParamArray byteArrays() As IEnumerable(Of Byte))
        Dim bytes As New List(Of Byte)
        For Each byteArray In byteArrays
            If byteArray IsNot Nothing Then bytes.AddRange(byteArray)
        Next
        If bytes.Count = 0 Then Return
        Using port As New IO.Ports.SerialPort(SerialPort, 115200, IO.Ports.Parity.None, 8, IO.Ports.StopBits.One)
            Try
                port.Open()
                Dim partSize = 64
                For i As Integer = 0 To bytes.Count - 1 Step partSize
                    Dim part = bytes.Skip(i).Take(partSize).ToArray()
                    port.Write(part, 0, part.Length)
                    Threading.Thread.Sleep(10) 'small delay to avoid overwhelming the serial buffer
                Next
            Catch ex As Exception
                Throw New Exception($"Error sending data to display: {ex.Message}", ex)
            Finally
                If port.IsOpen Then
                    port.Close()
                End If
            End Try
        End Using
    End Sub
End Class
