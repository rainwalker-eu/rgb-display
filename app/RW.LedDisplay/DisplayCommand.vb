Public Class DisplayCommand
    Public Property Cmd As DisplayCommandType
    Public Property Arg0 As Byte
    Public Property Arg1 As Byte
    Public Property Arg2 As Byte

    Public Overrides Function ToString() As String
        Return $"Cmd: {Cmd}, Arg0: {Arg0}, Arg1: {Arg1}, Arg2: {Arg2}"
    End Function

    Public Function ToBytes() As Byte()
        Return New Byte() {CByte(Cmd), Arg0, Arg1, Arg2}
    End Function
    Public Sub New(cmd As DisplayCommandType, Optional arg0 As Byte = 0, Optional arg1 As Byte = 0, Optional arg2 As Byte = 0)
        Me.Cmd = cmd
        Me.Arg0 = arg0
        Me.Arg1 = arg1
        Me.Arg2 = arg2
    End Sub

    Public Sub New(line As String)
        If String.IsNullOrWhiteSpace(line) Then Throw New ArgumentException("line cannot be null or whitespace", NameOf(line))
        Dim parts = line.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length > 0 Then
            If [Enum].TryParse(parts(0), True, Cmd) Then
                If parts.Length > 1 Then Byte.TryParse(parts(1), Arg0)
                If parts.Length > 2 Then Byte.TryParse(parts(2), Arg1)
                If parts.Length > 3 Then Byte.TryParse(parts(3), Arg2)
            Else
                Throw New ArgumentException($"Invalid command: {parts(0)}", NameOf(line))
            End If
        Else
            Throw New ArgumentException("line must contain at least a command", NameOf(line))
        End If
    End Sub

    ' Program text, one "Cmd arg0 arg1 arg2" step per line; blank lines are skipped
    Public Shared Function ParseProgram(program As String) As List(Of DisplayCommand)
        Dim commands As New List(Of DisplayCommand)
        For Each line In program.Split({Convert.ToChar(13), Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries)
            If Not String.IsNullOrWhiteSpace(line) Then commands.Add(New DisplayCommand(line))
        Next
        Return commands
    End Function
End Class
Public Enum DisplayCommandType As Byte
    Nop = 0

    Bright = 1

    Clear = 8
    Fill = 9

    Frame = 16
    FromX = 17

    Wait = 64
    Msec = 65
    Sec = 66

    ScrollLeft = 128
    ScrollRight = 129

    Restart = 254
End Enum