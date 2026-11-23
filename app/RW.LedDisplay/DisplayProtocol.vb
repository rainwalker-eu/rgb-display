Imports System.Drawing

Public Class DisplayProtocol
    Public Property SerialPort As String
    Public Shared Function ProgramBytes(commands As IEnumerable(Of DisplayCommand)) As Byte()
        Dim bytes As New List(Of Byte)
        bytes.AddRange({170, 16}) 'set buffer index to 0 (start of program data)
        For Each cmd In commands
            For Each b In cmd.ToBytes()
                If b = 170 Then
                    bytes.AddRange({170, 0}) 'escape 170 byte
                Else
                    bytes.Add(b)
                End If
            Next
        Next
        Dim endCommand As DisplayCommand = New DisplayCommand(DisplayCommandType.Restart)
        bytes.AddRange(endCommand.ToBytes())
        Return bytes.ToArray()
    End Function
    Public Shared Function PixelBytes(image As Color(,)) As Byte()
        Dim bytes As New List(Of Byte)
        For y As Integer = 0 To 9
            bytes.AddRange({170, 17 + y}) 'set buffer index to start of pixel data of line y
            For x As Integer = 0 To image.GetLength(0) - 1
                Dim pixel As Color = image(x, y)
                If (pixel.G = 170) Then bytes.AddRange({170, 0}) Else bytes.Add(pixel.G)
                If (pixel.R = 170) Then bytes.AddRange({170, 0}) Else bytes.Add(pixel.R)
                If (pixel.B = 170) Then bytes.AddRange({170, 0}) Else bytes.Add(pixel.B)
            Next
        Next
        Return bytes.ToArray()
    End Function

    Public Shared Function StartProgram() As Byte()
        Return {170, 64}
    End Function

    Public Shared Function StopProgram() As Byte()
        Return {170, 65}
    End Function
    Public Shared Function SkipWait() As Byte()
        Return {170, 66}
    End Function
    Public Shared Function ClearDisplay() As Byte()
        Return {170, 128}
    End Function
    Public Shared Function SaveToFlash() As Byte()
        Return {170, 129}
    End Function
    ' The device answers with a text line "temp=<degC> limit=<percent>\n" (MCU die temperature, thermal brightness limit)
    Public Shared Function ReadStatus() As Byte()
        Return {170, 130}
    End Function
End Class
