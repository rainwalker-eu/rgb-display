<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class RGB3210
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(RGB3210))
        tbPath = New TextBox()
        bSelectFile = New Button()
        pbImage = New PictureBox()
        OpenFileDialog1 = New OpenFileDialog()
        bLoadImage = New Button()
        cbPorts = New ComboBox()
        Label1 = New Label()
        Label2 = New Label()
        tbProgram = New TextBox()
        Label4 = New Label()
        TextBox2 = New TextBox()
        bOpenProgram = New Button()
        bSaveProgram = New Button()
        Label3 = New Label()
        pbDisplay = New PictureBox()
        bEmulate = New Button()
        bSendToBoard = New Button()
        bWriteToFlash = New Button()
        bStart = New Button()
        bStop = New Button()
        lError = New Label()
        CType(pbImage, ComponentModel.ISupportInitialize).BeginInit()
        CType(pbDisplay, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' tbPath
        ' 
        tbPath.Location = New Point(12, 12)
        tbPath.Name = "tbPath"
        tbPath.Size = New Size(824, 23)
        tbPath.TabIndex = 0
        tbPath.Text = "test.bmp"
        ' 
        ' bSelectFile
        ' 
        bSelectFile.Location = New Point(842, 11)
        bSelectFile.Name = "bSelectFile"
        bSelectFile.Size = New Size(95, 23)
        bSelectFile.TabIndex = 1
        bSelectFile.Text = "Open image"
        bSelectFile.UseVisualStyleBackColor = True
        ' 
        ' pbImage
        ' 
        pbImage.BorderStyle = BorderStyle.FixedSingle
        pbImage.Location = New Point(12, 72)
        pbImage.Name = "pbImage"
        pbImage.Size = New Size(925, 100)
        pbImage.SizeMode = PictureBoxSizeMode.Zoom
        pbImage.TabIndex = 3
        pbImage.TabStop = False
        ' 
        ' OpenFileDialog1
        ' 
        OpenFileDialog1.FileName = "OpenFileDialog1"
        ' 
        ' bLoadImage
        ' 
        bLoadImage.Enabled = False
        bLoadImage.Location = New Point(715, 225)
        bLoadImage.Name = "bLoadImage"
        bLoadImage.Size = New Size(222, 34)
        bLoadImage.TabIndex = 5
        bLoadImage.Text = "Load image and program from board"
        bLoadImage.UseVisualStyleBackColor = True
        ' 
        ' cbPorts
        ' 
        cbPorts.FormattingEnabled = True
        cbPorts.Location = New Point(715, 196)
        cbPorts.Name = "cbPorts"
        cbPorts.Size = New Size(222, 23)
        cbPorts.TabIndex = 6
        ' 
        ' Label1
        ' 
        Label1.Font = New Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label1.ForeColor = Color.FromArgb(CByte(192), CByte(0), CByte(0))
        Label1.Location = New Point(9, 539)
        Label1.Name = "Label1"
        Label1.Size = New Size(928, 26)
        Label1.TabIndex = 7
        Label1.Text = "Board does not have temperature protection and may be damaged if it overheats! Do not enable too many pixels at high brightness without a heatsink."
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Location = New Point(715, 178)
        Label2.Name = "Label2"
        Label2.Size = New Size(29, 15)
        Label2.TabIndex = 8
        Label2.Text = "Port"
        ' 
        ' tbProgram
        ' 
        tbProgram.Location = New Point(12, 196)
        tbProgram.Multiline = True
        tbProgram.Name = "tbProgram"
        tbProgram.Size = New Size(317, 205)
        tbProgram.TabIndex = 11
        tbProgram.Text = "BRIGHT 3" & vbCrLf & "FRAME 0" & vbCrLf & "SEC 1" & vbCrLf & "SCROLLLEFT 0 2 30" & vbCrLf & "SCROLLRIGHT 0 2 30" & vbCrLf & "SEC 1" & vbCrLf & "FILL 255 255 255" & vbCrLf & "MSEC 10" & vbCrLf & "CLEAR" & vbCrLf & "MSEC 250" & vbCrLf & "FILL 255 0 0" & vbCrLf & "MSEC 10" & vbCrLf & "CLEAR" & vbCrLf & "SEC 1"
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.Location = New Point(12, 178)
        Label4.Name = "Label4"
        Label4.Size = New Size(53, 15)
        Label4.TabIndex = 12
        Label4.Text = "Program"
        ' 
        ' TextBox2
        ' 
        TextBox2.BackColor = SystemColors.Control
        TextBox2.BorderStyle = BorderStyle.None
        TextBox2.Location = New Point(335, 196)
        TextBox2.Multiline = True
        TextBox2.Name = "TextBox2"
        TextBox2.ReadOnly = True
        TextBox2.Size = New Size(385, 205)
        TextBox2.TabIndex = 13
        TextBox2.Text = resources.GetString("TextBox2.Text")
        ' 
        ' bOpenProgram
        ' 
        bOpenProgram.Enabled = False
        bOpenProgram.Location = New Point(12, 407)
        bOpenProgram.Name = "bOpenProgram"
        bOpenProgram.Size = New Size(108, 23)
        bOpenProgram.TabIndex = 14
        bOpenProgram.Text = "Open program"
        bOpenProgram.UseVisualStyleBackColor = True
        ' 
        ' bSaveProgram
        ' 
        bSaveProgram.Enabled = False
        bSaveProgram.Location = New Point(126, 407)
        bSaveProgram.Name = "bSaveProgram"
        bSaveProgram.Size = New Size(108, 23)
        bSaveProgram.TabIndex = 15
        bSaveProgram.Text = "Save program"
        bSaveProgram.UseVisualStyleBackColor = True
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Location = New Point(335, 178)
        Label3.Name = "Label3"
        Label3.Size = New Size(69, 15)
        Label3.TabIndex = 16
        Label3.Text = "Commands"
        ' 
        ' pbDisplay
        ' 
        pbDisplay.BackColor = Color.Black
        pbDisplay.BorderStyle = BorderStyle.FixedSingle
        pbDisplay.Location = New Point(9, 436)
        pbDisplay.Name = "pbDisplay"
        pbDisplay.Size = New Size(320, 100)
        pbDisplay.SizeMode = PictureBoxSizeMode.Zoom
        pbDisplay.TabIndex = 17
        pbDisplay.TabStop = False
        ' 
        ' bEmulate
        ' 
        bEmulate.Location = New Point(240, 407)
        bEmulate.Name = "bEmulate"
        bEmulate.Size = New Size(89, 23)
        bEmulate.TabIndex = 18
        bEmulate.Text = "Emulate"
        bEmulate.UseVisualStyleBackColor = True
        ' 
        ' bSendToBoard
        ' 
        bSendToBoard.Location = New Point(715, 265)
        bSendToBoard.Name = "bSendToBoard"
        bSendToBoard.Size = New Size(222, 34)
        bSendToBoard.TabIndex = 19
        bSendToBoard.Text = "Send to board"
        bSendToBoard.UseVisualStyleBackColor = True
        ' 
        ' bWriteToFlash
        ' 
        bWriteToFlash.Location = New Point(715, 345)
        bWriteToFlash.Name = "bWriteToFlash"
        bWriteToFlash.Size = New Size(222, 34)
        bWriteToFlash.TabIndex = 20
        bWriteToFlash.Text = "Write data to flash"
        bWriteToFlash.UseVisualStyleBackColor = True
        ' 
        ' bStart
        ' 
        bStart.Location = New Point(715, 305)
        bStart.Name = "bStart"
        bStart.Size = New Size(110, 34)
        bStart.TabIndex = 21
        bStart.Text = "Start program"
        bStart.UseVisualStyleBackColor = True
        ' 
        ' bStop
        ' 
        bStop.Location = New Point(827, 305)
        bStop.Name = "bStop"
        bStop.Size = New Size(110, 34)
        bStop.TabIndex = 22
        bStop.Text = "Clear and stop"
        bStop.UseVisualStyleBackColor = True
        ' 
        ' lError
        ' 
        lError.Font = New Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lError.ForeColor = Color.FromArgb(CByte(192), CByte(0), CByte(0))
        lError.Location = New Point(12, 43)
        lError.Name = "lError"
        lError.Size = New Size(928, 17)
        lError.TabIndex = 23
        ' 
        ' RGB3210
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(949, 567)
        Controls.Add(lError)
        Controls.Add(bStop)
        Controls.Add(bStart)
        Controls.Add(bWriteToFlash)
        Controls.Add(bSendToBoard)
        Controls.Add(bEmulate)
        Controls.Add(pbDisplay)
        Controls.Add(Label3)
        Controls.Add(bSaveProgram)
        Controls.Add(bOpenProgram)
        Controls.Add(Label4)
        Controls.Add(tbProgram)
        Controls.Add(Label2)
        Controls.Add(Label1)
        Controls.Add(cbPorts)
        Controls.Add(bLoadImage)
        Controls.Add(pbImage)
        Controls.Add(bSelectFile)
        Controls.Add(tbPath)
        Controls.Add(TextBox2)
        FormBorderStyle = FormBorderStyle.FixedSingle
        MaximizeBox = False
        Name = "RGB3210"
        Text = "RW.RGB3210 Control App"
        CType(pbImage, ComponentModel.ISupportInitialize).EndInit()
        CType(pbDisplay, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents tbPath As TextBox
    Friend WithEvents bSelectFile As Button
    Friend WithEvents pbImage As PictureBox
    Friend WithEvents OpenFileDialog1 As OpenFileDialog
    Friend WithEvents bLoadImage As Button
    Friend WithEvents cbPorts As ComboBox
    Friend WithEvents Label1 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents tbProgram As TextBox
    Friend WithEvents Label4 As Label
    Friend WithEvents TextBox2 As TextBox
    Friend WithEvents bOpenProgram As Button
    Friend WithEvents bSaveProgram As Button
    Friend WithEvents Label3 As Label
    Friend WithEvents pbDisplay As PictureBox
    Friend WithEvents bEmulate As Button
    Friend WithEvents bSendToBoard As Button
    Friend WithEvents bWriteToFlash As Button
    Friend WithEvents bStart As Button
    Friend WithEvents bStop As Button
    Friend WithEvents lError As Label

End Class
