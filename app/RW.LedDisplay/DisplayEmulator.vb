Imports System.Drawing
Imports System.Threading

' Software model of the firmware's ProcessProgram(): runs a program over the loaded picture and
' hands every resulting frame to ShowScreen. Opcodes have to match DisplayCommandType and main.c.
Public Class DisplayEmulator
    Public Const Width As Integer = 32
    Public Const Height As Integer = 10

    ' Picture the Frame/FromX/Scroll steps cut their window from, (x, y), any width
    Public Property Pixels As Color(,)
    ' Called on the thread that runs Run(); the array is reused for the next frame
    Public Property ShowScreen As Action(Of Color(,))

    Private stopRequested As Boolean
    Private buttonPressed As Boolean

    Public Sub RequestStop()
        Volatile.Write(stopRequested, True)
    End Sub

    ' The button the Wait step is waiting for
    Public Sub PressButton()
        Volatile.Write(buttonPressed, True)
    End Sub

    ' Blocks until the program ends or RequestStop() is called; programs with Restart never end
    Public Sub Run(commands As IList(Of DisplayCommand))
        Dim screen(Width - 1, Height - 1) As Color
        Dim bright As Integer = 100
        Volatile.Write(stopRequested, False)

        Dim i = 0
        Do While i < commands.Count AndAlso Not Volatile.Read(stopRequested)
            Dim cmd = commands(i)
            Select Case cmd.Cmd
                Case DisplayCommandType.Clear
                    Fill(screen, Color.FromArgb(255, 0, 0, 0))
                    ShowScreen?.Invoke(screen)
                Case DisplayCommandType.Fill
                    Fill(screen, Color.FromArgb(255, cmd.Arg0, cmd.Arg1, cmd.Arg2))
                    ShowScreen?.Invoke(screen)
                Case DisplayCommandType.Frame
                    CopyPixels(screen, Width * cmd.Arg0, bright)
                    ShowScreen?.Invoke(screen)
                Case DisplayCommandType.FromX
                    CopyPixels(screen, cmd.Arg0, bright)
                    ShowScreen?.Invoke(screen)
                Case DisplayCommandType.ScrollLeft
                    For x = Width * cmd.Arg0 To Width * cmd.Arg1 - 1
                        CopyPixels(screen, x, bright)
                        ShowScreen?.Invoke(screen)
                        If Not Sleep(cmd.Arg2) Then Exit For
                    Next
                Case DisplayCommandType.ScrollRight
                    For x = Width * cmd.Arg1 - 1 To Width * cmd.Arg0 Step -1
                        CopyPixels(screen, x, bright)
                        ShowScreen?.Invoke(screen)
                        If Not Sleep(cmd.Arg2) Then Exit For
                    Next
                Case DisplayCommandType.Bright
                    bright = cmd.Arg0
                Case DisplayCommandType.Restart
                    i = -1
                Case DisplayCommandType.Wait
                    Volatile.Write(buttonPressed, False)
                    Do Until Volatile.Read(buttonPressed) OrElse Not Sleep(100)
                    Loop
                Case DisplayCommandType.Msec
                    Sleep(cmd.Arg0)
                Case DisplayCommandType.Sec
                    Sleep(cmd.Arg0 * 1000)
            End Select
            i += 1
        Loop
    End Sub

    ' Sleeps in short slices so a stop request is noticed; False when stopped
    Private Function Sleep(milliseconds As Integer) As Boolean
        Dim left = milliseconds
        Do While left > 0 AndAlso Not Volatile.Read(stopRequested)
            Dim slice = Math.Min(left, 100)
            Thread.Sleep(slice)
            left -= slice
        Loop
        Return Not Volatile.Read(stopRequested)
    End Function

    Private Shared Sub Fill(screen(,) As Color, color As Color)
        For x = 0 To Width - 1
            For y = 0 To Height - 1
                screen(x, y) = color
            Next
        Next
    End Sub

    ' The picture on a monitor is lifted above the real brightness scale: at the display's working
    ' brightness of a few percent it would be nearly black on screen
    Private Sub CopyPixels(screen(,) As Color, offset As Integer, bright As Integer)
        Dim source = Pixels
        For x = 0 To Width - 1
            For y = 0 To Height - 1
                Dim sx = x + offset
                If source IsNot Nothing AndAlso sx < source.GetLength(0) AndAlso bright > 0 Then
                    Dim clr = source(sx, y)
                    Dim minimal = 50
                    Dim useBright = bright + 20
                    Dim r = If(clr.R > 0, Math.Min(CInt(minimal + clr.R * useBright / 100), 255), 30)
                    Dim g = If(clr.G > 0, Math.Min(CInt(minimal + clr.G * useBright / 100), 255), 30)
                    Dim b = If(clr.B > 0, Math.Min(CInt(minimal + clr.B * useBright / 100), 255), 30)
                    screen(x, y) = Color.FromArgb(r, g, b)
                Else
                    screen(x, y) = Color.Black
                End If
            Next
        Next
    End Sub
End Class
