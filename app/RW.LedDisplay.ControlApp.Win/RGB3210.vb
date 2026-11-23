Imports System.IO.Ports

Public Class RGB3210
    Dim pixels As Color(,)
    Dim thread As Threading.Thread
    Private emulator As New DisplayEmulator With {.ShowScreen = AddressOf ShowScreen}
    Private display As New DisplaySender
    Private graphics As Graphics
    Private Sub App_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        cbPorts.Items.AddRange(IO.Ports.SerialPort.GetPortNames())
        If cbPorts.Items.Count > 0 Then
            cbPorts.SelectedIndex = 0
        End If
    End Sub

    Private Sub tbPath_TextChanged(sender As Object, e As EventArgs) Handles tbPath.TextChanged
        Try
            pbImage.Image = Nothing
            Dim bmp = New Bitmap(Image.FromFile(tbPath.Text))
            If bmp.Height <> 10 Or bmp.Width < 32 Then
                Throw New Exception("Image height must be 10 pixels and width must be at least 32 pixels")
            End If
            ReDim pixels(bmp.Width - 1, bmp.Height - 1)
            For x = 0 To bmp.Width - 1
                For y = 0 To bmp.Height - 1
                    pixels(x, y) = bmp.GetPixel(x, y)
                Next
            Next
            emulator.Pixels = pixels
            lError.Text = ""
            pbImage.Image = bmp
            bSendToBoard.Enabled = True
        Catch ex As Exception
            lError.Text = $"Error loading image: {ex.Message}"
            bSendToBoard.Enabled = False
        End Try
    End Sub

    Private Sub bSelectFile_Click(sender As Object, e As EventArgs) Handles bSelectFile.Click
        OpenFileDialog1.CheckFileExists = True
        OpenFileDialog1.Filter = "Image files|*.bmp;*.jpg;*.jpeg;*.png;*.gif|All files|*.*"
        If OpenFileDialog1.ShowDialog() = DialogResult.OK Then
            tbPath.Text = OpenFileDialog1.FileName
        End If
    End Sub

    Public Function InterpreteProgram(program As String) As List(Of DisplayCommand)
        Return DisplayCommand.ParseProgram(program)
    End Function

    Private Sub bEmulate_Click(sender As Object, e As EventArgs) Handles bEmulate.Click
        If thread Is Nothing Then
            Try
                Dim commands = InterpreteProgram(tbProgram.Text)
                bEmulate.Text = "Stop"
                thread = New Threading.Thread(Sub()
                                                  emulator.Run(commands)
                                                  thread = Nothing
                                                  Me.Invoke(Sub() bEmulate.Text = "Emulate")
                                              End Sub)
                thread.Start()
            Catch ex As Exception
                MsgBox(ex.Message, MsgBoxStyle.Critical)
            End Try
        Else
            emulator.RequestStop()
            bEmulate.Text = "Stopping..."
        End If
    End Sub

    Private Sub ShowScreen(screen(,) As Color)
        Me.Invoke(Sub()
                      If Graphics Is Nothing Then
                          Graphics = pbDisplay.CreateGraphics()
                      End If
                      'graphics.Clear(Color.Black)
                      For x = 0 To 31
                          For y = 0 To 9
                              Graphics.FillRectangle(New SolidBrush(screen(x, y)), x * 10 + 1, y * 10 + 1, 8, 8)
                          Next
                      Next
                  End Sub)
    End Sub

    Private Sub pbDisplay_Click(sender As Object, e As EventArgs) Handles pbDisplay.Click
        emulator.PressButton()
    End Sub

    Private Sub bSendToBoard_Click(sender As Object, e As EventArgs) Handles bSendToBoard.Click
        Try
            display.Send(DisplayProtocol.ProgramBytes(InterpreteProgram(tbProgram.Text)), DisplayProtocol.PixelBytes(pixels))
        Catch ex As Exception
            MsgBox(ex.Message)
        End Try
    End Sub

    Private Sub cbPorts_TextChanged(sender As Object, e As EventArgs) Handles cbPorts.TextChanged
        display.SerialPort = cbPorts.SelectedItem.ToString()
    End Sub

    Private Sub bStart_Click(sender As Object, e As EventArgs) Handles bStart.Click
        display.Send(DisplayProtocol.StartProgram())
    End Sub

    Private Sub bStop_Click(sender As Object, e As EventArgs) Handles bStop.Click
        display.Send(DisplayProtocol.StopProgram(), DisplayProtocol.ClearDisplay)
    End Sub

    Private Sub bWriteToFlash_Click(sender As Object, e As EventArgs) Handles bWriteToFlash.Click
        display.Send(DisplayProtocol.SaveToFlash())
    End Sub
End Class
